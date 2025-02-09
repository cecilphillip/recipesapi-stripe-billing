using System.Security.Claims;
using Bogus;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Recipes.Api.Data;
using Recipes.Api.Models;
using Stripe;

namespace Recipes.Api.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
public class AuthController(UserManager<RecipeApiUserDataModel> userManager, IStripeClient stripeClient, ILogger<AuthController> logger) : ControllerBase
{
    private readonly Faker _faker = new("en_US");
    private readonly CustomerService _customerService = new(stripeClient);
    private readonly AuthenticationProperties _forbiddenProperties = new(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
            "Invalid credentials"
    });
    
    [HttpGet("list/users")]
    public async Task<IActionResult> ListUsers()
    {
        var users = await userManager.Users.ToArrayAsync();
        return Ok(users);
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegistrationModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await userManager.FindByEmailAsync(model.Email);
            if (user != null)
                return StatusCode(StatusCodes.Status409Conflict);

            var ccOptions = new CustomerCreateOptions
            {
                Name = model.FullName,
                Email = model.Email,
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

            var newCustomer = await _customerService.CreateAsync(ccOptions);
            user = new RecipeApiUserDataModel
            {
                UserName = model.Email, Email = model.Email,
                StripeCustomerId = newCustomer.Id
            };
            
            var result = await userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await userManager.AddClaimAsync(user, new Claim("recipe.access", "write"));
                var cuOptions = new CustomerUpdateOptions
                {
                    Metadata = new() { [ApiConstants.StripeCustomerMetaIdKey] = user.Id }
                };

                await _customerService.UpdateAsync(newCustomer.Id, cuOptions);
                return Ok();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        
        logger.RegistrationFailed();
        return BadRequest(ModelState);
    }

    [HttpPost("~/connect/token"), IgnoreAntiforgeryToken, Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        try
        {
            var oidcRequest = HttpContext.GetOpenIddictServerRequest();
            if (oidcRequest is null)
            {
                logger.InvalidOpenIdRequest();
                return BadRequest(new OpenIddictResponse()
                {
                    Error = OpenIddictConstants.Errors.ServerError,
                    ErrorDescription = "The OpenID Connect request cannot be retrieved"
                });
            }
            
            if (oidcRequest.IsPasswordGrantType())
            {
                var user = await userManager.FindByNameAsync(oidcRequest.Username!);

                if (user == null)
                    return Forbid(_forbiddenProperties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                
                var result = await userManager.CheckPasswordAsync(user, oidcRequest.Password!);
                if (!result)
                    return Forbid(_forbiddenProperties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                
                var identity = new ClaimsIdentity(
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    nameType: OpenIddictConstants.Claims.Name,
                    roleType: OpenIddictConstants.Claims.Role);
                
                identity.SetClaim(OpenIddictConstants.Claims.Subject, user.Id)
                    .SetClaim(OpenIddictConstants.Claims.Email, user.Email)
                    .SetClaim(OpenIddictConstants.Claims.Name, user.UserName)
                    .SetClaim(OpenIddictConstants.Claims.PreferredUsername, user.UserName)
                    .SetClaim(ApiConstants.StripeCustomerIdClaimType, user.StripeCustomerId);

              var userClaims =   await userManager.GetClaimsAsync(user);
                if (userClaims.Any())
                    identity.AddClaims(userClaims);
                
                var userRoles = await userManager.GetRolesAsync(user);
                if (userRoles.Any())
                    identity.SetClaims(OpenIddictConstants.Claims.Role, [.. userRoles]);

                var requestScopes = oidcRequest.GetScopes();
                identity.SetScopes(requestScopes.Intersect([
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Roles,
                    OpenIddictConstants.Scopes.OfflineAccess
                ]));
                
                identity.SetDestinations(GetDestinations);
                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
        }
        catch (Exception ex)
        {
            logger.InvalidLoginAttempt();
            return BadRequest(new OpenIddictResponse()
            {
                Error = OpenIddictConstants.Errors.ServerError,
                ErrorDescription = "Invalid login attempt"
            });
        }
        
        logger.UnsupportedGrantType();
        return BadRequest(new OpenIddictResponse()
        {
            Error = OpenIddictConstants.Errors.InvalidGrant,
            ErrorDescription = "Grant type not supported."
        });
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case OpenIddictConstants.Claims.Name or OpenIddictConstants.Claims.PreferredUsername
                or ApiConstants.StripeCustomerIdClaimType:
                
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;

            case OpenIddictConstants.Claims.Email:
                yield return OpenIddictConstants.Destinations.AccessToken;

                if (claim.Subject!.HasScope(OpenIddictConstants.Permissions.Scopes.Email))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;

            default:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
        }
    }
}