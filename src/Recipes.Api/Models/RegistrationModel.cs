using System.ComponentModel.DataAnnotations;

namespace Recipes.Api.Models;

public class RegistrationModel
{
    [Required]
    public string FullName { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}