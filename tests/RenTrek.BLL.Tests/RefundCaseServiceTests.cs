using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class RefundCaseServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Quote_UsesHighestRuleWithinHoursBeforeDeparture()
    {
        var fixture = new Fixture();
        var order = fixture.NewOrder();
        order.TripBookingOrder = new TripBookingOrder { Trip = new Trip { StartAt = Now.AddHours(80) } };
        fixture.Repository.Policy = new RefundPolicy
        {
            RefundPolicyRules =
            [
                new RefundPolicyRule { MinHoursBefore = 24, RefundPercentage = 50 },
                new RefundPolicyRule { MinHoursBefore = 72, RefundPercentage = 80 },
                new RefundPolicyRule { MinHoursBefore = 120, RefundPercentage = 100 }
            ]
        };
        fixture.Collected = 2_000_000m;

        var quote = await fixture.Service.QuoteAsync(RefundOrderInput.From(order), Now);

        Assert.Equal(1_600_000m, quote.RefundAmount);
        Assert.Equal(72, quote.Rule!.MinHoursBefore);
    }

    [Fact]
    public async Task OpenForCancellation_CreatesPendingApprovalRefund_WithEncryptedAccount()
    {
        var fixture = new Fixture();

        var order = fixture.NewOrder();
        order.TripBookingOrder = new TripBookingOrder { Trip = new Trip() };
        var summary = await fixture.Service.OpenForCancellationAsync(
            RefundOrderInput.From(order),
            new RefundQuoteResult { RefundAmount = 1_600_000m },
            new RefundBankAccountRequest { BankCode = "VCB", AccountNumber = "0123456789", AccountHolder = " nguyen   van a " },
            RefundBankCodes.ConsentVersion,
            Now);

        var payment = Assert.Single(fixture.Payments);
        Assert.Equal(PaymentStatuses.PendingApproval, payment.Status);
        Assert.Equal("Refund", payment.PaymentType);
        Assert.Equal("******6789", summary.MaskedAccount);

        var stored = Assert.Single(fixture.Repository.Cases);
        Assert.Equal(RefundCaseStatuses.Requested, stored.Status);
        Assert.DoesNotContain("0123456789", Encoding.UTF8.GetString(stored.PayoutDetailsCiphertext!));
        var details = fixture.Protector.Unprotect(
            new ProtectedRefundPayload(stored.PayoutDetailsCiphertext!, stored.PayoutDetailsNonce!, stored.PayoutDetailsTag!),
            stored.RefundCaseId,
            stored.OrderId);
        Assert.Equal("NGUYEN VAN A", details.AccountHolder);
        Assert.All(fixture.AuditLogs, log => Assert.DoesNotContain("0123456789", log.Details));
    }

    [Fact]
    public async Task Export_WithoutOtpIsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Requested);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.ExportAsync(new RefundExportRequest()));

        Assert.Equal(ErrorCodes.RefundVerificationRequired, error.ErrorCode);
        Assert.Equal(RefundCaseStatuses.Requested, refund.Status);
    }

    [Fact]
    public async Task Export_WrongCode_CommitsAttemptCount_ThenLocksAfterFiveAttempts()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.Requested);
        fixture.ActAs(fixture.Admin);
        var challenge = await fixture.Service.RequestExportCodeAsync();
        var wrong = new RefundExportRequest { ChallengeId = challenge.ChallengeId, VerificationCode = "000000x" };

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var commitsBefore = fixture.Commits;
            var error = await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.ExportAsync(wrong));
            Assert.Equal(ErrorCodes.RefundVerificationInvalid, error.ErrorCode);
            Assert.Equal(commitsBefore + 1, fixture.Commits);
        }

        var locked = await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.ExportAsync(
            new RefundExportRequest { ChallengeId = challenge.ChallengeId, VerificationCode = fixture.LastCode }));
        Assert.Equal(ErrorCodes.RefundVerificationExpired, locked.ErrorCode);
    }

    [Fact]
    public async Task Export_ExpiredCodeIsRejected()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.Requested);
        fixture.ActAs(fixture.Admin);
        var challenge = await fixture.Service.RequestExportCodeAsync();
        fixture.Time.Current = Now.AddMinutes(6);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.ExportAsync(
            new RefundExportRequest { ChallengeId = challenge.ChallengeId, VerificationCode = fixture.LastCode }));

        Assert.Equal(ErrorCodes.RefundVerificationExpired, error.ErrorCode);
    }

    [Fact]
    public async Task VerificationCode_LimitedToThreePerWindow()
    {
        var fixture = new Fixture();
        fixture.ActAs(fixture.Admin);
        for (var issued = 0; issued < 3; issued++)
            await fixture.Service.RequestExportCodeAsync();

        await Assert.ThrowsAsync<TooManyRequestsException>(() => fixture.Service.RequestExportCodeAsync());
    }

    [Fact]
    public async Task Export_WritesDecryptedAccounts_StampsExportedAt_AndCodeCannotBeReused()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        var own = fixture.Seed(RefundCaseStatuses.Approved);
        own.CustomerUserId = fixture.Admin;
        fixture.ActAs(fixture.Admin);
        var request = await fixture.NewExportRequestAsync();

        var file = await fixture.Service.ExportAsync(request);

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        var sheet = workbook.Worksheet(1);
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal("0123456789", sheet.Cell(2, 7).GetString());
        Assert.Equal("NGUYEN VAN A", sheet.Cell(2, 8).GetString());
        Assert.Equal(2_000_000d, sheet.Cell(2, 9).GetDouble());

        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Equal(Now, refund.ExportedAt);
        Assert.Null(own.ExportedAt);
        var audit = Assert.Single(fixture.AuditLogs, log => log.Action == "RefundExported");
        Assert.DoesNotContain("0123456789", audit.Details);

        var reuse = await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.ExportAsync(request));
        Assert.Equal(ErrorCodes.RefundVerificationInvalid, reuse.ErrorCode);
    }

    [Fact]
    public async Task Export_FormulaInHolderName_StaysPlainText()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.Approved, holder: "=HYPERLINK(\"http://evil\")");
        fixture.ActAs(fixture.Admin);

        var file = await fixture.Service.ExportAsync(await fixture.NewExportRequestAsync());

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        var cell = workbook.Worksheet(1).Cell(2, 8);
        Assert.False(cell.HasFormula);
        Assert.StartsWith("=HYPERLINK", cell.GetString());
    }

    [Fact]
    public async Task Export_IncludesAlreadyExportedCasesWithoutChangingThem()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        refund.ExportedAt = Now.AddDays(-1);
        fixture.ActAs(fixture.Admin);

        var file = await fixture.Service.ExportAsync(await fixture.NewExportRequestAsync());

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal("0123456789", workbook.Worksheet(1).Cell(2, 7).GetString());
        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Equal(Now.AddDays(-1), refund.ExportedAt);
        Assert.Equal(1, refund.RowVersion);
    }

    [Fact]
    public async Task Export_SkipsCasesNotYetApproved()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Requested);
        fixture.ActAs(fixture.Admin);

        var empty = await Assert.ThrowsAsync<ConflictException>(async () =>
            await fixture.Service.ExportAsync(await fixture.NewExportRequestAsync()));
        Assert.Equal(ErrorCodes.RefundExportEmpty, empty.ErrorCode);
        Assert.Equal(RefundCaseStatuses.Requested, refund.Status);
    }

    [Fact]
    public async Task Approve_RequestedCase_MovesToApprovedWithoutExport()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Requested);
        fixture.ActAs(fixture.Admin);

        await fixture.Service.ApproveAsync(refund.RefundCaseId, new RefundApproveRequest { ExpectedRowVersion = 1 });

        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Equal(fixture.Admin, refund.ApprovedByAdminUserId);
        Assert.Equal(Now, refund.ApprovedAt);
        Assert.Null(refund.ExportedAt);
        Assert.Equal(PaymentStatuses.Pending, refund.RefundPayment.Status);
        Assert.Single(fixture.AuditLogs, log => log.Action == "RefundCaseApproved");
    }

    [Theory]
    [InlineData(RefundCaseStatuses.AwaitingBankInfo)]
    [InlineData(RefundCaseStatuses.Approved)]
    [InlineData(RefundCaseStatuses.Rejected)]
    public async Task Approve_NonRequestedCase_IsRejected(string status)
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(status);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.ApproveAsync(refund.RefundCaseId, new RefundApproveRequest { ExpectedRowVersion = 1 }));
        Assert.Equal(ErrorCodes.RefundCaseNotApprovable, error.ErrorCode);
    }

    [Fact]
    public async Task Approve_OwnRefundIsForbidden()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Requested);
        refund.CustomerUserId = fixture.Admin;
        fixture.ActAs(fixture.Admin);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Service.ApproveAsync(refund.RefundCaseId, new RefundApproveRequest { ExpectedRowVersion = 1 }));
    }

    [Fact]
    public async Task Export_WithOnlyCasesAwaitingBankInfo_IsEmpty()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.AwaitingBankInfo);
        fixture.ActAs(fixture.Admin);

        var empty = await Assert.ThrowsAsync<ConflictException>(async () =>
            await fixture.Service.ExportAsync(await fixture.NewExportRequestAsync()));
        Assert.Equal(ErrorCodes.RefundExportEmpty, empty.ErrorCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnCasesWithActionFlags()
    {
        var fixture = new Fixture();
        var rejected = fixture.Seed(RefundCaseStatuses.Rejected);
        rejected.RejectionReason = "Chưa cung cấp STK";
        var awaiting = fixture.Seed(RefundCaseStatuses.AwaitingBankInfo);
        var other = fixture.Seed(RefundCaseStatuses.Requested);
        other.CustomerUserId = Guid.NewGuid();
        fixture.ActAs(fixture.Customer);

        var page = await fixture.Service.GetMineAsync(new RefundCaseQueryRequest());

        Assert.Equal(2, page.TotalItems);
        var rejectedItem = Assert.Single(page.Items, x => x.RefundCaseId == rejected.RefundCaseId);
        Assert.True(rejectedItem.CanResubmit);
        Assert.Equal("Chưa cung cấp STK", rejectedItem.RejectionReason);
        Assert.True(Assert.Single(page.Items, x => x.RefundCaseId == awaiting.RefundCaseId).CanSubmitBankInfo);
    }

    [Fact]
    public async Task Resubmit_RejectedCase_ReturnsToRequestedWithNewBankAccount()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Rejected);
        refund.RejectionReason = "Chưa cung cấp STK";
        refund.RejectedAt = Now.AddDays(-2);
        refund.RefundPayment.Status = PaymentStatuses.Cancelled;
        fixture.ActAs(fixture.Customer);

        await fixture.Service.ResubmitAsync(refund.RefundCaseId, new SubmitRefundBankInfoRequest
        {
            BankAccount = new RefundBankAccountRequest { BankCode = "TCB", AccountNumber = "1900123456", AccountHolder = "Nguyen Van B" },
            ConsentAccepted = true,
            ConsentVersion = RefundBankCodes.ConsentVersion,
            Reason = "  Tôi đã bổ sung STK đúng  "
        });

        Assert.Equal(RefundCaseStatuses.Requested, refund.Status);
        Assert.Null(refund.RejectionReason);
        Assert.Equal("TCB", refund.BankCode);
        Assert.Equal("3456", refund.BankAccountLast4);
        Assert.Equal(PaymentStatuses.PendingApproval, refund.RefundPayment.Status);
        Assert.Equal(1, refund.ResubmitCount);
        Assert.Equal("Tôi đã bổ sung STK đúng", refund.AppealReason);
    }

    [Fact]
    public async Task Resubmit_RejectedCaseWithoutReason_IsInvalid()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Rejected);
        refund.RejectedAt = Now;
        fixture.ActAs(fixture.Customer);

        var error = await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.ResubmitAsync(
            refund.RefundCaseId,
            new SubmitRefundBankInfoRequest { BankAccount = new RefundBankAccountRequest { BankCode = "TCB", AccountNumber = "1900123456", AccountHolder = "Nguyen Van B" },
            ConsentAccepted = true,
            ConsentVersion = RefundBankCodes.ConsentVersion }));
        Assert.Contains("reason", error.Errors.Keys);
        Assert.Equal(0, refund.ResubmitCount);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 8)]
    public async Task Resubmit_RejectedCaseAfterLimitOrDeadline_IsBlocked(int resubmitCount, int rejectedDaysAgo)
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Rejected);
        refund.ResubmitCount = resubmitCount;
        refund.RejectedAt = Now.AddDays(-rejectedDaysAgo);
        fixture.ActAs(fixture.Customer);

        var detail = await fixture.Service.GetAsync(refund.RefundCaseId);
        Assert.False(detail.CanResubmit);

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.ResubmitAsync(
            refund.RefundCaseId,
            new SubmitRefundBankInfoRequest { BankAccount = new RefundBankAccountRequest { BankCode = "TCB", AccountNumber = "1900123456", AccountHolder = "Nguyen Van B" },
            ConsentAccepted = true,
            ConsentVersion = RefundBankCodes.ConsentVersion, Reason = "Khiếu nại" }));
        Assert.Equal(ErrorCodes.RefundCaseNotResubmittable, error.ErrorCode);
    }

    [Fact]
    public async Task Resubmit_LegacyFailedCase_DoesNotConsumeAppeal()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Failed);
        fixture.ActAs(fixture.Customer);

        await fixture.Service.ResubmitAsync(refund.RefundCaseId, new SubmitRefundBankInfoRequest { BankAccount = new RefundBankAccountRequest { BankCode = "TCB", AccountNumber = "1900123456", AccountHolder = "Nguyen Van B" },
            ConsentAccepted = true,
            ConsentVersion = RefundBankCodes.ConsentVersion });

        Assert.Equal(RefundCaseStatuses.Requested, refund.Status);
        Assert.Equal(0, refund.ResubmitCount);
    }

    [Fact]
    public async Task Get_RejectedCase_ExposesResubmitDeadline()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Rejected);
        refund.RejectedAt = Now.AddDays(-1);
        fixture.ActAs(fixture.Customer);

        var detail = await fixture.Service.GetAsync(refund.RefundCaseId);

        Assert.True(detail.CanResubmit);
        Assert.Equal(Now.AddDays(6), detail.ResubmitDeadline);
    }

    [Theory]
    [InlineData(RefundCaseStatuses.Requested)]
    [InlineData(RefundCaseStatuses.Completed)]
    public async Task Resubmit_NonClosedOrCompletedCase_IsRejected(string status)
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(status);
        fixture.ActAs(fixture.Customer);

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.ResubmitAsync(refund.RefundCaseId,
            new SubmitRefundBankInfoRequest
            {
                BankAccount = new RefundBankAccountRequest { BankCode = "TCB", AccountNumber = "1900123456", AccountHolder = "Nguyen Van B" },
                ConsentAccepted = true,
                ConsentVersion = RefundBankCodes.ConsentVersion
            }));
        Assert.Equal(ErrorCodes.RefundCaseNotResubmittable, error.ErrorCode);
    }

    [Fact]
    public async Task Resubmit_OtherCustomerSeesNotFound()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Rejected);
        fixture.ActAs(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Service.ResubmitAsync(refund.RefundCaseId,
            new SubmitRefundBankInfoRequest { ConsentAccepted = true, ConsentVersion = RefundBankCodes.ConsentVersion }));
    }

    [Fact]
    public async Task Get_OtherCustomerSeesNotFound()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Service.GetAsync(refund.RefundCaseId));
    }

    [Theory]
    [InlineData(2_000_000, OrderPaymentStatuses.Refunded)]
    [InlineData(3_000_000, OrderPaymentStatuses.PartiallyRefunded)]
    public async Task Complete_UpdatesPaymentAndOrderStatus(int collected, string expectedStatus)
    {
        var fixture = new Fixture { Collected = collected };
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var result = await fixture.Service.CompleteAsync(refund.RefundCaseId, new CompleteRefundRequest
        {
            ProviderTransactionId = $"BANK-{collected}",
            ExpectedRowVersion = 1
        }, ProofStorage.Files());

        Assert.Equal(RefundCaseStatuses.Completed, result.Status);
        Assert.Equal(PaymentStatuses.Succeeded, refund.RefundPayment.Status);
        Assert.Equal(expectedStatus, refund.Order.PaymentStatus);
        Assert.True(fixture.PaymentOrderLockAcquired);
        Assert.True(fixture.Repository.OrderReloaded);
    }

    [Fact]
    public async Task Complete_OrderNeedsReconciliationIsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        refund.Order.PaymentStatus = OrderPaymentStatuses.NeedsReconciliation;
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-RECON", ExpectedRowVersion = 1 }, ProofStorage.Files()));

        Assert.Equal(ErrorCodes.OrderNotReconcilable, error.ErrorCode);
        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
    }

    [Fact]
    public async Task Complete_BeforeExportIsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Requested);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.CompleteAsync(refund.RefundCaseId, new CompleteRefundRequest { ExpectedRowVersion = 1 }, ProofStorage.Files()));

        Assert.Equal(ErrorCodes.RefundCaseNotCompletable, error.ErrorCode);
        Assert.Equal(OrderPaymentStatuses.Paid, refund.Order.PaymentStatus);
    }

    [Fact]
    public async Task Complete_WithoutTransactionIdIsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ExpectedRowVersion = 1 }, ProofStorage.Files()));
    }

    [Fact]
    public async Task Complete_StaleRowVersionIsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.CompleteAsync(refund.RefundCaseId, new CompleteRefundRequest { ExpectedRowVersion = 9 }, ProofStorage.Files()));

        Assert.Equal(ErrorCodes.ConcurrencyConflict, error.ErrorCode);
    }

    [Fact]
    public async Task Complete_DuplicateTransactionIsRejected()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.Completed).ProviderTransactionId = "BANK-1";
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CompleteAsync(
            refund.RefundCaseId,
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 }, ProofStorage.Files()));

        Assert.Equal(ErrorCodes.RefundTransactionDuplicated, error.ErrorCode);
    }

    [Fact]
    public async Task Complete_OwnRefundIsForbidden()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Customer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Service.CompleteAsync(refund.RefundCaseId, new CompleteRefundRequest { ExpectedRowVersion = 1 }, ProofStorage.Files()));
    }

    [Fact]
    public async Task Reject_AfterExportIsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.RejectAsync(
            refund.RefundCaseId,
            new RefundDecisionRequest { Reason = "Sai thông tin", ExpectedRowVersion = 1 }));

        Assert.Equal(ErrorCodes.RefundCaseNotRejectable, error.ErrorCode);
    }

    [Fact]
    public async Task Purge_RemovesPayloadButKeepsAuditFields()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Completed);
        refund.CompletedAt = Now.AddDays(-31);
        var recent = fixture.Seed(RefundCaseStatuses.Completed);
        recent.CompletedAt = Now.AddDays(-29);

        await fixture.Service.PurgeSensitiveDataAsync();

        Assert.Null(refund.PayoutDetailsCiphertext);
        Assert.Null(refund.PayoutDetailsNonce);
        Assert.Null(refund.PayoutDetailsTag);
        Assert.NotNull(refund.SensitiveDataPurgedAt);
        Assert.Equal("6789", refund.BankAccountLast4);
        Assert.NotNull(recent.PayoutDetailsCiphertext);
    }

    [Fact]
    public void Protector_PayloadCopiedToAnotherCaseFailsToDecrypt()
    {
        var protector = new Fixture().Protector;
        var orderId = Guid.NewGuid();
        var payload = protector.Protect(new RefundPayoutDetails("0123456789", "A"), Guid.NewGuid(), orderId);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(payload, Guid.NewGuid(), orderId));
    }

    [Fact]
    public void EfModel_MapsRefundTablesWithConcurrencyToken()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").Options);

        var refundCase = context.Model.FindEntityType(typeof(RefundCase))!;
        Assert.Equal("refund_cases", refundCase.GetTableName());
        Assert.True(refundCase.FindProperty(nameof(RefundCase.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("refund_action_challenges", context.Model.FindEntityType(typeof(RefundActionChallenge))!.GetTableName());
    }

    [Fact]
    public async Task Quote_AfterPartialCancel_ScalesPaidToActiveSeats()
    {
        var fixture = new Fixture();
        var order = fixture.NewOrder();
        order.TripBookingOrder = new TripBookingOrder
        {
            Trip = new Trip { StartAt = Now.AddHours(200) },
            Participants =
            [
                new BookingParticipant { Status = BookingParticipantStatuses.Pending },
                new BookingParticipant { Status = BookingParticipantStatuses.Cancelled },
                new BookingParticipant { Status = BookingParticipantStatuses.Pending },
                new BookingParticipant { Status = BookingParticipantStatuses.Pending }
            ]
        };
        fixture.Repository.Policy = new RefundPolicy { RefundPolicyRules = [new RefundPolicyRule { MinHoursBefore = 120, RefundPercentage = 100 }] };
        fixture.Collected = 2_000_000m;

        var quote = await fixture.Service.QuoteAsync(RefundOrderInput.From(order), Now);

        Assert.Equal(1_500_000m, quote.PaidAmount);
        Assert.Equal(1_500_000m, quote.RefundAmount);
    }

    private static CancelTripParticipantsRequest PartialRequest(decimal expected, params Guid[] ids) => new()
    {
        ParticipantIds = [.. ids],
        ExpectedRefundAmount = expected,
        BankAccount = new RefundBankAccountRequest { BankCode = "VCB", AccountNumber = "0123456789", AccountHolder = "A B" },
        ConsentAccepted = true,
        ConsentVersion = RefundBankCodes.ConsentVersion
    };

    [Fact]
    public async Task PartialCancel_PreviewAndCancel_RefundsShareOfSeats()
    {
        var fixture = new BookingFixture(refundAmount: 1_600_000m, rulePercentage: 80);
        var people = fixture.SeedParticipants();

        var preview = await fixture.Service.GetParticipantCancellationPreviewAsync(fixture.Order.OrderId, [people[1].ParticipantId]);
        Assert.True(preview.CanCancel);
        Assert.Equal(500_000m, preview.PaidAmount);
        Assert.Equal(400_000m, preview.EstimatedRefundAmount);

        var result = await fixture.Service.CancelParticipantsAsync(
            fixture.Order.OrderId, PartialRequest(400_000m, people[1].ParticipantId));

        Assert.Equal(OrderStatuses.Confirmed, result.Status);
        Assert.Equal(BookingParticipantStatuses.Cancelled, people[1].Status);
        Assert.Equal(3, fixture.Order.TripBookingOrder.ParticipantCount);
        Assert.Equal(RefundCaseSources.TripPartialCancellation, fixture.OpenedSource);
        Assert.Equal(400_000m, fixture.OpenedAmount);
    }

    [Fact]
    public async Task PartialCancel_BlocksOwner_LastSeat_AndOpenRefund()
    {
        var fixture = new BookingFixture(refundAmount: 1_600_000m, rulePercentage: 80);
        var people = fixture.SeedParticipants();

        var owner = await fixture.Service.GetParticipantCancellationPreviewAsync(fixture.Order.OrderId, [people[0].ParticipantId]);
        Assert.False(owner.CanCancel);

        people[0].LinkedUserId = null;
        var all = await fixture.Service.GetParticipantCancellationPreviewAsync(
            fixture.Order.OrderId, people.Select(p => p.ParticipantId).ToList());
        Assert.False(all.CanCancel);

        fixture.OpenCase = new RefundCaseSummaryResponse { Status = RefundCaseStatuses.Requested };
        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CancelParticipantsAsync(
            fixture.Order.OrderId, PartialRequest(400_000m, people[1].ParticipantId)));
        Assert.Equal(ErrorCodes.OrderNotCancellable, error.ErrorCode);
        Assert.Equal(BookingParticipantStatuses.Pending, people[1].Status);
        Assert.False(fixture.CaseOpened);
    }

    [Fact]
    public async Task Cancel_RefundAmountChanged_Returns409WithoutCancelling()
    {
        var fixture = new BookingFixture(refundAmount: 1_600_000m);

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CancelAsync(
            fixture.Order.OrderId,
            new CancelTripBookingRequest { ExpectedRefundAmount = 2_000_000m }));

        Assert.Equal(ErrorCodes.RefundAmountChanged, error.ErrorCode);
        Assert.Equal(OrderStatuses.Confirmed, fixture.Order.Status);
    }

    [Fact]
    public async Task Cancel_MissingConsent_IsRejected()
    {
        var fixture = new BookingFixture(refundAmount: 1_600_000m);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CancelAsync(
            fixture.Order.OrderId,
            new CancelTripBookingRequest
            {
                ExpectedRefundAmount = 1_600_000m,
                BankAccount = new RefundBankAccountRequest { BankCode = "VCB", AccountNumber = "0123456789", AccountHolder = "A B" }
            }));

        Assert.Equal(OrderStatuses.Confirmed, fixture.Order.Status);
    }

    [Fact]
    public async Task Cancel_PaidBooking_KeepsPaidUntilRefundSucceeds()
    {
        var fixture = new BookingFixture(refundAmount: 1_600_000m);

        var result = await fixture.Service.CancelAsync(
            fixture.Order.OrderId,
            new CancelTripBookingRequest
            {
                ExpectedRefundAmount = 1_600_000m,
                BankAccount = new RefundBankAccountRequest { BankCode = "VCB", AccountNumber = "0123456789", AccountHolder = "A B" },
                ConsentAccepted = true,
                ConsentVersion = RefundBankCodes.ConsentVersion
            });

        Assert.Equal(OrderStatuses.Cancelled, result.Status);
        Assert.Equal(OrderPaymentStatuses.Paid, result.PaymentStatus);
        Assert.True(fixture.CaseOpened);
        Assert.NotNull(result.RefundCase);
    }

    [Fact]
    public async Task Cancel_UnpaidBooking_OpensNoRefundAndNeedsNoBankInfo()
    {
        var fixture = new BookingFixture(refundAmount: 0m);
        fixture.Order.Status = OrderStatuses.PendingPayment;
        fixture.Order.PaymentStatus = OrderPaymentStatuses.Unpaid;

        // Không gửi STK / consent: đơn chưa thanh toán thì không cần
        var result = await fixture.Service.CancelAsync(fixture.Order.OrderId, new CancelTripBookingRequest { Reason = "Đổi kế hoạch" });

        Assert.Equal(OrderStatuses.Cancelled, result.Status);
        Assert.False(fixture.CaseOpened);
        Assert.Null(result.RefundCase);
    }

    [Fact]
    public async Task Preview_ReportsWhetherOrderIsPaid()
    {
        var paid = new BookingFixture(refundAmount: 1_600_000m);
        Assert.True((await paid.Service.GetCancellationPreviewAsync(paid.Order.OrderId)).IsPaid);

        var unpaid = new BookingFixture(refundAmount: 0m);
        unpaid.Order.Status = OrderStatuses.PendingPayment;
        unpaid.Order.PaymentStatus = OrderPaymentStatuses.Unpaid;
        Assert.False((await unpaid.Service.GetCancellationPreviewAsync(unpaid.Order.OrderId)).IsPaid);
    }

    [Fact]
    public async Task Cancel_UnsupportedPaidStateIsRejected()
    {
        var fixture = new BookingFixture(refundAmount: 1_600_000m);
        fixture.Order.Status = OrderStatuses.InFulfillment;

        var error = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CancelAsync(
            fixture.Order.OrderId,
            new CancelTripBookingRequest { ExpectedRefundAmount = 1_600_000m }));

        Assert.Equal(ErrorCodes.OrderNotCancellable, error.ErrorCode);
        Assert.False(fixture.CaseOpened);
    }

    [Fact]
    public async Task Cancel_UnpaidBooking_CancelsPendingCollection()
    {
        var fixture = new BookingFixture(refundAmount: 0m);
        fixture.Order.Status = OrderStatuses.PendingPayment;
        fixture.Order.PaymentStatus = OrderPaymentStatuses.Unpaid;
        fixture.PendingCollection = new Payment { Status = PaymentStatuses.Pending };

        var result = await fixture.Service.CancelAsync(fixture.Order.OrderId);

        Assert.Equal(OrderStatuses.Cancelled, result.Status);
        Assert.Equal(PaymentStatuses.Cancelled, fixture.PendingCollection.Status);
        Assert.Equal(Now, fixture.PendingCollection.ProcessedAt);
        Assert.False(fixture.CaseOpened);
    }

    private sealed class FixedTime : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class Fixture
    {
        private Guid _actor;

        public Fixture()
        {
            Repository = new FakeRefundCaseRepository();
            Protector = new RefundDataProtector(Microsoft.Extensions.Options.Options.Create(new RefundSettings
            {
                DataEncryptionKey = Convert.ToBase64String(new byte[32])
            }));

            var unitOfWork = TestProxy.Create<IUnitOfWork>(
                ("get_RefundCases", _ => Repository),
                ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                    ("GetSucceededPaymentTotalAsync", _ => Task.FromResult(Collected)))),
                ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", args =>
                {
                    AuditLogs.Add((AuditLog)args[0]!);
                    return Task.CompletedTask;
                }
                ))),
                ("get_Notifications", _ => TestProxy.Create<INotificationRepository>(("AddAsync", _ => Task.CompletedTask))),
                ("get_RefundProofs", _ => TestProxy.Create<IRefundProofRepository>(("AddRangeAsync", _ => Task.CompletedTask))),
                ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                    ("AddAsync", args =>
                    {
                        Payments.Add((Payment)args[0]!);
                        return Task.CompletedTask;
                    }
                ),
                    ("AcquireOrderLockAsync", _ =>
                    {
                        PaymentOrderLockAcquired = true;
                        return Task.CompletedTask;
                    }
                ),
                    ("ProviderTransactionExistsAsync", _ => Task.FromResult(false)))),
                ("get_Accounts", _ => TestProxy.Create<IAccountRepository>(
                    ("GetByIdWithRoleAsync", _ => Task.FromResult<Account?>(new Account
                    {
                        Email = "admin@example.com",
                        FullName = "Admin",
                        EmailConfirmedAt = DateTime.UtcNow
                    })))),
                ("BeginTransactionAsync", _ => Task.CompletedTask),
                ("RollbackAsync", _ => Task.CompletedTask),
                ("CommitAsync", _ =>
                {
                    Commits++;
                    return Task.CompletedTask;
                }
            ),
                ("SaveChangesAsync", _ => Task.FromResult(1)));

            var currentUser = TestProxy.Create<ICurrentUserService>(
                ("get_RequiredUserId", _ => _actor),
                ("get_UserId", _ => (Guid?)_actor));
            var hasher = TestProxy.Create<IPasswordHasher>(
                ("Hash", args => $"hash:{args[0]}"),
                ("Verify", args => (string)args[1]! == $"hash:{args[0]}"));
            var email = TestProxy.Create<IEmailSender>(("SendAsync", args =>
            {
                LastCode = Regex.Match((string)args[3]!, @"\d{6}").Value;
                return Task.CompletedTask;
            }
            ));

            Service = new RefundCaseService(
                unitOfWork,
                currentUser,
                Time,
                Protector,
                new RefundVerificationService(unitOfWork, hasher, Time),
                email,
                NullLogger<RefundCaseService>.Instance,
                TestProxy.Create<INotificationService>(),
                privateStorage: ProofStorage.Create());
        }

        public Guid Customer { get; } = Guid.NewGuid();
        public Guid Admin { get; } = Guid.NewGuid();
        public FixedTime Time { get; } = new();
        public FakeRefundCaseRepository Repository { get; }
        public RefundDataProtector Protector { get; }
        public RefundCaseService Service { get; }
        public List<AuditLog> AuditLogs { get; } = [];
        public List<Payment> Payments { get; } = [];
        public decimal Collected { get; set; } = 2_000_000m;
        public bool PaymentOrderLockAcquired { get; private set; }
        public int Commits { get; private set; }
        public string LastCode { get; private set; } = string.Empty;

        public void ActAs(Guid userId) => _actor = userId;

        public async Task<RefundExportRequest> NewExportRequestAsync()
        {
            var challenge = await Service.RequestExportCodeAsync();
            return new RefundExportRequest { ChallengeId = challenge.ChallengeId, VerificationCode = LastCode };
        }

        public Order NewOrder() => new()
        {
            OrderId = Guid.NewGuid(),
            CustomerUserId = Customer,
            OrderCode = "RT-001",
            OrderType = OrderTypes.TripBooking,
            PaymentStatus = OrderPaymentStatuses.Paid
        };

        public RefundCase Seed(string status, string holder = "NGUYEN VAN A")
        {
            var order = NewOrder();
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
            var payload = Protector.Protect(new RefundPayoutDetails("0123456789", holder), refund.RefundCaseId, order.OrderId);
            refund.PayoutDetailsCiphertext = payload.Ciphertext;
            refund.PayoutDetailsNonce = payload.Nonce;
            refund.PayoutDetailsTag = payload.Tag;
            Repository.Cases.Add(refund);
            return refund;
        }
    }

    private sealed class BookingFixture
    {
        public BookingFixture(decimal refundAmount, decimal? rulePercentage = null)
        {
            var userId = Guid.NewGuid();
            Order = new Order
            {
                OrderId = Guid.NewGuid(),
                CustomerUserId = userId,
                Status = OrderStatuses.Confirmed,
                PaymentStatus = OrderPaymentStatuses.Paid,
                TripBookingOrder = new TripBookingOrder { Trip = new Trip { StartAt = Now.AddDays(10) } }
            };

            var unitOfWork = TestProxy.Create<IUnitOfWork>(
                ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                    ("GetOwnedBookingAsync", _ => Task.FromResult<Order?>(Order)))),
                ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                    ("AcquireOrderLockAsync", _ => Task.CompletedTask),
                    ("GetLatestPendingCollectionAsync", _ => Task.FromResult(PendingCollection)))),
                ("get_TripGroups", _ => TestProxy.Create<ITripGroupRepository>()),
                ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>()),
                ("SaveChangesAsync", _ => Task.FromResult(1)));
            var refundCases = TestProxy.Create<IRefundCaseService>(
                ("QuoteAsync", _ => Task.FromResult(new RefundQuoteResult
                {
                    PaidAmount = 2_000_000m,
                    RefundAmount = refundAmount,
                    Rule = rulePercentage is null ? null : new RefundPolicyRule { RefundPercentage = rulePercentage.Value }
                })),
                ("GetSummaryByOrderAsync", _ => Task.FromResult(OpenCase)),
                ("OpenForCancellationAsync", args =>
                {
                    CaseOpened = true;
                    OpenedSource = (string)args[^1]!;
                    OpenedAmount = ((RefundQuoteResult)args[1]!).RefundAmount;
                    return Task.FromResult(new RefundCaseSummaryResponse());
                }
            ));

            Service = new TripBookingService(
                unitOfWork,
                TestProxy.Create<ICurrentUserService>(("get_RequiredUserId", _ => userId), ("get_Role", _ => RoleNames.Customer)),
                new FixedTime(),
                TestProxy.Create<IInviteTokenHelper>(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings()),
                TestProxy.Create<INotificationService>(),
                refundCases);
        }

        public Order Order { get; }
        public Payment? PendingCollection { get; set; }
        public TripBookingService Service { get; }
        public bool CaseOpened { get; private set; }
        public string? OpenedSource { get; private set; }
        public decimal OpenedAmount { get; private set; }
        public RefundCaseSummaryResponse? OpenCase { get; set; }

        /// <summary>4 ghế, ghế đầu là người đặt.</summary>
        public List<BookingParticipant> SeedParticipants()
        {
            var list = Enumerable.Range(0, 4).Select(i => new BookingParticipant
            {
                ParticipantId = Guid.NewGuid(),
                LinkedUserId = i == 0 ? Order.CustomerUserId : null,
                Status = BookingParticipantStatuses.Pending
            }).ToList();
            Order.TripBookingOrder.Participants = list;
            Order.TripBookingOrder.ParticipantCount = 4;
            return list;
        }
    }

    private sealed class FakeRefundCaseRepository : IRefundCaseRepository
    {
        public List<RefundCase> Cases { get; } = [];
        public List<RefundActionChallenge> Challenges { get; } = [];
        public RefundPolicy? Policy { get; set; }

        private RefundCase? Find(Guid id) => Cases.SingleOrDefault(x => x.RefundCaseId == id);

        public Task AcquireChallengeLockAsync(Guid adminUserId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public bool OrderReloaded { get; private set; }

        public Task ReloadOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            OrderReloaded = true;
            return Task.CompletedTask;
        }

        public Task AddAsync(RefundCase refundCase, CancellationToken cancellationToken = default)
        {
            Cases.Add(refundCase);
            return Task.CompletedTask;
        }

        public Task<RefundCase?> GetDetailAsync(Guid refundCaseId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(refundCaseId));

        public Task<RefundCase?> GetForUpdateAsync(Guid refundCaseId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(refundCaseId));

        public Task<RefundCase?> GetLatestByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cases.LastOrDefault(x => x.OrderId == orderId));

        public Task<(IReadOnlyList<RefundCase> Items, int TotalItems)> SearchByCustomerAsync(
            Guid customerUserId,
            IReadOnlyCollection<string> statuses,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RefundCase> items = Cases
                .Where(x => x.CustomerUserId == customerUserId && (statuses.Count == 0 || statuses.Contains(x.Status)))
                .ToList();
            return Task.FromResult((items, items.Count));
        }

        public Task<bool> HasOtherOpenCaseAsync(Guid orderId, Guid excludedRefundCaseId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cases.Any(x => x.OrderId == orderId && x.RefundCaseId != excludedRefundCaseId
                && x.Status is not (RefundCaseStatuses.Completed or RefundCaseStatuses.Rejected or RefundCaseStatuses.Failed or RefundCaseStatuses.Cancelled)));

        public Task<(IReadOnlyList<RefundCase> Items, int TotalItems)> SearchAsync(
            IReadOnlyCollection<string> statuses,
            DateTimeOffset? fromDate,
            DateTimeOffset? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RefundCase> items = Cases.Where(x => statuses.Contains(x.Status)).ToList();
            return Task.FromResult((items, items.Count));
        }

        public Task<IReadOnlyList<RefundCase>> GetForExportAsync(
            IReadOnlyCollection<string> statuses,
            Guid excludedCustomerUserId,
            DateTimeOffset? fromDate,
            DateTimeOffset? toDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RefundCase>>(
                Cases.Where(x => statuses.Contains(x.Status) && x.CustomerUserId != excludedCustomerUserId).ToList());

        public Task<IReadOnlyList<Guid>> FindApprovedIdsByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<bool> ProviderTransactionUsedAsync(string providerTransactionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cases.Any(x => x.ProviderTransactionId == providerTransactionId));

        public Task<IReadOnlyList<RefundCase>> GetAwaitingBankInfoAsync(DateTimeOffset requestedBefore, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RefundCase>>([]);

        public Task<IReadOnlyList<Guid>> GetPurgeableIdsAsync(DateTimeOffset closedBefore, CancellationToken cancellationToken = default)
            => Ids(x => x.PayoutDetailsCiphertext is not null && x.Status == RefundCaseStatuses.Completed && x.CompletedAt <= closedBefore);

        private Task<IReadOnlyList<Guid>> Ids(Func<RefundCase, bool> predicate)
            => Task.FromResult<IReadOnlyList<Guid>>(Cases.Where(predicate).Select(x => x.RefundCaseId).ToList());

        public Task<RefundPolicy?> GetPolicyWithRulesAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
            => Task.FromResult(Policy);

        public Task<Guid?> GetLatestCollectionPaymentIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(null);

        public Task<decimal> GetSucceededRefundTotalAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task<IReadOnlyList<Guid>> GetActiveUserIdsByRolesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task AddChallengeAsync(RefundActionChallenge challenge, CancellationToken cancellationToken = default)
        {
            Challenges.Add(challenge);
            return Task.CompletedTask;
        }

        public Task<RefundActionChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Challenges.SingleOrDefault(x => x.ChallengeId == challengeId));

        public Task<IReadOnlyList<RefundActionChallenge>> GetOpenChallengesAsync(Guid adminUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RefundActionChallenge>>(
                Challenges.Where(x => x.AdminUserId == adminUserId && x.UsedAt is null).ToList());

        public Task<int> CountChallengesSinceAsync(Guid adminUserId, DateTimeOffset since, CancellationToken cancellationToken = default)
            => Task.FromResult(Challenges.Count(x => x.AdminUserId == adminUserId && x.CreatedAt >= since));
    }
}

