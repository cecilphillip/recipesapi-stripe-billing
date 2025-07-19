using System.Threading.Channels;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Recipes.Api;
using Recipes.Api.Data;
using Recipes.Api.Filters;
using Recipes.Api.Models;
using Recipes.Api.Workers;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register app services
builder.AddNpgsqlDbContext<RecipeDbContext>("recipesDb",
    settings =>
    {
        settings.DisableRetry = false;
        settings.CommandTimeout = 30;
        settings.DisableHealthChecks = false;
    },
    options => { options.UseOpenIddict(); });

builder.AddCaching();
builder.AddIdentityServices();

builder.Services.AddProblemDetails();

builder.Services.AddValidatorsFromAssemblyContaining<RecipeApiModel>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
         options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.AddStripe();
builder.Services.AddScoped<ReportApiUsageFilter>();
builder.Services.AddTransient<Seeder>();
builder.Services.AddSingleton<Channel<ApiUsageReport>>( _ => Channel.CreateUnbounded<ApiUsageReport>(new()
{
    SingleReader = true,
    AllowSynchronousContinuations = false
}));

builder.AddBackgroundWorkers();

builder.Services.AddOpenApi("v1", options => { options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0; });

var app = builder.Build();

// Configure app pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

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