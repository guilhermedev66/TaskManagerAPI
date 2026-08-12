using System.Text;
using Microsoft.Extensions.Options;

namespace TaskManagerAPI.Security
{
    public class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        private const int MinKeyBytes = 32;

        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Key))
            {
                errors.Add("Jwt:Key é obrigatório.");
            }
            else if (Encoding.UTF8.GetByteCount(options.Key) < MinKeyBytes)
            {
                errors.Add($"Jwt:Key deve ter pelo menos {MinKeyBytes} bytes UTF-8.");
            }

            if (string.IsNullOrWhiteSpace(options.Issuer))
            {
                errors.Add("Jwt:Issuer é obrigatório.");
            }

            if (string.IsNullOrWhiteSpace(options.Audience))
            {
                errors.Add("Jwt:Audience é obrigatório.");
            }

            if (options.ExpiresInMinutes <= 0)
            {
                errors.Add("Jwt:ExpiresInMinutes deve ser maior que zero.");
            }

            if (options.RefreshTokenExpiresInDays <= 0)
            {
                errors.Add("Jwt:RefreshTokenExpiresInDays deve ser maior que zero.");
            }

            return errors.Count > 0
                ? ValidateOptionsResult.Fail(errors)
                : ValidateOptionsResult.Success;
        }
    }
}
