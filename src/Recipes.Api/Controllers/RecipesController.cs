using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Recipes.Api.Data;
using Recipes.Api.Filters;
using Recipes.Api.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Recipes.Api.Controllers;

[ApiController]
[ServiceFilter<ReportApiUsageFilter>()]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class RecipesController(RecipeDbContext dbContext, IValidator<RecipeApiModel> recipeValidator , IFusionCache cache, ILogger<RecipesController> logger)
    : ControllerBase
{
    [EndpointName(nameof(GetRecipeByLookUp))]
    [EndpointSummary("Retrieves a recipe by its lookup code.")]
    [EndpointDescription("This endpoint retrieves a recipe by its lookup code. If not found, a 404 is returned.")]
    [HttpGet("{lookup}")]
    public async Task<ActionResult> GetRecipeByLookUp(string lookup)
    {
        try
        {
            var recipe = await cache.GetOrSetAsync<RecipeApiModel>($"product:{lookup}", async (ctx, ct) =>
            {
                var foundRecipe = await dbContext.Recipes.AsNoTracking()
                    .FirstOrDefaultAsync(r => EF.Functions.ILike(r.LookupCode, lookup), cancellationToken: ct);

                return foundRecipe is null ? ctx.Fail("Not found") : foundRecipe.ToApiModel();
            });

            return Ok(recipe);
        }
        catch (FusionCacheFactoryException ex) when (ex.Message == "Not found")
        {
            logger.RecipeNotFoundForLookupCode(lookup);
            return NotFound();
        }
        catch (Exception ex)
        {
            logger.RecipeForLookupCodeException(lookup, ex);
            return BadRequest();
        }
    }

    [EndpointName(nameof(GetRandomRecipe))]
    [EndpointSummary("Retrieve a random recipe.")]
    [EndpointDescription("This endpoint retrieves a random recipe.")]
    [HttpGet("random")]
    public async Task<ActionResult> GetRandomRecipe()
    {
        try
        {
            var randomRecipe = await dbContext.Recipes
                .FromSql($"SELECT * FROM \"Recipes\" ORDER BY RANDOM() LIMIT 1")
                .FirstOrDefaultAsync();

            return randomRecipe is null ? NotFound() : Ok(randomRecipe.ToApiModel());
        }
        catch (Exception ex)
        {
            logger.GetRandomRecipeException(ex);
            return BadRequest();
        }
    }

    [EndpointName(nameof(GetRecipesByCategory))]
    [EndpointSummary("Retrieve recipes by category.")]
    [EndpointDescription(
        "This endpoint retrieves recipes by category. If no recipes are found, an empty list is returned.")]
    [HttpGet("category/{category}")]
    public async Task<ActionResult> GetRecipesByCategory(string category)
    {
        try
        {
            var recipes = await cache.GetOrSetAsync<IEnumerable<RecipeApiModel>>($"category:{category}",
                async (ctx, ct) =>
                {
                    var categorizedRecipes = await dbContext.Recipes
                        .Where(r => r.Tags.Contains(category))
                        .ToListAsync(cancellationToken: ct);

                    return categorizedRecipes.Count != 0
                        ? ctx.Fail("Not found")
                        : categorizedRecipes.Select(r => r.ToApiModel());
                });

            return Ok(recipes);
        }
        catch (FusionCacheFactoryException ex) when (ex.Message == "Not found")
        {
            logger.NoRecipesForCategory(category);
            return NotFound();
        }
        catch (Exception ex)
        {
            logger.GetRecipeCategoryException(category, ex);
            return BadRequest();
        }
    }

    [EndpointName(nameof(GetLatestRecipes))]
    [EndpointSummary("Retrieve the latest recipes.")]
    [EndpointDescription(
        "This endpoint retrieves the latest recipes. An optional count parameter can be specified to define the return size. If no recipes are found, an empty list is returned.")]
    [HttpGet("latest")]
    public async Task<ActionResult> GetLatestRecipes([FromQuery] int count = ApiConstants.DefaultPageSize) 
    {
        try
        {
            var recipes = await cache.GetOrSetAsync<IEnumerable<RecipeApiModel>>($"latest:{count}",
                async (ctx, ct) =>
                {
                    var categorizedRecipes = await dbContext.Recipes
                        .OrderByDescending(r => r.DateCreated )
                        .ToListAsync(cancellationToken: ct);

                    return categorizedRecipes.Count != 0
                        ? ctx.Fail("Not found")
                        : categorizedRecipes.Select(r => r.ToApiModel());
                });

            return Ok(recipes);
        }
        catch (FusionCacheFactoryException ex) when (ex.Message == "Not found")
        {
            logger.NoLatestRecipesFound();
            return NotFound();
        }
        catch (Exception ex)
        {
            logger.LatestRecipeException(ex);
            return BadRequest();
        }
    }

    [EndpointName(nameof(CreateRecipe))]
    [EndpointSummary("Create a new recipe.")]
    [EndpointDescription("This endpoint creates a new recipe based on the payload provded in the request.")]
    [HttpPost]
    public async Task<ActionResult> CreateRecipe(RecipeApiModel newRecipe)
    {
        var validationResult = await recipeValidator.ValidateAsync(newRecipe);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem();
        }
        
        var recipeDataModel = newRecipe.ToDataModel();
        dbContext.Recipes.Add(recipeDataModel);
        
        await dbContext.SaveChangesAsync();
        return Ok();
    }
    
    [EndpointName(nameof(DeleteRecipe))]
    [EndpointSummary("Delete a recipe.")]
    [EndpointDescription(
        "This endpoint deletes a recipe associated with the provided lookup code. If the recipe is not found, a 404 is returned.")]
    [HttpDelete("{lookup}")]
    public async Task<ActionResult> DeleteRecipe(string lookup)
    {
        try
        {
            var recipe = await dbContext.Recipes.FirstOrDefaultAsync(r => EF.Functions.ILike(r.LookupCode, lookup));
            
            if (recipe != null) 
            {
                dbContext.Recipes.Remove(recipe); 
                await dbContext.SaveChangesAsync();
                await cache.RemoveAsync($"product:{lookup}");
                return Ok(recipe);
            }

            logger.NoRecipeLookupFoundToDelete(lookup);
            return NotFound();
        }
        catch (Exception ex)
        {
            logger.DeleteRecipeByLookupException(lookup, ex);
            return BadRequest();
        }
    }
}