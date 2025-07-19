using Microsoft.AspNetCore.Identity;
using Recipes.Api.Data;
using Recipes.Api.Workers;
using ZiggyCreatures.Caching.Fusion;

namespace Recipes.Api;

public static class BuilderServiceExtensions
{
    public static WebApplicationBuilder AddCaching(this WebApplicationBuilder builder)
    {
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
        
        return builder;
    }

    public static WebApplicationBuilder AddIdentityServices(this WebApplicationBuilder builder)
    {
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
        
        return builder;
    }
    
    public static WebApplicationBuilder AddBackgroundWorkers(this WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<BootstrapWorker>();
        builder.Services.AddHostedService<ReportUsageWorker>();
        return builder;
    }
}