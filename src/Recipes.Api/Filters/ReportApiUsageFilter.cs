using System.Security.Claims;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc.Filters;
using Recipes.Api.Workers;

namespace Recipes.Api.Filters;

public class ReportApiUsageFilter(Channel<ApiUsageReport> apiUsageChannel, ILogger<ReportApiUsageFilter> logger) : IAsyncResultFilter
{

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        await next();
       
        if (context.HttpContext.User.Identity is ClaimsIdentity { IsAuthenticated: true } claimsIdentity)
        {
            var stripeCustomerClaim = claimsIdentity.FindFirst(c => c.Type == ApiConstants.StripeCustomerIdClaimType);
            if(stripeCustomerClaim is null) return;
            
            await ReportUsage(stripeCustomerClaim.Value);
        }
    }

    private async Task ReportUsage(string stripeCustomerId)
    {
        //Todo: handle error 
        await apiUsageChannel.Writer.WriteAsync(new ApiUsageReport(stripeCustomerId, 1));
    }
}