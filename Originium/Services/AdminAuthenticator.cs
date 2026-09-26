using System.Security.Cryptography;
using System.Text;
using Originium.Datas;

namespace Originium.Services;

public class AdminAuthenticator(Config config)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(config.AdminPassword ?? string.Empty);

    public bool IsEnabled => _expected.Length > 0;

    public bool Verify(string? password)
    {
        if (!IsEnabled || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var supplied = Encoding.UTF8.GetBytes(password);
        return _expected.Length == supplied.Length
            && CryptographicOperations.FixedTimeEquals(_expected, supplied);
    }
}
