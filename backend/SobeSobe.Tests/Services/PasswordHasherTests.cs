using SobeSobe.Api.Services;

namespace SobeSobe.Tests.Services;

public class PasswordHasherTests
{
    #region HashPassword Tests

    [Fact(DisplayName = "HashPassword generates non-empty hash")]
    public void HashPassword_ValidPassword_GeneratesHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = PasswordHasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact(DisplayName = "HashPassword generates different hashes for same password")]
    public void HashPassword_SamePassword_GeneratesDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = PasswordHasher.HashPassword(password);
        var hash2 = PasswordHasher.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2); // Different salt each time
    }

    [Fact(DisplayName = "HashPassword generates 64-byte base64 string")]
    public void HashPassword_ValidPassword_Generates64ByteBase64()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = PasswordHasher.HashPassword(password);
        var hashBytes = Convert.FromBase64String(hash);

        // Assert
        Assert.Equal(48, hashBytes.Length); // 16-byte salt + 32-byte hash
    }

    [Theory(DisplayName = "HashPassword handles various password formats")]
    [InlineData("short")]
    [InlineData("VeryLongPasswordWith123NumbersAndSymbols!@#$%^&*()")]
    [InlineData("with spaces")]
    [InlineData("Émojis🔐🎮")]
    [InlineData("12345678")]
    public void HashPassword_VariousFormats_GeneratesHash(string password)
    {
        // Act
        var hash = PasswordHasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    #endregion

    #region VerifyPassword Tests

    [Fact(DisplayName = "VerifyPassword returns true for correct password")]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = PasswordHasher.HashPassword(password);

        // Act
        var isValid = PasswordHasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact(DisplayName = "VerifyPassword returns false for incorrect password")]
    public void VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = PasswordHasher.HashPassword(password);

        // Act
        var isValid = PasswordHasher.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(isValid);
    }

    [Fact(DisplayName = "VerifyPassword is case-sensitive")]
    public void VerifyPassword_CaseMismatch_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongCasePassword = "testpassword123!";
        var hash = PasswordHasher.HashPassword(password);

        // Act
        var isValid = PasswordHasher.VerifyPassword(wrongCasePassword, hash);

        // Assert
        Assert.False(isValid);
    }

    [Theory(DisplayName = "VerifyPassword handles various password formats")]
    [InlineData("short")]
    [InlineData("VeryLongPasswordWith123NumbersAndSymbols!@#$%^&*()")]
    [InlineData("with spaces")]
    [InlineData("Émojis🔐🎮")]
    [InlineData("12345678")]
    public void VerifyPassword_VariousFormats_VerifiesCorrectly(string password)
    {
        // Arrange
        var hash = PasswordHasher.HashPassword(password);

        // Act
        var isValid = PasswordHasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact(DisplayName = "VerifyPassword handles empty password")]
    public void VerifyPassword_EmptyPassword_VerifiesCorrectly()
    {
        // Arrange
        var password = "";
        var hash = PasswordHasher.HashPassword(password);

        // Act
        var isValid = PasswordHasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact(DisplayName = "VerifyPassword returns false for invalid hash format")]
    public void VerifyPassword_InvalidHashFormat_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var invalidHash = "NotAValidBase64Hash";

        // Act
        var isValid = PasswordHasher.VerifyPassword(password, invalidHash);

        // Assert
        Assert.False(isValid);
    }

    [Fact(DisplayName = "VerifyPassword returns false for too short hash")]
    public void VerifyPassword_TooShortHash_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var tooShortHash = Convert.ToBase64String(new byte[10]); // Less than 48 bytes

        // Act
        var isValid = PasswordHasher.VerifyPassword(password, tooShortHash);

        // Assert
        Assert.False(isValid);
    }

    #endregion

    #region Integration Tests

    [Fact(DisplayName = "Hash and verify workflow works end-to-end")]
    public void HashAndVerify_CompleteWorkflow_WorksCorrectly()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act: Hash the password
        var hash = PasswordHasher.HashPassword(password);

        // Act: Verify correct password
        var validResult = PasswordHasher.VerifyPassword(password, hash);

        // Act: Verify incorrect password
        var invalidResult = PasswordHasher.VerifyPassword("WrongPassword", hash);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.True(validResult);
        Assert.False(invalidResult);
    }

    [Fact(DisplayName = "Multiple hashes of same password all verify correctly")]
    public void MultipleHashes_SamePassword_AllVerifyCorrectly()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash1 = PasswordHasher.HashPassword(password);
        var hash2 = PasswordHasher.HashPassword(password);
        var hash3 = PasswordHasher.HashPassword(password);

        // Act & Assert: All hashes should verify the original password
        Assert.True(PasswordHasher.VerifyPassword(password, hash1));
        Assert.True(PasswordHasher.VerifyPassword(password, hash2));
        Assert.True(PasswordHasher.VerifyPassword(password, hash3));

        // Assert: Hashes should be different (different salts)
        Assert.NotEqual(hash1, hash2);
        Assert.NotEqual(hash2, hash3);
        Assert.NotEqual(hash1, hash3);
    }

    #endregion
}
