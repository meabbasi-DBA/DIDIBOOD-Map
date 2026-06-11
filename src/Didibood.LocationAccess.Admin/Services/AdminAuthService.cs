using System.Security.Cryptography;
using System.Text;
using Didibood.LocationAccess.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.Admin.Services;

public sealed class AdminAuthService(IOptions<AdminAuthOptions> options)
{
    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(options.Value.Username)
        && !string.IsNullOrWhiteSpace(options.Value.Password);

    public bool ValidateCredentials(string username, string password)
    {
        if (!IsEnabled)
            return false;

        var configured = options.Value;
        var usernameMatch = FixedTimeEquals(username, configured.Username);
        var passwordMatch = FixedTimeEquals(password, configured.Password);
        return usernameMatch && passwordMatch;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
