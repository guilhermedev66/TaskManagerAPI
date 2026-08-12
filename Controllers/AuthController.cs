using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManagerAPI.Models;
using TaskManagerAPI.Security;
using TaskManagerAPI.Services;

namespace TaskManagerAPI.Controllers
{
    [ApiController]
    [Route("api")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        [EnableRateLimiting(RateLimitPolicies.Authentication)]
        public async Task<ActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            var created = await _authService.RegisterAsync(request.Username, request.Password, cancellationToken);
            if (!created)
            {
                return Problem(
                    detail: "Usuário já existe.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Ok(new { message = "Usuário registrado com sucesso." });
        }

        [HttpPost("login")]
        [EnableRateLimiting(RateLimitPolicies.Authentication)]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var tokens = await _authService.LoginAsync(request.Username, request.Password, cancellationToken);
            if (tokens is null)
            {
                return Problem(
                    detail: "Credenciais inválidas.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Ok(new AuthResponse { Token = tokens.AccessToken, RefreshToken = tokens.RefreshToken });
        }

        [HttpPost("refresh")]
        [EnableRateLimiting(RateLimitPolicies.RefreshToken)]
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
        {
            var tokens = await _authService.RefreshAsync(request.RefreshToken, cancellationToken);
            if (tokens is null)
            {
                return Problem(
                    detail: "Refresh token inválido.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Ok(new AuthResponse { Token = tokens.AccessToken, RefreshToken = tokens.RefreshToken });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
        {
            await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
            return NoContent();
        }
    }
}
