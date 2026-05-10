using System.Text.Json;
using VNUFLearning.Gateway.Services;

namespace VNUFLearning.Gateway.Middleware;

/// <summary>
/// Validates JWT trên mỗi request:
/// - Public paths -> pass-through
/// - Protected paths -> kiểm tra token, inject X-User-Id, X-User-Role, X-User-Name vào header gửi xuống VNUFLearning
/// - Role-protected paths (/admin, /teacher, /student) -> chặn nếu role không khớp
/// </summary>
public class JwtValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private readonly TokenValidator _tokenValidator;
    private readonly ILogger<JwtValidationMiddleware> _logger;

    public JwtValidationMiddleware(
        RequestDelegate next,
        IConfiguration config,
        TokenValidator tokenValidator,
        ILogger<JwtValidationMiddleware> logger)
    {
        _next = next;
        _config = config;
        _tokenValidator = tokenValidator;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // CORS preflight
        if (context.Request.Method == HttpMethods.Options)
        {
            await _next(context);
            return;
        }

        // Public paths
        var unauthPaths = _config.GetSection("Gateway:UnauthenticatedPaths").Get<List<string>>() ?? new();
        bool isPublic = unauthPaths.Any(pub =>
            path.Equals(pub.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(pub.ToLowerInvariant() + "/", StringComparison.OrdinalIgnoreCase));

        if (isPublic)
        {
            await _next(context);
            return;
        }

        // Trích xuất token: ưu tiên header, fallback cookie (cho MVC views vẫn dùng cookie)
        string? token = null;
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is not null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader["Bearer ".Length..].Trim();
        }
        else if (context.Request.Cookies.TryGetValue("access_token", out var cookieToken))
        {
            token = cookieToken;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            // Nếu là API request -> trả 401 JSON. Nếu là MVC -> redirect về login
            if (IsApiRequest(context))
            {
                await WriteUnauthorized(context, "Yêu cầu đăng nhập. Thiếu token xác thực.");
            }
            else
            {
                context.Response.Redirect("/Account/Login");
            }
            return;
        }

        var principal = _tokenValidator.ValidateToken(token);
        if (principal is null)
        {
            if (IsApiRequest(context))
            {
                await WriteUnauthorized(context, "Token không hợp lệ hoặc đã hết hạn.");
            }
            else
            {
                context.Response.Redirect("/Account/Login");
            }
            return;
        }

        // Lấy claim
        var userId = _tokenValidator.GetClaim(principal, "UserId")
                  ?? _tokenValidator.GetClaim(principal, "sub")
                  ?? _tokenValidator.GetClaim(principal, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        var roleCode = _tokenValidator.GetClaim(principal, "role")
                    ?? _tokenValidator.GetClaim(principal, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        var userName = _tokenValidator.GetClaim(principal, "name")
                    ?? _tokenValidator.GetClaim(principal, "FullName")
                    ?? _tokenValidator.GetClaim(principal, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

        // Role-based path guard
        var roleProtected = _config.GetSection("Gateway:RoleProtectedPaths")
            .Get<Dictionary<string, string[]>>() ?? new();
        foreach (var kv in roleProtected)
        {
            var prefix = kv.Key.ToLowerInvariant();
            if (path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                path.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(roleCode) || !kv.Value.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Forbidden: user {UserId} role={Role} -> {Path}", userId, roleCode, path);
                    await WriteForbidden(context, "Bạn không có quyền truy cập tài nguyên này.");
                    return;
                }
                break;
            }
        }

        // Inject user context cho downstream.
        // HTTP header chỉ được phép ASCII -> percent-encode value có thể chứa Unicode (FullName tiếng Việt).
        if (!string.IsNullOrWhiteSpace(userId))
            context.Request.Headers["X-User-Id"] = AsciiSafe(userId);
        if (!string.IsNullOrWhiteSpace(roleCode))
            context.Request.Headers["X-User-Role"] = AsciiSafe(roleCode);
        if (!string.IsNullOrWhiteSpace(userName))
            context.Request.Headers["X-User-Name"] = Uri.EscapeDataString(userName);

        _logger.LogDebug("Authorized: userId={UserId} role={Role} -> {Path}", userId, roleCode, path);

        await _next(context);
    }

    /// <summary>
    /// Trả về chuỗi nếu đã ASCII; ngược lại percent-encode để tránh lỗi
    /// "Request headers must contain only ASCII characters" khi YARP forward.
    /// </summary>
    private static string AsciiSafe(string value)
    {
        foreach (var c in value)
        {
            if (c > 0x7F) return Uri.EscapeDataString(value);
        }
        return value;
    }

    private static bool IsApiRequest(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return true;
        var accept = ctx.Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteUnauthorized(HttpContext context, string message) =>
        WriteJson(context, StatusCodes.Status401Unauthorized, message);

    private static Task WriteForbidden(HttpContext context, string message) =>
        WriteJson(context, StatusCodes.Status403Forbidden, message);

    private static Task WriteJson(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = JsonSerializer.Serialize(new
        {
            success = false,
            message,
            data = (object?)null,
            errors = (object?)null,
        });
        return context.Response.WriteAsync(body);
    }
}
