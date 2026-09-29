using System.Text.Json;
using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class ConnectionSettingsTests
{
    [Fact]
    public void Serialization_DoesNotPersistPlainTextSqlPassword()
    {
        var settings = new ConnectionSettings
        {
            ServerAddress = "db-server",
            SqlUsername = "inventory-user",
            SqlPassword = "plain-secret",
            EncryptedSqlPassword = "encrypted-value"
        };

        string json = JsonSerializer.Serialize(settings);

        Assert.DoesNotContain("plain-secret", json);
        Assert.Contains("encrypted-value", json);
        Assert.Contains("db-server", json);
        Assert.Contains("inventory-user", json);
    }

    [Fact]
    public void LoginResultFailure_PreservesReasonAndMessage()
    {
        var result = LoginResult.Fail(
            LoginFailureReason.DatabaseUnavailable,
            "Database unavailable");

        Assert.False(result.Success);
        Assert.Equal(LoginFailureReason.DatabaseUnavailable, result.FailureReason);
        Assert.Equal("Database unavailable", result.Message);
    }
}
