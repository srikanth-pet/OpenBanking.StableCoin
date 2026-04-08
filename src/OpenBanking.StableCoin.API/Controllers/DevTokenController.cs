using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace OpenBanking.StableCoin.API.Controllers;

/// <summary>
/// Development-only endpoint for generating test JWT tokens.
/// NOT included in production builds.
/// </summary>
[ApiController]
[Route("api/dev")]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class DevTokenController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public DevTokenController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    /// <summary>
    /// Generate a development JWT token. Only available in Development environment.
    /// </summary>
    /// <param name="customerId">The customer ID to embed in the token (default: cust-001)</param>
    /// <param name="expiresInMinutes">Token lifetime in minutes (default: 60)</param>
    [HttpGet("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetDevToken(
        [FromQuery] string customerId = "cust-001",
        [FromQuery] int expiresInMinutes = 60)
    {
        if (!_env.IsDevelopment())
            return StatusCode(403, new { error = "This endpoint is only available in Development." });

        var signingKey = _config["Banking:Jwt:SigningKey"];
        if (string.IsNullOrEmpty(signingKey))
            return StatusCode(500, new { error = "Banking:Jwt:SigningKey is not configured." });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim("customer_id", customerId),
            new Claim(ClaimTypes.NameIdentifier, customerId),
            new Claim(ClaimTypes.Name, $"Test Customer ({customerId})"),
            new Claim("email", $"{customerId}@openbanking.dev"),
            new Claim(JwtRegisteredClaimNames.Sub, customerId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Banking:Jwt:Issuer"],
            audience: _config["Banking:Jwt:Audience"],
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(expiresInMinutes),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            token = tokenString,
            customerId,
            issuer = _config["Banking:Jwt:Issuer"],
            audience = _config["Banking:Jwt:Audience"],
            expiresAt = now.AddMinutes(expiresInMinutes).ToString("O"),
            usage = "Copy the token value and click 'Authorize' in Swagger UI, then paste it."
        });
    }
}
