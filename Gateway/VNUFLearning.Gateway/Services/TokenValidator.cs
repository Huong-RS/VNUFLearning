using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace VNUFLearning.Gateway.Services;

/// <summary>
/// Validates JWT tokens locally (signature + expiry).
/// </summary>
public class TokenValidator
{
    private readonly IConfiguration _config;
    private readonly ILogger<TokenValidator> _logger;

    public TokenValidator(IConfiguration config, ILogger<TokenValidator> logger)
    {
        _config = config;
        _logger = logger;
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var secret = _config["Gateway:JwtSecret"]
                ?? throw new InvalidOperationException("Gateway:JwtSecret not configured");
            var issuer = _config["Gateway:JwtIssuer"] ?? "VNUFLearning";
            var audience = _config["Gateway:JwtAudience"] ?? "VNUFLearning";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("Token expired");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Token validation failed: {Msg}", ex.Message);
            return null;
        }
    }

    public string? GetClaim(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;
}
