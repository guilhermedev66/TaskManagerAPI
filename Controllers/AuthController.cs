using Microsoft.AspNetCore.Mvc;
using TaskManagerAPI.Models;
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
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var token = await _authService.LoginAsync(request.Username, request.Password, cancellationToken);
            if (token is null)
            {
                return Problem(
                    detail: "Credenciais inválidas.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Ok(new AuthResponse { Token = token });
        }
    }
}
