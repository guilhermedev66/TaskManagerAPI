using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;
using TaskManagerAPI.Security;

namespace TaskManagerAPI.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly JwtOptions _jwtOptions;

        public AuthService(AppDbContext context, IOptions<JwtOptions> jwtOptions)
        {
            _context = context;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<bool> RegisterAsync(string username, string password, CancellationToken cancellationToken)
        {
            var normalizedUsername = Normalize(username);
            var userExists = await _context.Users.AnyAsync(u => u.Username == normalizedUsername, cancellationToken);
            if (userExists)
            {
                return false;
            }

            var user = new User
            {
                Username = normalizedUsername,
                PasswordHash = PasswordHasher.Hash(password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<string?> LoginAsync(string username, string password, CancellationToken cancellationToken)
        {
            var normalizedUsername = Normalize(username);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername, cancellationToken);

            if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, out var needsRehash))
            {
                return null;
            }

            if (needsRehash)
            {
                user.PasswordHash = PasswordHasher.Hash(password);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return GenerateToken(user);
        }

        private string GenerateToken(User user)
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
                expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiresInMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string Normalize(string username) => username.Trim().ToLowerInvariant();
    }
}
