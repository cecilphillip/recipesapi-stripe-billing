using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Recipes.Api.Models;

namespace Recipes.Api;

public static class ModelExtensions
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