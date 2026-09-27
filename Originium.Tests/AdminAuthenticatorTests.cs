using Xunit;
using Originium.Datas;
using Originium.Services;

namespace Originium.Tests;

public class AdminAuthenticatorTests
{
    [Fact]
    public void IsEnabled_WithPassword_ReturnsTrue()
    {
        var config = new Config { AdminPassword = "secret123" };
        var auth = new AdminAuthenticator(config);
        Assert.True(auth.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WithEmptyPassword_ReturnsFalse()
    {
        var config = new Config { AdminPassword = "" };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WithNullPassword_ReturnsFalse()
    {
        var config = new Config { AdminPassword = null };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.IsEnabled);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var config = new Config { AdminPassword = "secret123" };
        var auth = new AdminAuthenticator(config);
        Assert.True(auth.Verify("secret123"));
    }

    [Fact]
    public void Verify_IncorrectPassword_ReturnsFalse()
    {
        var config = new Config { AdminPassword = "secret123" };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.Verify("wrong"));
    }

    [Fact]
    public void Verify_NullPassword_ReturnsFalse()
    {
        var config = new Config { AdminPassword = "secret123" };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.Verify(null));
    }

    [Fact]
    public void Verify_EmptyPassword_ReturnsFalse()
    {
        var config = new Config { AdminPassword = "secret123" };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.Verify(string.Empty));
    }

    [Fact]
    public void Verify_DisabledAuth_ReturnsFalse()
    {
        var config = new Config { AdminPassword = "" };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.Verify("anything"));
    }

    [Fact]
    public void Verify_SameLengthButDifferent_ReturnsFalse()
    {
        var config = new Config { AdminPassword = "abc" };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.Verify("def"));
    }

    [Fact]
    public void Verify_DifferentLength_ReturnsFalse()
    {
        var config = new Config { AdminPassword = "abc" };
        var auth = new AdminAuthenticator(config);
        Assert.False(auth.Verify("abcd"));
    }

    [Fact]
    public void Verify_LongPassword_CorrectlyVerified()
    {
        var password = new string('x', 100);
        var config = new Config { AdminPassword = password };
        var auth = new AdminAuthenticator(config);
        Assert.True(auth.Verify(password));
    }

    [Fact]
    public void Verify_SpecialCharacters_CorrectlyVerified()
    {
        var config = new Config { AdminPassword = "!@#$%^&*()" };
        var auth = new AdminAuthenticator(config);
        Assert.True(auth.Verify("!@#$%^&*()"));
    }

    [Fact]
    public void Verify_UnicodeCharacters_CorrectlyVerified()
    {
        var config = new Config { AdminPassword = "密码123🎉" };
        var auth = new AdminAuthenticator(config);
        Assert.True(auth.Verify("密码123🎉"));
    }
}
