using System.Collections.Concurrent;
using System.Threading.Channels;
using Stripe;
using Stripe.Billing;

namespace Recipes.Api.Workers;

public class ReportUsageWorker(Channel<ApiUsageReport> apiUsageChannel, IServiceProvider provider): BackgroundService
{
    private const int BatchSize = 10;
    private readonly ConcurrentBag<ApiUsageReport> _reportedUsage = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await apiUsageChannel.Reader.WaitToReadAsync(stoppingToken))
        {
            var report = await apiUsageChannel.Reader.ReadAsync(stoppingToken);

            _reportedUsage.Add(report);

            if (_reportedUsage.Count >= BatchSize)
            {
                await ReportUsageBatch();
            }
        }
    }

    private async Task ReportUsageBatch()
    {
        var groupedUsage = _reportedUsage.Take(_reportedUsage.Count)
            .GroupBy(r => r.CustomerId)
            .Select(g => new ApiUsageReport(g.Key, g.Sum(r => r.Usage)));

        foreach (var usageReport in groupedUsage)
        {
            var options = new MeterEventCreateOptions
            {
                EventName = ApiConstants.ReportUsageEventName,
                Payload = new Dictionary<string, string>
                {
                    { ApiConstants.ReportUsageEventCustomer, usageReport.CustomerId },
                    { ApiConstants.ReportUsageEventValue, usageReport.Usage.ToString() },
                },
            };

            var scope = provider.CreateScope();
            var stripeClient = scope.ServiceProvider.GetRequiredService<StripeClient>();

            await stripeClient.V1.Billing.MeterEvents.CreateAsync(options);
        }
        _reportedUsage.Clear();
    }
}

public record ApiUsageReport(string CustomerId, int Usage);