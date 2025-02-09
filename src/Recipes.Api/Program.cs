using System.Threading.Channels;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Recipes.Api;
using Recipes.Api.Data;
using Recipes.Api.Filters;
using Recipes.Api.Models;
using Recipes.Api.Workers;
using Scalar.AspNetCore;
using ZiggyCreatures.Caching.Fusion;

var builder = WebApplication.CreateBuilder(args);

builder.AddRedisDistributedCache(connectionName: "redisCache");

builder.Services.AddFusionCache()
    .WithDefaultEntryOptions(new FusionCacheEntryOptions
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromMinutes(5),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(5),
        AllowBackgroundDistributedCacheOperations = true,
        Duration = TimeSpan.FromMinutes(4)
    })
    .WithSystemTextJsonSerializer()
    .WithRegisteredDistributedCache();

builder.AddNpgsqlDbContext<RecipeDbContext>("recipesDb",
    settings =>
    {
        settings.DisableRetry = false;
        settings.CommandTimeout = 30;
        settings.DisableHealthChecks = false;
    },
    options => { options.UseOpenIddict(); });

builder.Services.AddIdentityCore<RecipeApiUserDataModel>(options =>
    {
        // Relax default password settings.
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 4;
        options.Password.RequiredUniqueChars = 0;
    })
    .AddDefaultTokenProviders()
    .AddSignInManager()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<RecipeDbContext>();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance =
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

        context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
    };
});

builder.Services.AddValidatorsFromAssemblyContaining<RecipeApiModel>();

builder.Services.AddOpenIddict()
    .AddCore(options => { options.UseEntityFrameworkCore().UseDbContext<RecipeDbContext>(); })
    .AddServer(options =>
    {
        options.AcceptAnonymousClients()
            .AllowPasswordFlow()
            .AllowClientCredentialsFlow()
            .AllowRefreshTokenFlow();

        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate()
            .DisableAccessTokenEncryption();

        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough();
        
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token");
    })
    .AddValidation(options =>
    {
        // Import configuration from local OpenIddict server
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddControllers(options =>
    {
        //options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        //options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.ModelValidatorProviders.Clear();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        //options.SuppressModelStateInvalidFilter = true;
        //options.SuppressMapClientErrors = false;
    });
builder.Services.AddScoped<ReportApiUsageFilter>();
builder.Services.AddStripe(builder.Configuration);

builder.Services.AddTransient<Seeder>();
builder.Services.AddHostedService<BootstrapWorker>();
builder.Services.AddHostedService<ReportUsageWorker>();

builder.Services.AddSingleton<Channel<ApiUsageReport>>( _ => Channel.CreateUnbounded<ApiUsageReport>(new()
{
    SingleReader = true,
    AllowSynchronousContinuations = false
}));

builder.Services.AddOpenApi("v1", options => { options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0; });

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi("/openapi/{documentName}/spec.json");
app.MapScalarApiReference(options =>
{
    options.WithTitle("Recipes API")
        .WithOpenApiRoutePattern("/openapi/{documentName}/spec.json");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();

app.Run();