using System.Security.Claims;
using FAInances.Services.Entities;
using FAInances.Services.Exceptions;
using FAInances.Services.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FAInances.Web.Controllers;

/// <summary>Handles the login/register/logout form posts and issues the auth cookie.</summary>
[Route("account")]
public class AccountController(IUserService userService) : Controller
{
    public record LoginForm(string Email, string Password, bool RememberMe = false, string? ReturnUrl = null);

    public record RegisterForm(string Email, string DisplayName, string Password, string ConfirmPassword);

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginForm form, CancellationToken cancellationToken)
    {
        var user = await userService.ValidateCredentialsAsync(form.Email, form.Password, cancellationToken);
        if (user is null)
            return Redirect(WithQuery("/login", ("error", "Invalid email or password."), ("returnUrl", form.ReturnUrl)));

        await SignInAsync(user, form.RememberMe);
        return LocalRedirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl : "/");
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([FromForm] RegisterForm form, CancellationToken cancellationToken)
    {
        if (form.Password != form.ConfirmPassword)
            return Redirect(WithQuery("/register", ("error", "Passwords do not match.")));

        User user;
        try
        {
            user = await userService.RegisterAsync(form.Email, form.DisplayName, form.Password, cancellationToken);
        }
        catch (BusinessRuleException ex)
        {
            return Redirect(WithQuery("/register", ("error", ex.Message)));
        }

        await SignInAsync(user, isPersistent: false);
        return LocalRedirect("/");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/login");
    }

    private Task SignInAsync(User user, bool isPersistent)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Email, user.Email)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = isPersistent });
    }

    private static string WithQuery(string path, params (string Key, string? Value)[] parameters) =>
        QueryHelpers.AddQueryString(
            path,
            parameters.Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)));
}
