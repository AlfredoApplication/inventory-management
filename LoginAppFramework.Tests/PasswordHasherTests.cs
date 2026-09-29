using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void HashAndVerify_RoundTripSucceeds()
    {
        const string password = "Correct-Horse-Battery-Staple-42";

        string hash = PasswordHasher.HashPassword(password);

        Assert.NotEqual(password, hash);
        Assert.True(PasswordHasher.VerifyPassword(hash, password));
        Assert.False(PasswordHasher.VerifyPassword(hash, password + "!"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    public void VerifyPassword_InvalidHashReturnsFalse(string hash)
    {
        Assert.False(PasswordHasher.VerifyPassword(hash, "password"));
    }
}
