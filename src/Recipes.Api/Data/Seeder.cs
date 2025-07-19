using System.Security.Claims;
using System.Text.Json;
using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Recipes.Api.Models;
using Stripe;
using Stripe.Billing;
using Stripe.TestHelpers;
using File = System.IO.File;

namespace Recipes.Api.Data;

public class Seeder(
    UserManager<RecipeApiUserDataModel> userManager,
    RecipeDbContext dbContext,
    StripeClient stripeClient,
    ILogger<Seeder> logger) 
{
    private readonly Faker _faker = new("en_US");

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseAsync(stoppingToken);
        await SeedDataAsync(stoppingToken);
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        if (!await dbCreator.ExistsAsync(cancellationToken))
        {
            await dbCreator.CreateAsync(cancellationToken);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private async Task SeedDataAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Recipes.Any())
        {
            await CreateRecipesAsync(cancellationToken);
        }
        
        if (!await AnyExistingProductsAsync(cancellationToken))
        {
          await CreateProductAsync(cancellationToken);
        }

        if (!dbContext.Users.Any())
        {
            await CreateUsersAsync(cancellationToken);
        }
    }

    // Stripe
    private async Task<bool> AnyExistingProductsAsync(CancellationToken cancelToken)
    {
        var listOptions = new ProductListOptions { Active = true, Limit = 1 };


        var existingProducts = await stripeClient.V1.Products.ListAsync(listOptions, cancellationToken: cancelToken);
        return existingProducts.Any();
    }

    private async Task<Meter?> GetExistingMeterAsync(CancellationToken cancelToken)
    {
        var listOptions = new MeterListOptions() { Limit = 5 };


        var existingMeters =
            await stripeClient.V1.Billing.Meters.ListAsync(listOptions, cancellationToken: cancelToken);

        foreach (var meter in existingMeters)
        {
            if (meter.EventName == ApiConstants.ReportUsageEventName)
            {
                return meter;
            }
        }

        return null;
    }

    private async Task CreateProductAsync(CancellationToken cancellationToken)
    {
        // Create Standard Tier Product
        var productTierOptions = new ProductCreateOptions
        {
            Name = "Recipes API Standard Access",
            Description = "Standard Tier Recipes API access",
            UnitLabel = "API Calls"
        };

        var standardTierProduct =
            await stripeClient.V1.Products.CreateAsync(productTierOptions, cancellationToken: cancellationToken);

        // Create Standard tier price
        var standardTierPriceOptions = new PriceCreateOptions
        {
            Nickname = "Base monthly price",
            Product = standardTierProduct.Id,
            Currency = "usd",
            UnitAmount = 4000,
            BillingScheme = "per_unit",
            Recurring = new PriceRecurringOptions { UsageType = "licensed", Interval = "month" },
        };

        await stripeClient.V1.Prices.CreateAsync(standardTierPriceOptions, cancellationToken: cancellationToken);

        //Create billing meter if it doesn't exist
        var meter = await GetExistingMeterAsync(cancellationToken);

        if (meter is null)
        {
            var meterCreateOptions = new MeterCreateOptions
            {
                DisplayName = "Standard API Requests",
                EventName = ApiConstants.ReportUsageEventName,
                DefaultAggregation = new MeterDefaultAggregationOptions { Formula = "sum", },
                ValueSettings = new MeterValueSettingsOptions { EventPayloadKey = ApiConstants.ReportUsageEventValue },
                CustomerMapping = new MeterCustomerMappingOptions
                {
                    Type = "by_id",
                    EventPayloadKey = "stripe_customer_id",
                },
            };
            meter = await stripeClient.V1.Billing.Meters.CreateAsync(meterCreateOptions,
                cancellationToken: cancellationToken);
        }

        // Create Standard tier metered price
        var standardTierMeteredPrice = new PriceCreateOptions
        {
            Nickname = "API overages",
            Product = standardTierProduct.Id,
            Currency = "usd",
            BillingScheme = "tiered",
            Recurring = new PriceRecurringOptions
            {
                UsageType = "metered",
                Interval = "month",
                Meter = meter.Id
            },
            TiersMode = "graduated",
            Tiers =
            [
                new PriceTierOptions() { UpTo = 40, UnitAmount = 0 },
                new PriceTierOptions() { UpTo = PriceTierUpTo.Inf, UnitAmount = 25 }
            ],
        };

        await stripeClient.V1.Prices.CreateAsync(standardTierMeteredPrice, cancellationToken: cancellationToken);
    }

    private async Task CreateRecipesAsync(CancellationToken cancellationToken)
    {
        var lookupCodeGenerator = new Randomizer();

        // Read the transformed_recipes.json file into a JsonDocument and populate a list of RecipeDataModel objects 
        var jsonStream = File.OpenRead("transformed_recipes.json");
        var parsedRecipes = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);

        foreach (var jsonRecipe in parsedRecipes.RootElement.EnumerateArray())
        {
            var recipe = new RecipeDataModel
            {
                Name = jsonRecipe.GetProperty("title").GetString() ?? "No Title",
                Description = jsonRecipe.GetProperty("description").GetString() ?? "No Description",
                LookupCode = lookupCodeGenerator.Replace("REC-???*****"),
                Tags = jsonRecipe.GetProperty("tags").EnumerateArray()
                    .Select(rt => rt.GetString())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToArray()!
            };

            foreach (var jsonIngredient in jsonRecipe.GetProperty("ingredients").EnumerateArray())
            {
                var ingredient = new IngredientDataModel
                {
                    Name = jsonIngredient.GetProperty("name").GetString() ?? "No Name",
                    Quantity = float.Parse(jsonIngredient.GetProperty("amount").GetString() ?? "0"),
                    Unit = jsonIngredient.GetProperty("unit").GetString() ?? "No Unit"
                };
                recipe.Ingredients.Add(ingredient);
            }

            dbContext.Recipes.Add(recipe);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateUsersAsync(CancellationToken cancellationToken)
    {
        await CreateUserAsync("James Moriarty", "james@test.com", false, cancellationToken);
        await CreateUserAsync("Victor Von Doom", "victor@test.com", false,  cancellationToken);
        await CreateUserAsync("Dorian Gray", "dorian@test.com", false,  cancellationToken);
    }


    private async Task CreateUserAsync(string name, string email, bool admin, CancellationToken cancellationToken)
    {
        // Set customer options
        var ccOptions = new CustomerCreateOptions
        {
            Name = name,
            Email = email,
            Description = "Faker User Account",
            PaymentMethod = "pm_card_visa",
            Address = new AddressOptions
            {
                Line1 = _faker.Address.StreetAddress(),
                City = _faker.Address.City(),
                State = _faker.Address.StateAbbr(),
                Country = "US"
            },
            InvoiceSettings =
                new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = "pm_card_visa" }
        };

       
            // Create test clock
            var tcCreateOptions = new TestClockCreateOptions
            {
                Name = $"Subscription Clock ({name})", FrozenTime = DateTimeOffset.UtcNow.DateTime
            };

            var newTestClock = await stripeClient.V1.TestHelpers.TestClocks.CreateAsync(tcCreateOptions, cancellationToken: cancellationToken);
            ccOptions.TestClock = newTestClock.Id;
        

        // Create Stripe customer
        var newCustomer = await stripeClient.V1.Customers.CreateAsync(ccOptions, cancellationToken: cancellationToken);

        var newUser = new RecipeApiUserDataModel
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            StripeCustomerId = newCustomer.Id
        };

        // Add user to the identity store
        await userManager.CreateAsync(newUser, "test");
        await userManager.AddClaimAsync(newUser, new Claim("recipe.access", admin ? "admin" : "write"));

        var cuOptions = new CustomerUpdateOptions
        {
            Metadata = new() { [ApiConstants.StripeCustomerMetaIdKey] = newUser.Id }
        };

        await stripeClient.V1.Customers.UpdateAsync(newCustomer.Id, cuOptions, cancellationToken: cancellationToken);
        
        // There is only one product in the test account, so we can just get the prices for it
        var plOptions = new PriceListOptions {
            Limit = 2,Active = true,
        };
      
        var prices = await stripeClient.V1.Prices.ListAsync(plOptions, cancellationToken: cancellationToken);
        var lineItems = prices.Select(p => new SubscriptionItemOptions {
            Price = p.Id, Quantity = !p.BillingScheme.Equals("tiered") ? 1 : null
        }).ToList();
        
        // Create a new subscription for the customer (replace with your price ID)
        await stripeClient.V1.Subscriptions.CreateAsync(new SubscriptionCreateOptions
        {
            Customer = newCustomer.Id,
            Items = lineItems
        }, cancellationToken: cancellationToken);
    }
}