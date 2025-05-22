using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Recipes.Api.Models;
using Stripe;

namespace Recipes.Api;

public static class Extensions
{  
    public static RecipeApiModel ToApiModel(this RecipeDataModel dataModel) => new()
    {
        Name = dataModel.Name,
        Description = dataModel.Description,
        Ingredients = dataModel.Ingredients.Select(i => i.ToApiModel()),
        LookupCode = dataModel.LookupCode,
        Tags = dataModel.Tags
    };
    
    public static RecipeDataModel ToDataModel(this RecipeApiModel apiModel) => new()
    {
        Name = apiModel.Name,
        Description = apiModel.Description,
        Ingredients = apiModel.Ingredients.Select(i => i.ToDataModel()).ToList(),
        LookupCode = apiModel.LookupCode,
        Tags = apiModel.Tags
    };
    
    public static IngredientApiModel ToApiModel(this IngredientDataModel dataModel) => new()
    {
        Name = dataModel.Name,
        Quantity = dataModel.Quantity,
        Unit = dataModel.Unit
    };
    
    public static IngredientDataModel ToDataModel(this IngredientApiModel apiModel) => new()
    {
        Name = apiModel.Name,
        Quantity = apiModel.Quantity,
        Unit = apiModel.Unit
    };

    public static void AddToModelState(this ValidationResult result, ModelStateDictionary modelState)
    {
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 401,
        Level = LogLevel.Error,
        Message = "Recipe {LookupCode} not found")]
    public static partial void RecipeNotFoundForLookupCode(this ILogger logger, string lookupCode);
    
    [LoggerMessage(
        EventId = 402,
        Level = LogLevel.Error,
        Message = "Unknown exception when trying to get Recipe {LookupCode}")]
    public static partial void RecipeForLookupCodeException(this ILogger logger, string lookupCode, Exception ex);

    [LoggerMessage(
        EventId = 403,
        Level = LogLevel.Error,
        Message = "Unknown exception when trying to get random Recipe")]
    public static partial void GetRandomRecipeException(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 405,
        Level = LogLevel.Error,
        Message = "No recipes found for {Category}")]
    public static partial void NoRecipesForCategory(this ILogger logger, string category);

    [LoggerMessage(
        EventId = 406,
        Level = LogLevel.Error,
        Message = "Unknown exception when trying to get recipe for {Category}")]
    public static partial void GetRecipeCategoryException(this ILogger logger, string category, Exception ex);
    
    [LoggerMessage(
        EventId = 407,
        Level = LogLevel.Error,
        Message = "No latest recipes found")]
    public static partial void NoLatestRecipesFound(this ILogger logger);

    
    [LoggerMessage(
        EventId = 408,
        Level = LogLevel.Error,
        Message = "Unknown exception when trying to get latest recipes")]
    public static partial void LatestRecipeException(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 409,
        Level = LogLevel.Error,
        Message = "No entry found for recipe {LookupCode} to delete")]
    public static partial void NoRecipeLookupFoundToDelete(this ILogger logger, string lookupCode);
    
    
    [LoggerMessage(
        EventId = 410,
        Level = LogLevel.Error,
        Message = "Unknown exception when trying to get recipe for {LookupCode}")]
    public static partial void DeleteRecipeByLookupException(this ILogger logger, string lookupCode, Exception ex);

    [LoggerMessage(
        EventId = 501,
        Level = LogLevel.Error,
        Message = "Registration failed")]
    public static partial void RegistrationFailed(this ILogger logger);
    
    [LoggerMessage(
        EventId = 502,
        Level = LogLevel.Error,
        Message = "Invalid login attempt")]
    public static partial void InvalidLoginAttempt(this ILogger logger);
    
    [LoggerMessage(
        EventId = 503,
        Level = LogLevel.Error,
        Message = "Grant type not supported")]
    public static partial void UnsupportedGrantType(this ILogger logger);
    
    [LoggerMessage(
        EventId = 504,
        Level = LogLevel.Error,
        Message = "Invalid OpenID request")]
    public static partial void InvalidOpenIdRequest(this ILogger logger);
}