/// <summary>
/// Giả lập interface lớn bằng vài handler theo tên method; method trả Task mặc định hoàn thành, method khác chưa khai báo thì ném lỗi.
/// </summary>
public class TestProxy : DispatchProxy
{
    private Dictionary<string, Func<object?[], object?>> _handlers = [];

    public static T Create<T>(params (string Name, Func<object?[], object?> Handler)[] handlers) where T : class
    {
        var proxy = Create<T, TestProxy>();
        ((TestProxy)(object)proxy)._handlers = handlers.GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Handler);
        return proxy;
    }

    /// <summary>Dịch vụ hoàn tiền cho test không quan tâm tới hoàn tiền: mọi booking được hoàn 0.</summary>
    public static IRefundCaseService NoRefunds()
        => Create<IRefundCaseService>(
            ("QuoteAsync", _ => Task.FromResult(new RefundQuoteResult())),
            ("GetSummaryByOrderAsync", _ => Task.FromResult<RefundCaseSummaryResponse?>(null)));

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_handlers.TryGetValue(targetMethod!.Name, out var handler))
            return handler(args ?? []);
        if (targetMethod.ReturnType == typeof(Task))
            return Task.CompletedTask;
        throw new NotSupportedException(targetMethod.Name);
    }
}

/// <summary>Storage chứng từ giả và file PNG hợp lệ cho các test hoàn tất hồ sơ.</summary>
internal static class ProofStorage
{
    public static IPrivateFileStorage Create() => TestProxy.Create<IPrivateFileStorage>(
        ("SaveAsync", _ => Task.FromResult($"test/{Guid.NewGuid():N}.png")),
        ("DeleteAsync", _ => Task.CompletedTask));

    public static IReadOnlyList<ProofFileInput> Files()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        return [new ProofFileInput(new MemoryStream(png), "proof.png", "image/png", png.Length)];
    }
}
