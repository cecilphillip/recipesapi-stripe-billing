namespace Recipes.Api;

public static class ApiConstants
{
    public const int DefaultPageSize = 10;
    public const string StripeCustomerIdClaimType = "stripe.customer.id";
    public const string StripeCustomerMetaIdKey = "recipe.customer.id";
    
    public const string ReportUsageEventName = "standard_api_requests";
    public const string ReportUsageEventValue = "requests";
    public const string ReportUsageEventCustomer = "stripe_customer_id";
}
