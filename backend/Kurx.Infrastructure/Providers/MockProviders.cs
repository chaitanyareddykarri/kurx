using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Providers;

// Mock money/KYC providers: succeed instantly and mint deterministic-looking ids
// so downstream flows (webhooks, transfers, ledger) can be exercised offline.

public class MockPaymentGateway(ILogger<MockPaymentGateway> log) : IPaymentGateway
{
    public Task<GatewayOrder> CreateOrderAsync(Guid orderId, long amountPaise, CancellationToken ct = default)
    {
        var id = $"order_mock_{orderId:N}";
        log.LogInformation("[pay→mock] created {GatewayOrderId} amount={Amount}p", id, amountPaise);
        return Task.FromResult(new GatewayOrder(id, amountPaise, "INR"));
    }

    public Task<GatewayRefundResult> RefundAsync(string gatewayPaymentId, long amountPaise, CancellationToken ct = default)
    {
        var id = $"rfnd_mock_{Guid.NewGuid():N}";
        log.LogInformation("[pay→mock] refund {RefundId} of {PaymentId} amount={Amount}p", id, gatewayPaymentId, amountPaise);
        return Task.FromResult(new GatewayRefundResult(id, "processed"));
    }

    // Mock gateway accepts any signature so local webhook simulation scripts stay simple.
    public bool VerifyWebhookSignature(string rawBody, string signature) => true;
}

public class MockRouteClient(ILogger<MockRouteClient> log) : IRouteClient
{
    public Task<LinkedAccount> CreateLinkedAccountAsync(Guid orgId, string legalName, string accountNumber, string ifsc, CancellationToken ct = default)
    {
        var id = $"acc_mock_{orgId:N}";
        log.LogInformation("[route→mock] linked account {Id} for org {OrgId}", id, orgId);
        return Task.FromResult(new LinkedAccount(id));
    }

    public Task<RouteTransferResult> CreateOnHoldTransferAsync(string gatewayPaymentId, string linkedAccountId, long amountPaise, DateTime? holdUntil, CancellationToken ct = default)
        => Task.FromResult(new RouteTransferResult($"trf_mock_{Guid.NewGuid():N}", "on_hold"));

    public Task<RouteTransferResult> ReleaseTransferAsync(string transferId, CancellationToken ct = default)
        => Task.FromResult(new RouteTransferResult(transferId, "released"));

    public Task<RouteTransferResult> ReverseTransferAsync(string transferId, long amountPaise, CancellationToken ct = default)
        => Task.FromResult(new RouteTransferResult(transferId, "reversed"));
}

public class MockKycProvider : IKycProvider
{
    // Any account/PAN passes; a value ending in "0000" fails, so rejection paths are testable.
    public Task<KycResult> PennyDropAsync(string accountNumber, string ifsc, string holderName, CancellationToken ct = default)
        => Task.FromResult(accountNumber.EndsWith("0000")
            ? new KycResult(false, "penny drop failed (mock)")
            : new KycResult(true, holderName));

    public Task<KycResult> PanMatchAsync(string pan, string name, CancellationToken ct = default)
        => Task.FromResult(pan.EndsWith("0000")
            ? new KycResult(false, "PAN mismatch (mock)")
            : new KycResult(true, name));

    public Task<KycResult> DigilockerAsync(string payloadJson, CancellationToken ct = default)
        => Task.FromResult(new KycResult(true, null));
}
