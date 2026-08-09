using System.Security.Cryptography;
using System.Text;
using DirectiveDrift.Application.Ports;

namespace DirectiveDrift.Api;

public sealed class GuestSessionMiddleware(RequestDelegate next)
{
    public const string OwnerItem = "directive-drift.owner";
    public const string GuestCookie = "dd_guest";
    public const string CsrfCookie = "dd_csrf";
    public const string CsrfHeader = "X-DD-CSRF";

    public async Task InvokeAsync(
        HttpContext context,
        IGameRepository repository,
        TimeProvider timeProvider)
    {
        var hadGuestCookie = context.Request.Cookies.TryGetValue(
                GuestCookie,
                out var presentedOwnerId)
            && presentedOwnerId is { Length: 54 }
            && presentedOwnerId.StartsWith("guest_", StringComparison.Ordinal)
            && await repository.GuestExistsAsync(presentedOwnerId, context.RequestAborted);
        var ownerId = hadGuestCookie ? presentedOwnerId! : CreateToken("guest");

        var hadCsrfCookie = context.Request.Cookies.TryGetValue(CsrfCookie, out var csrfValue)
            && !string.IsNullOrWhiteSpace(csrfValue)
            && csrfValue.Length == 53
            && csrfValue.StartsWith("csrf_", StringComparison.Ordinal);
        var csrf = hadCsrfCookie ? csrfValue! : CreateToken("csrf");

        if (hadGuestCookie && IsMutation(context.Request.Method) && !MatchesCsrf(context, csrf))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "https://directive-drift.invalid/problems/csrf",
                    title = "CSRF validation failed.",
                    status = StatusCodes.Status400BadRequest,
                    code = "csrf-invalid",
                },
                context.RequestAborted);
            return;
        }

        await repository.EnsureGuestAsync(ownerId, context.RequestAborted);
        context.Items[OwnerItem] = ownerId;

        var common = new CookieOptions
        {
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = timeProvider.GetUtcNow().AddYears(1),
        };
        if (!hadGuestCookie)
        {
            context.Response.Cookies.Append(
                GuestCookie,
                ownerId,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = common.Secure,
                    SameSite = common.SameSite,
                    Path = common.Path,
                    Expires = common.Expires,
                });
        }

        if (!hadCsrfCookie)
        {
            context.Response.Cookies.Append(CsrfCookie, csrf, common);
        }

        await next(context);
    }

    public static string Owner(HttpContext context) =>
        (string)(context.Items[OwnerItem]
            ?? throw new InvalidOperationException("Guest session middleware did not run."));

    private static bool IsMutation(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static bool MatchesCsrf(HttpContext context, string expected)
    {
        var actual = context.Request.Headers[CsrfHeader].ToString();
        if (actual.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual),
            Encoding.UTF8.GetBytes(expected));
    }

    private static string CreateToken(string prefix) =>
        $"{prefix}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
}
