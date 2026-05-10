using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VNUFLearning.Filters;

/// <summary>
/// Yêu cầu header X-Api-Key trùng với Gateway:ApiKey trong appsettings.
/// Áp dụng cho controller/action API để chỉ chấp nhận traffic đến từ Gateway.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
{
    private const string HeaderName = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var required = config.GetValue<bool>("Gateway:RequireApiKeyOnApi");
        var expected = config["Gateway:ApiKey"];

        // Tắt check trong môi trường không cấu hình (cho phép dev gọi trực tiếp)
        if (!required || string.IsNullOrWhiteSpace(expected))
        {
            await next();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !string.Equals(provided.ToString(), expected, StringComparison.Ordinal))
        {
            context.Result = new ObjectResult(new
            {
                success = false,
                message = "Yêu cầu phải đến từ Gateway hợp lệ."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        await next();
    }
}
