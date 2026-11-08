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
    public async Task Export_WritesDecryptedAccounts_MovesCasesToApproved_AndCodeCannotBeReused()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Requested);
        var own = fixture.Seed(RefundCaseStatuses.Requested);
        own.CustomerUserId = fixture.Admin;
        fixture.ActAs(fixture.Admin);
        var request = await fixture.NewExportRequestAsync();

        var file = await fixture.Service.ExportAsync(request);

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        var sheet = workbook.Worksheet(1);
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal("0123456789", sheet.Cell(2, 5).GetString());
        Assert.Equal("NGUYEN VAN A", sheet.Cell(2, 6).GetString());
        Assert.Equal(2_000_000d, sheet.Cell(2, 7).GetDouble());

        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Equal(fixture.Admin, refund.ApprovedByAdminUserId);
        Assert.Equal(PaymentStatuses.Pending, refund.RefundPayment.Status);
        Assert.Equal(RefundCaseStatuses.Requested, own.Status);
        var audit = Assert.Single(fixture.AuditLogs, log => log.Action == "RefundExported");
        Assert.DoesNotContain("0123456789", audit.Details);

        var reuse = await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.ExportAsync(request));
        Assert.Equal(ErrorCodes.RefundVerificationInvalid, reuse.ErrorCode);
    }

    [Fact]
    public async Task Export_FormulaInHolderName_StaysPlainText()
    {
        var fixture = new Fixture();
        fixture.Seed(RefundCaseStatuses.Requested, holder: "=HYPERLINK(\"http://evil\")");
        fixture.ActAs(fixture.Admin);

        var file = await fixture.Service.ExportAsync(await fixture.NewExportRequestAsync());

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        var cell = workbook.Worksheet(1).Cell(2, 6);
        Assert.False(cell.HasFormula);
        Assert.StartsWith("=HYPERLINK", cell.GetString());
    }

    [Fact]
    public async Task Export_DoesNotRepeatExportedCases_ButReExportDoes()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var empty = await Assert.ThrowsAsync<ConflictException>(async () =>
            await fixture.Service.ExportAsync(await fixture.NewExportRequestAsync()));
        Assert.Equal(ErrorCodes.RefundExportEmpty, empty.ErrorCode);

        var request = await fixture.NewExportRequestAsync();
        request.ReExport = true;
        var file = await fixture.Service.ExportAsync(request);

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal("0123456789", workbook.Worksheet(1).Cell(2, 5).GetString());
        Assert.Equal(RefundCaseStatuses.Approved, refund.Status);
        Assert.Equal(1, refund.RowVersion);
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
        });

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
            new CompleteRefundRequest { ProviderTransactionId = "BANK-RECON", ExpectedRowVersion = 1 }));

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
            fixture.Service.CompleteAsync(refund.RefundCaseId, new CompleteRefundRequest { ExpectedRowVersion = 1 }));

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
            new CompleteRefundRequest { ExpectedRowVersion = 1 }));
    }

    [Fact]
    public async Task Complete_StaleRowVersionIsRejected()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Admin);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.CompleteAsync(refund.RefundCaseId, new CompleteRefundRequest { ExpectedRowVersion = 9 }));

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
            new CompleteRefundRequest { ProviderTransactionId = "BANK-1", ExpectedRowVersion = 1 }));

        Assert.Equal(ErrorCodes.RefundTransactionDuplicated, error.ErrorCode);
    }

    [Fact]
    public async Task Complete_OwnRefundIsForbidden()
    {
        var fixture = new Fixture();
        var refund = fixture.Seed(RefundCaseStatuses.Approved);
        fixture.ActAs(fixture.Customer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Service.CompleteAsync(refund.RefundCaseId, new CompleteRefundRequest { ExpectedRowVersion = 1 }));
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
                TestProxy.Create<INotificationService>());
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
        public BookingFixture(decimal refundAmount)
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
                ("QuoteAsync", _ => Task.FromResult(new RefundQuoteResult { PaidAmount = 2_000_000m, RefundAmount = refundAmount })),
                ("OpenForCancellationAsync", _ =>
                {
                    CaseOpened = true;
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
            string status,
            Guid excludedCustomerUserId,
            DateTimeOffset? fromDate,
            DateTimeOffset? toDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RefundCase>>(
                Cases.Where(x => x.Status == status && x.CustomerUserId != excludedCustomerUserId).ToList());

        public Task<bool> ProviderTransactionUsedAsync(string providerTransactionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cases.Any(x => x.ProviderTransactionId == providerTransactionId));

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
        ((TestProxy)(object)proxy)._handlers = handlers.ToDictionary(x => x.Name, x => x.Handler);
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
