using System.Security.Claims;
using System.Text.Json;
using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Recipes.Api.Models;
using Stripe;
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

           recipe.Tags =  jsonRecipe.GetProperty("tags").EnumerateArray()
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