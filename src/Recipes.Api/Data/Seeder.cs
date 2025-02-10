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
using File = System.IO.File;

namespace Recipes.Api.Data;

public class Seeder(
    UserManager<RecipeApiUserDataModel> userManager,
    RecipeDbContext dbContext,
    IStripeClient stripeClient)
{
    private readonly Faker _faker = new("en_US");
    private readonly CustomerService _customerService = new(stripeClient);

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseAsync(stoppingToken);
        await SeedDataAsync(stoppingToken);
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        //TODO: Use polly instead of the execution strategy??

        if (!await dbCreator.ExistsAsync(cancellationToken))
        {
            await dbCreator.CreateAsync(cancellationToken);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private async Task SeedDataAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Users.Any())
        {
            await CreateUsersAsync(cancellationToken);
        }

        if (!dbContext.Recipes.Any())
        {
            await CreateRecipesAsync(cancellationToken);
        }

        if (!await AnyExistingProductsAsync(cancellationToken))
        {
            await CreateProductTiersAsync(cancellationToken);
        }
    }

    // Stripe
    private async Task<bool> AnyExistingProductsAsync(CancellationToken cancelToken)
    {
        var listOptions = new ProductListOptions { Active = true, Limit = 1 };
        var productService = new ProductService(stripeClient);

        var existingProducts = await productService.ListAsync(listOptions, cancellationToken: cancelToken);
        return existingProducts.Any();
    }
    
    private async Task<Meter?> GetExistingMeterAsync(CancellationToken cancelToken)
    {
        var listOptions = new MeterListOptions() { Limit = 5 };
        var meterService = new MeterService(stripeClient);

        var existingMeters = await meterService.ListAsync(listOptions, cancellationToken: cancelToken);

        foreach (var meter in existingMeters)
        {
            if(meter.EventName == ApiConstants.ReportUsageEventName)
            {
                return meter;
            }
        }

        return null;
    }

    private async Task CreateProductTiersAsync(CancellationToken cancellationToken)
    {
        //TODO: check for errors

        // Create Standard Tier Product
        var productTierOptions = new ProductCreateOptions
        {
            Name = "Recipes API Standard Access",
            Description = "Standard Tier Recipes API access",
            UnitLabel = "API Calls"
        };
        var productService = new ProductService(stripeClient);
        var standardTierProduct =
            await productService.CreateAsync(productTierOptions, cancellationToken: cancellationToken);

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
        var priceService = new PriceService();
        await priceService.CreateAsync(standardTierPriceOptions, cancellationToken: cancellationToken);

        //TODO: Create meter
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
        
            var meterService = new MeterService(stripeClient);
             meter = await meterService.CreateAsync(meterCreateOptions, cancellationToken: cancellationToken);
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

        await priceService.CreateAsync(standardTierMeteredPrice, cancellationToken: cancellationToken);
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
                LookupCode = lookupCodeGenerator.Replace("REC-???*****")
            };

            recipe.Tags = jsonRecipe.GetProperty("tags").EnumerateArray()
                .Select(rt => rt.GetString())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray()!;

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

        var result = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateUsersAsync(CancellationToken cancellationToken)
    {
        await CreateUserAsync("James Moriarty", "james@test.com", false, cancellationToken);
        await CreateUserAsync("Victor Von Doom", "victor@test.com", false, cancellationToken);
        await CreateUserAsync("Dorian Gray", "dorian@test.com", false, cancellationToken);
    }

    private async Task CreateUserAsync(string name, string email, bool admin, CancellationToken cancellationToken)
    {
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

        var newCustomer = await _customerService.CreateAsync(ccOptions, cancellationToken: cancellationToken);

        var newUser = new RecipeApiUserDataModel
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            StripeCustomerId = newCustomer.Id
        };

        await userManager.CreateAsync(newUser, "test");
        await userManager.AddClaimAsync(newUser, new Claim("recipe.access", admin ? "admin" : "write"));

        var cuOptions = new CustomerUpdateOptions
        {
            Metadata = new() { [ApiConstants.StripeCustomerMetaIdKey] = newUser.Id }
        };

        await _customerService.UpdateAsync(newCustomer.Id, cuOptions, cancellationToken: cancellationToken);
    }
}