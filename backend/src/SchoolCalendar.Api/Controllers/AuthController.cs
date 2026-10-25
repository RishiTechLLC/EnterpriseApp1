using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolCalendar.Api.Infrastructure;

namespace SchoolCalendar.Api.Controllers;

/// <summary>
/// Development-only sign-in: issues a JWT for any user id so the Permify roles can be tried out.
/// In production, configure Jwt:Authority to point at a real identity provider and leave EnableDevTokens off.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IOptions<JwtSettings> settings) : ControllerBase
{
    public sealed record DevTokenRequest(string UserId);
    public sealed record TokenResponse(string AccessToken, string UserId, DateTime ExpiresAt);

    [AllowAnonymous]
    [HttpPost("dev-token")]
    public ActionResult<TokenResponse> DevToken(DevTokenRequest request)
    {
        var jwt = settings.Value;
        if (!jwt.EnableDevTokens || !string.IsNullOrWhiteSpace(jwt.Authority)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 64) return BadRequest();

        var expires = DateTime.UtcNow.AddHours(8);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: [new Claim("sub", request.UserId), new Claim("name", request.UserId)],
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));

        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), request.UserId, expires);
    }
}
