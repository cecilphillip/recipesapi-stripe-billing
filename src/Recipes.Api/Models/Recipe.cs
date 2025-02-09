using FluentValidation;

namespace Recipes.Api.Models;

public class RecipeDataModel
{
    public string Id { get; set; } = Ulid.NewUlid().ToString();
    public required string Name { get; set; }
    public required string Description { get; set; }
    public ICollection<IngredientDataModel> Ingredients { get; set; } = [];
    public required string LookupCode { get; init; }
    public string[] Tags { get; set; } = [];
    
    public DateTime DateCreated { get; set; }
}

public record RecipeApiModel
{
    public  string Name { get; set; }
    public  string Description { get; set; }
    public  IEnumerable<IngredientApiModel> Ingredients { get; set; } = [];
    public  string LookupCode { get; set; }
    public string[] Tags { get; init; } = [];
}

public class RecipeApiModelValidator : AbstractValidator<RecipeApiModel>
{
    public RecipeApiModelValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("oops");
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.LookupCode).NotEmpty();
        RuleForEach(x => x.Ingredients).SetValidator(new IngredientApiModelValidator());
    }
}
