using FluentValidation;

namespace Recipes.Api.Models;

public class IngredientDataModel
{
    public required string Name { get; set; }
    public float Quantity { get; set; }
    public required string Unit { get; set; } 
}

public record IngredientApiModel
{
    public required string Name { get; init; }
    public float Quantity { get; set; }
    public required string Unit { get; set; }
}

public class IngredientApiModelValidator : AbstractValidator<IngredientApiModel>
{
    public IngredientApiModelValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Unit).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}