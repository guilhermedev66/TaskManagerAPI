using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;
using TaskManagerAPI.Security;

namespace TaskManagerAPI.Services
{
    public sealed record AuthTokens(string AccessToken, string RefreshToken);

    public class AuthService
    {
        private const int RefreshTokenByteLength = 32;

        private readonly AppDbContext _context;
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AppDbContext context, IOptions<JwtOptions> jwtOptions, ILogger<AuthService> logger)
        {
            _context = context;
            _jwtOptions = jwtOptions.Value;
            _logger = logger;
        }

        public async Task<bool> RegisterAsync(string username, string password, CancellationToken cancellationToken)
        {
            var normalizedUsername = Normalize(username);
            var userExists = await _context.Users.AnyAsync(u => u.Username == normalizedUsername, cancellationToken);
            if (userExists)
            {
                _logger.LogWarning("Registration rejected because the normalized username already exists");
                return false;
            }

            var user = new User
            {
                Username = normalizedUsername,
                PasswordHash = PasswordHasher.Hash(password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("User {UserId} registered", user.Id);
            return true;
        }

        public async Task<AuthTokens?> LoginAsync(string username, string password, CancellationToken cancellationToken)
        {
            var normalizedUsername = Normalize(username);
            var now = DateTime.UtcNow;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername, cancellationToken);

            if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, out var needsRehash))
            {
                _logger.LogWarning("Login rejected due to invalid credentials");
                return null;
            }

            if (needsRehash)
            {
                user.PasswordHash = PasswordHasher.Hash(password);
            }

            var refreshBytes = GenerateRefreshTokenBytes();
            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                FamilyId = Guid.NewGuid(),
                TokenHash = HashToken(refreshBytes),
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenExpiresInDays),
                RevokedAt = null,
                ReplacedByTokenHash = null
            });
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("User {UserId} logged in and started a refresh token family", user.Id);

            var accessToken = GenerateAccessToken(user, now);
            var rawRefreshToken = WebEncoders.Base64UrlEncode(refreshBytes);
            return new AuthTokens(accessToken, rawRefreshToken);
        }

        public async Task<AuthTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            if (!TryDecodeToken(refreshToken, out var presentedBytes))
            {
                _logger.LogWarning("Refresh rejected because the token format is invalid");
                return null;
            }

            var presentedHash = HashToken(presentedBytes);
            var now = DateTime.UtcNow;

            var current = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken);

            if (current is null)
            {
                _logger.LogWarning("Refresh rejected because the token is unknown");
                return null;
            }

            if (current.RevokedAt is not null)
            {
                // token já consumido antes desta chamada: só é reuso (sinal de roubo) se veio de uma
                // rotação legítima. Se foi revogado por logout, ReplacedByTokenHash fica nulo e não
                // dispara a varredura da família.
                if (current.ReplacedByTokenHash is not null)
                {
                    _logger.LogWarning(
                        "Refresh token reuse detected for family {FamilyId}; revoking the family",
                        current.FamilyId);
                    await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
                }

                return null;
            }

            if (current.ExpiresAt <= now)
            {
                // expiração pura não é reuso — não mexe na família.
                _logger.LogInformation("Expired refresh token rejected for family {FamilyId}", current.FamilyId);
                return null;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == current.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("Refresh token references missing user {UserId}", current.UserId);
                return null;
            }

            var newBytes = GenerateRefreshTokenBytes();
            var newHash = HashToken(newBytes);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var rowsAffected = await _context.RefreshTokens
                .Where(t => t.Id == current.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.ReplacedByTokenHash, newHash), cancellationToken);

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);

                // outra requisição concorrente já rotacionou este token entre o SELECT acima e o
                // UPDATE condicional. Recarrega sem confiar na entidade lida antes da corrida.
                var reloaded = await _context.RefreshTokens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == current.Id, cancellationToken);

                if (reloaded is not null && reloaded.ReplacedByTokenHash is not null)
                {
                    _logger.LogWarning(
                        "Concurrent refresh reuse detected for family {FamilyId}; revoking the family",
                        reloaded.FamilyId);
                    await RevokeFamilyAsync(reloaded.FamilyId, now, cancellationToken);
                }

                return null;
            }

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = current.UserId,
                FamilyId = current.FamilyId,
                TokenHash = newHash,
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenExpiresInDays),
                RevokedAt = null,
                ReplacedByTokenHash = null
            });
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Refresh token rotated for user {UserId} in family {FamilyId}",
                current.UserId,
                current.FamilyId);

            var accessToken = GenerateAccessToken(user, now);
            var rawNewToken = WebEncoders.Base64UrlEncode(newBytes);
            return new AuthTokens(accessToken, rawNewToken);
        }

        public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
        {
            if (!TryDecodeToken(refreshToken, out var presentedBytes))
            {
                return;
            }

            var presentedHash = HashToken(presentedBytes);
            var now = DateTime.UtcNow;

            var current = await _context.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken);

            if (current is null)
            {
                return;
            }

            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            _logger.LogInformation(
                "Refresh token family {FamilyId} revoked during logout for user {UserId}",
                current.FamilyId,
                current.UserId);
        }

        private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        private string GenerateAccessToken(User user, DateTime now)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: now.AddMinutes(_jwtOptions.ExpiresInMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static bool TryDecodeToken(string token, out byte[] bytes)
        {
            try
            {
                bytes = WebEncoders.Base64UrlDecode(token);
            }
            catch (FormatException)
            {
                bytes = [];
                return false;
            }

            return bytes.Length == RefreshTokenByteLength;
        }

        private static byte[] GenerateRefreshTokenBytes() => RandomNumberGenerator.GetBytes(RefreshTokenByteLength);

        private static string HashToken(byte[] bytes) => Convert.ToBase64String(SHA256.HashData(bytes));

        private static string Normalize(string username) => username.Trim().ToLowerInvariant();
    }
}
