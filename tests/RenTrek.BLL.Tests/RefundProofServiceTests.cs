using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Tests.Fixtures;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Test nghiệp vụ chứng từ hoàn tiền: hoàn tất thủ công kèm ảnh và nhập Excel kết quả (dry-run / apply).
/// Fixture bám RefundCaseServiceTests (TestProxy + repo giả). Các điểm có thể lệch với BE thật được gom ở
/// Fixture.CreateService / IUnitOfWork.RefundProofs / IPrivateFileStorage để dễ chỉnh ở phase 2.
/// </summary>
public sealed class RefundProofServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ Complete thủ công + ảnh

    [Fact]
    public async Task Complete_WithProofs_StoresProofsAndCompletesCase()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var result = await fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 },
            [fixture.File(TestImages.Png(1, 1, 1)), fixture.File(TestImages.Jpeg(), "b.jpg", "image/jpeg")]);

        Assert.Equal(RefundCaseStatuses.Completed, result.Status);
        Assert.Equal(2, fixture.Proofs.Count);
        Assert.All(fixture.Proofs, proof =>
        {
            Assert.Equal(refund.RefundCaseId, proof.RefundCaseId);
            Assert.Null(proof.PaymentId);
            Assert.Equal(fixture.Admin, proof.UploadedByUserId);
            Assert.False(string.IsNullOrWhiteSpace(proof.StoragePath));
        });
        Assert.Equal(2, fixture.Storage.Saved.Count);
    }

    [Fact]
    public async Task Complete_WithoutFiles_IsRejectedAndNothingChanges()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        await AssertRejectedAsync(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 },
            []));

        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Empty(fixture.Proofs);
        Assert.Empty(fixture.Storage.Saved);
    }

    [Fact]
    public async Task Complete_MoreThanThreeFiles_IsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        var files = Enumerable.Range(0, 4).Select(i => fixture.File(TestImages.Png((byte)i, 0, 0), $"{i}.png")).ToList();

        await AssertRejectedAsync(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 },
            files));

        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Empty(fixture.Proofs);
    }

    [Fact]
    public async Task Complete_FileWithWrongMagicBytes_IsRejectedEvenWhenContentTypeLooksLikeImage()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        await AssertRejectedAsync(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 },
            [fixture.File(TestImages.NotAnImage(), "fake.png", "image/png")]));

        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Empty(fixture.Proofs);
    }

    [Fact]
    public async Task Complete_FileOverFiveMegabytes_IsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        await AssertRejectedAsync(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 },
            [fixture.File(TestImages.OversizedPng())]));

        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
    }

    [Fact]
    public async Task Complete_DuplicateTransactionId_RejectedWithoutKeepingFiles()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.Completed).ProviderTransactionId = "BANK-1";
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 },
            [fixture.File(TestImages.Png())]));

        Assert.Equal(ErrorCodes.RefundTransactionDuplicated, error.ErrorCode);
        Assert.Empty(fixture.Proofs);
        Assert.Equal(fixture.Storage.Saved.Count, fixture.Storage.Deleted.Count); // không để file mồ côi
    }

    [Fact]
    public async Task Complete_WhenSaveFails_UploadedFilesAreDeleted()
    {
        var fixture = new Fixture { FailSaveChanges = true };
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 },
            [fixture.File(TestImages.Png())]));

        Assert.NotEmpty(fixture.Storage.Saved);
        Assert.Equal(fixture.Storage.Saved.OrderBy(x => x), fixture.Storage.Deleted.OrderBy(x => x));
    }

    // ------------------------------------------------------------------ Import: dry-run

    [Fact]
    public async Task ImportDryRun_ReturnsPreviewAndWritesNothing()
    {
        var fixture = new Fixture();
        var ok = fixture.Seed(RefundCaseStatuses.Approved);
        var fail = fixture.Seed(RefundCaseStatuses.Approved);
        var blank = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        var file = Workbook(
            Spec(ok, "Có", "TX-1", images: [TestImages.Png()]),
            Spec(fail, "Không", reason: "Sai STK"),
            Spec(blank, null));

        var report = await fixture.Service.ImportResultAsync(new MemoryStream(file), "ketqua.xlsx", dryRun: true);

        Assert.True(report.DryRun);
        Assert.Null(report.BatchId);
        Assert.Equal(3, report.TotalRows);
        Assert.Equal(2, report.ValidRows);
        Assert.Equal(1, report.ErrorCount);
        Assert.Equal("Complete", report.Rows[0].Action);
        Assert.Equal(1, report.Rows[0].ImageCount);
        Assert.Equal("Fail", report.Rows[1].Action);
        Assert.Equal("Skip", report.Rows[2].Action);
        Assert.All(report.Rows, row => Assert.False(row.Applied));
        // không ghi gì
        Assert.Equal(RefundCaseStatuses.Approved, ok.Status);
        Assert.Equal(RefundCaseStatuses.Approved, fail.Status);
        Assert.Empty(fixture.Proofs);
        Assert.Empty(fixture.Storage.Saved);
        Assert.Empty(fixture.AuditLogs);
        Assert.Equal(0, fixture.Commits);
    }

    // ------------------------------------------------------------------ Import: apply

    [Fact]
    public async Task ImportApply_CompleteRow_CompletesCaseAndStoresExcelProofs()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        var file = Workbook(Spec(refund, "Có", " FT-001 ", images: [TestImages.Png(1, 2, 3), TestImages.Png(4, 5, 6)]));

        var report = await fixture.Service.ImportResultAsync(new MemoryStream(file), "ketqua.xlsx", dryRun: false);

        Assert.False(report.DryRun);
        Assert.NotNull(report.BatchId);
        Assert.Equal(1, report.CompletedCount);
        var row = Assert.Single(report.Rows);
        Assert.True(row.Applied);
        Assert.Equal("Completed", row.ResultStatus);
        Assert.Equal(RefundCaseStatuses.Completed, refund.Status);
        Assert.Equal("FT-001", refund.ProviderTransactionId);
        Assert.Equal(PaymentStatuses.Succeeded, refund.RefundPayment.Status);
        Assert.Equal(2, fixture.Proofs.Count);
        Assert.All(fixture.Proofs, proof => Assert.Equal("ExcelImport", proof.Source));
        Assert.Contains(fixture.AuditLogs, log => log.Action == "RefundCompleted" && log.Details.Contains(report.BatchId!.ToString()!));
    }

    [Fact]
    public async Task ImportApply_FailRow_ReturnsCaseToCustomerForNewBankInfo()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        var file = Workbook(Spec(refund, "Không", reason: "  Sai số tài khoản  "));

        var report = await fixture.Service.ImportResultAsync(new MemoryStream(file), "ketqua.xlsx", dryRun: false);

        Assert.Equal(1, report.FailedCount);
        Assert.Equal(RefundCaseStatuses.AwaitingBankInfo, Assert.Single(report.Rows).ResultStatus);
        Assert.Equal(RefundCaseStatuses.AwaitingBankInfo, refund.Status);
        Assert.Equal("Sai số tài khoản", refund.RejectionReason);
        Assert.Null(refund.PayoutDetailsCiphertext);
        Assert.Null(refund.BankCode);
        Assert.Equal(PaymentStatuses.PendingApproval, refund.RefundPayment.Status);
        Assert.Empty(fixture.Proofs);
    }

    [Fact]
    public async Task ImportApply_PartialSuccess_ValidRowsApplyIndependently()
    {
        var fixture = new Fixture();
        var good = fixture.Seed(RefundCaseStatuses.Approved);
        var noImage = fixture.Seed(RefundCaseStatuses.Approved);
        var failGood = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        var file = Workbook(
            Spec(good, "Có", "TX-GOOD", images: [TestImages.Png()]),
            Spec(noImage, "Có", "TX-NOIMG"),
            Spec(failGood, "Không", reason: "Không chuyển được"));

        var report = await fixture.Service.ImportResultAsync(new MemoryStream(file), "ketqua.xlsx", dryRun: false);

        Assert.Equal(1, report.CompletedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.ErrorCount);
        Assert.Equal(RefundCaseStatuses.Completed, good.Status);
        Assert.Equal(RefundCaseStatuses.Approved, noImage.Status);
        Assert.Equal(RefundCaseStatuses.AwaitingBankInfo, failGood.Status);
        Assert.NotEmpty(report.Rows[1].Errors);
        Assert.False(report.Rows[1].Applied);
    }

    [Fact]
    public async Task ImportApply_ReuploadingSameFile_IsIdempotent()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        var file = Workbook(Spec(refund, "Có", "TX-1", images: [TestImages.Png()]));
        await fixture.Service.ImportResultAsync(new MemoryStream(file), "ketqua.xlsx", dryRun: false);
        var proofsAfterFirst = fixture.Proofs.Count;

        var second = await fixture.Service.ImportResultAsync(new MemoryStream(file), "ketqua.xlsx", dryRun: false);

        Assert.Equal(0, second.CompletedCount);
        Assert.Equal(1, second.ErrorCount);
        var row = Assert.Single(second.Rows);
        Assert.Equal("Skip", row.Action);
        Assert.NotEmpty(row.Errors);
        Assert.False(row.Applied);
        Assert.Equal(proofsAfterFirst, fixture.Proofs.Count);
        Assert.Equal(RefundCaseStatuses.Completed, refund.Status);
    }

    // ------------------------------------------------------------------ Import: quy tắc từng dòng

    [Fact]
    public async Task Import_BlankStatus_IsSkippedWithMessage()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(refund, null, "TX-1", images: [TestImages.Png()]))), "k.xlsx", dryRun: false);

        var row = Assert.Single(report.Rows);
        Assert.Equal("Skip", row.Action);
        Assert.Contains("Chưa điền trạng thái", row.Errors);
        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
    }

    [Fact]
    public async Task Import_UnrecognisedStatus_IsSkippedWithMessage()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(refund, "Chờ xử lý"))), "k.xlsx", dryRun: true);

        Assert.Equal("Skip", Assert.Single(report.Rows).Action);
        Assert.Equal(1, report.ErrorCount);
    }

    [Theory]
    [InlineData("Có")]
    [InlineData("co")]
    [InlineData("Yes")]
    [InlineData("X")]
    [InlineData("1")]
    public async Task Import_YesSynonyms_AllMeanComplete(string yes)
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(refund, yes, "TX-" + yes, images: [TestImages.Png()]))), "k.xlsx", dryRun: true);

        Assert.Equal("Complete", Assert.Single(report.Rows).Action);
    }

    [Theory]
    [InlineData("Không")]
    [InlineData("khong")]
    [InlineData("No")]
    [InlineData("0")]
    public async Task Import_NoSynonyms_AllMeanFail(string no)
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(refund, no, reason: "Lỗi"))), "k.xlsx", dryRun: true);

        Assert.Equal("Fail", Assert.Single(report.Rows).Action);
    }

    [Fact]
    public async Task Import_CompleteWithoutTransactionId_IsError()
    {
        var report = await RunOneAsync(refund => Spec(refund, "Có", null, images: [TestImages.Png()]));

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_CompleteWithoutImage_IsError()
    {
        var report = await RunOneAsync(refund => Spec(refund, "Có", "TX-1"));

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_MoreThanThreeImages_IsError()
    {
        var images = Enumerable.Range(0, 4).Select(i => TestImages.Png((byte)(i * 50), 1, 1)).ToArray();

        var report = await RunOneAsync(refund => Spec(refund, "Có", "TX-1", images: images));

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_ImageOverFiveMegabytes_IsError()
    {
        var report = await RunOneAsync(refund => Spec(refund, "Có", "TX-1", images: [TestImages.OversizedPng()]));

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_NonImagePlaceInCell_IsError()
    {
        var report = await RunOneAsync(refund => Spec(refund, "Có", "TX-1", placeInCell: TestImages.NotAnImage()));

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_PlaceInCellImage_IsAcceptedForCompletion()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(refund, "Có", "TX-1", placeInCell: TestImages.Png(9, 9, 9)))), "k.xlsx", dryRun: false);

        Assert.Equal(1, report.CompletedCount);
        Assert.Single(fixture.Proofs);
    }

    [Fact]
    public async Task Import_FailWithoutReason_IsError()
    {
        var report = await RunOneAsync(refund => Spec(refund, "Không"));

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_FailReasonOver500Characters_IsError()
    {
        var report = await RunOneAsync(refund => Spec(refund, "Không", reason: new string('a', 501)));

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_FailReasonOfExactly500Characters_IsAccepted()
    {
        var report = await RunOneAsync(refund => Spec(refund, "Không", reason: new string('a', 500)));

        Assert.Equal("Fail", Assert.Single(report.Rows).Action);
        Assert.Equal(0, report.ErrorCount);
    }

    [Fact]
    public async Task Import_DuplicateTransactionIdInsideFile_FlagsAtLeastOneRowAndCompletesAtMostOne()
    {
        // Giả định: BE đánh lỗi dòng thứ hai (hoặc cả hai); test chấp nhận cả hai cách nhưng không cho hoàn tất cả hai hồ sơ.
        var fixture = new Fixture();
        var first = fixture.Seed(RefundCaseStatuses.Approved);
        var second = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(new MemoryStream(Workbook(
            Spec(first, "Có", "TX-DUP", images: [TestImages.Png()]),
            Spec(second, "Có", "TX-DUP", images: [TestImages.Png()]))), "k.xlsx", dryRun: false);

        Assert.True(report.ErrorCount >= 1);
        Assert.True(report.CompletedCount <= 1);
        Assert.True(new[] { first, second }.Count(x => x.Status == RefundCaseStatuses.Completed) <= 1);
    }

    [Fact]
    public async Task Import_TransactionIdAlreadyUsedInDatabase_IsError()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.Completed).ProviderTransactionId = "TX-OLD";
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(refund, "Có", "TX-OLD", images: [TestImages.Png()]))), "k.xlsx", dryRun: false);

        Assert.Equal(1, report.ErrorCount);
        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Empty(fixture.Proofs);
    }

    [Theory]
    [InlineData(RefundCaseStatuses.Requested)]
    [InlineData(RefundCaseStatuses.Completed)]
    [InlineData(RefundCaseStatuses.Failed)]
    public async Task Import_CaseNotApproved_IsSkippedWithError(string status)
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(status);
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(refund, "Có", "TX-1", images: [TestImages.Png()]))), "k.xlsx", dryRun: false);

        var row = Assert.Single(report.Rows);
        Assert.Equal("Skip", row.Action);
        Assert.NotEmpty(row.Errors);
        Assert.Equal(status, refund.Status);
    }

    [Fact]
    public async Task Import_AdminsOwnRefund_IsError()
    {
        var fixture = new Fixture();
        var own = fixture.Seed(RefundCaseStatuses.Approved);
        own.CustomerUserId = fixture.Admin;
        fixture.ActAs(fixture.Admin);

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(own, "Có", "TX-1", images: [TestImages.Png()]))), "k.xlsx", dryRun: false);

        Assert.Equal(1, report.ErrorCount);
        Assert.Equal(RefundCaseStatuses.Approved, own.Status);
    }

    [Fact]
    public async Task Import_UnknownRefundCaseId_IsError()
    {
        var fixture = new Fixture();
        fixture.ActAs(fixture.Admin);
        var ghost = new RefundCase { RefundCaseId = Guid.NewGuid() };

        var report = await fixture.Service.ImportResultAsync(
            new MemoryStream(Workbook(Spec(ghost, "Có", "TX-1", images: [TestImages.Png()]))), "k.xlsx", dryRun: true);

        AssertInvalid(report);
    }

    [Fact]
    public async Task Import_EmptyRowsAreIgnored()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        var file = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = refund.RefundCaseId, Transferred = "Không", Reason = "x" }, new ResultRowSpec { Code = "" }],
            customize: sheet => { sheet.Cell(3, 1).Clear(); sheet.Cell(3, 5).Clear(); sheet.Cell(3, 9).Clear(); });

        var report = await fixture.Service.ImportResultAsync(new MemoryStream(file), "k.xlsx", dryRun: true);

        Assert.Equal(1, report.TotalRows);
    }

    [Fact]
    public async Task Import_FileThatIsNotXlsx_IsBadRequest()
    {
        var fixture = new Fixture();
        fixture.ActAs(fixture.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.Service.ImportResultAsync(new MemoryStream(TestImages.NotAnImage()), "ketqua.txt", dryRun: true));
    }

    [Fact]
    public async Task Import_MissingHeaders_IsBadRequest()
    {
        var fixture = new Fixture();
        fixture.ActAs(fixture.Admin);
        var file = ResultWorkbookFactory.Build([], includeHeader: h => h != "Mã giao dịch");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.Service.ImportResultAsync(new MemoryStream(file), "k.xlsx", dryRun: true));
    }

    // ------------------------------------------------------------------ helpers

    private static void AssertInvalid(dynamic report)
    {
        Assert.Equal(1, (int)report.ErrorCount);
        var rows = ((IEnumerable<object>)report.Rows).ToList();
        Assert.Single(rows);
        dynamic row = rows[0];
        Assert.Equal("Skip", (string)row.Action);
        Assert.NotEmpty((IEnumerable<string>)row.Errors);
    }

    private static async Task AssertRejectedAsync(Func<Task> action)
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(action);
        Assert.True(error is BadRequestException or ValidationException, $"Expected 400-type exception, got {error.GetType().Name}");
    }

    private static async Task<dynamic> RunOneAsync(Func<RefundCase, ResultRowSpec> spec)
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);
        return await fixture.Service.ImportResultAsync(new MemoryStream(Workbook(spec(refund))), "k.xlsx", dryRun: true);
    }

    private static ResultRowSpec Spec(RefundCase refund, string? transferred = null, string? transactionId = null, string? reason = null,
        IReadOnlyList<byte[]>? images = null, byte[]? placeInCell = null)
        => new()
        {
            RefundCaseId = refund.RefundCaseId,
            Code = $"HT-{refund.RefundCaseId:N}"[..11].ToUpperInvariant(),
            Transferred = transferred,
            TransactionId = transactionId,
            Reason = reason,
            FloatingImages = images ?? [],
            PlaceInCellImage = placeInCell
        };

    private static byte[] Workbook(params ResultRowSpec[] rows) => ResultWorkbookFactory.Build(rows);

    // ------------------------------------------------------------------ fixture

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakePrivateStorage
    {
        public List<string> Saved { get; } = [];
        public List<string> Deleted { get; } = [];
    }

    private sealed class Fixture
    {
        private Guid _actor;

        public Fixture()
        {
            Repository = TestProxy.Create<IRefundCaseRepository>(
                ("GetForUpdateAsync", args => Task.FromResult(Cases.SingleOrDefault(x => x.RefundCaseId == (Guid)args[0]!))),
                ("GetDetailAsync", args => Task.FromResult(Cases.SingleOrDefault(x => x.RefundCaseId == (Guid)args[0]!))),
                ("ProviderTransactionUsedAsync", args => Task.FromResult(Cases.Any(x => x.ProviderTransactionId == (string)args[0]!))),
                ("ReloadOrderAsync", _ => Task.CompletedTask),
                ("GetSucceededRefundTotalAsync", _ => Task.FromResult(0m)));

            UnitOfWork = TestProxy.Create<IUnitOfWork>(
                ("get_RefundCases", _ => Repository),
                ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                    ("GetSucceededPaymentTotalAsync", _ => Task.FromResult(Collected)))),
                ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", args =>
                {
                    AuditLogs.Add((AuditLog)args[0]!);
                    return Task.CompletedTask;
                }))),
                ("get_Notifications", _ => TestProxy.Create<INotificationRepository>(("AddAsync", _ => Task.CompletedTask))),
                ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                    ("AcquireOrderLockAsync", _ => Task.CompletedTask),
                    ("ProviderTransactionExistsAsync", _ => Task.FromResult(false)))),
                ("get_Accounts", _ => TestProxy.Create<IAccountRepository>(
                    ("GetByIdWithRoleAsync", _ => Task.FromResult<Account?>(new Account { Email = "admin@example.com", FullName = "Admin", EmailConfirmedAt = DateTime.UtcNow })))),
                // Giả định tên: IUnitOfWork.RefundProofs (IRefundProofRepository.AddAsync)
                ("get_RefundProofs", _ => TestProxy.Create<IRefundProofRepository>(
                    ("AddAsync", args =>
                    {
                        Proofs.Add((RefundProof)args[0]!);
                        return Task.CompletedTask;
                    }),
                    ("AddRangeAsync", args =>
                    {
                        Proofs.AddRange(((IEnumerable<RefundProof>)args[0]!));
                        return Task.CompletedTask;
                    }))),
                ("BeginTransactionAsync", _ => Task.CompletedTask),
                ("RollbackAsync", _ => Task.CompletedTask),
                ("CommitAsync", _ =>
                {
                    Commits++;
                    return Task.CompletedTask;
                }),
                ("SaveChangesAsync", _ => FailSaveChanges ? throw new InvalidOperationException("db down") : Task.FromResult(1)));

            Protector = new RefundDataProtector(Microsoft.Extensions.Options.Options.Create(new RefundSettings
            {
                DataEncryptionKey = Convert.ToBase64String(new byte[32])
            }));
            Service = CreateService();
        }

        public Guid Customer { get; } = Guid.NewGuid();
        public Guid Admin { get; } = Guid.NewGuid();
        public List<RefundCase> Cases { get; } = [];
        public List<RefundProof> Proofs { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public FakePrivateStorage Storage { get; } = new();
        public IRefundCaseRepository Repository { get; }
        public IUnitOfWork UnitOfWork { get; }
        public RefundDataProtector Protector { get; }
        public IRefundCaseService Service { get; }
        public decimal Collected { get; set; } = 2_000_000m;
        public bool FailSaveChanges { get; set; }
        public int Commits { get; private set; }

        public void ActAs(Guid userId) => _actor = userId;

        /// <summary>Điểm duy nhất cần chỉnh nếu chữ ký constructor RefundCaseService đổi (thêm IPrivateFileStorage ở cuối là giả định).</summary>
        private IRefundCaseService CreateService()
        {
            var currentUser = TestProxy.Create<ICurrentUserService>(
                ("get_RequiredUserId", _ => _actor),
                ("get_UserId", _ => (Guid?)_actor));
            var hasher = TestProxy.Create<IPasswordHasher>(("Hash", args => $"hash:{args[0]}"), ("Verify", args => (string)args[1]! == $"hash:{args[0]}"));
            var email = TestProxy.Create<IEmailSender>();
            var storage = TestProxy.Create<IPrivateFileStorage>(
                ("SaveAsync", args =>
                {
                    var path = $"refund-proofs/{Guid.NewGuid():N}";
                    Storage.Saved.Add(path);
                    return Task.FromResult(path);
                }),
                ("DeleteAsync", args =>
                {
                    Storage.Deleted.Add((string)args[0]!);
                    return Task.CompletedTask;
                }));
            return new RefundCaseService(
                UnitOfWork,
                currentUser,
                new FixedTime(),
                Protector,
                new RefundVerificationService(UnitOfWork, hasher, new FixedTime()),
                email,
                NullLogger<RefundCaseService>.Instance,
                TestProxy.Create<INotificationService>(),
                storage);
        }

        public ProofFileInput File(byte[] data, string name = "proof.png", string contentType = "image/png")
            => new(new MemoryStream(data), name, contentType, data.Length);

        public RefundCase Seed(string status)
        {
            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                CustomerUserId = Customer,
                OrderCode = "RT-001",
                OrderType = OrderTypes.TripBooking,
                PaymentStatus = OrderPaymentStatuses.Paid
            };
            var refund = new RefundCase
            {
                RefundCaseId = Guid.NewGuid(),
                OrderId = order.OrderId,
                Order = order,
                CustomerUserId = Customer,
                CustomerUser = new Account { FullName = "Nguyễn Văn A" },
                RefundPayment = new Payment { Status = PaymentStatuses.PendingApproval },
                Amount = 2_000_000m,
                Status = status,
                BankCode = "VCB",
                BankAccountLast4 = "6789",
                CreatedAt = Now,
                UpdatedAt = Now
            };
            Cases.Add(refund);
            return refund;
        }
    }
}
