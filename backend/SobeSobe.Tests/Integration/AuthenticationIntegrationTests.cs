using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SobeSobe.Api.DTOs;
using SobeSobe.Infrastructure.Data;
using Xunit;

namespace SobeSobe.Tests.Integration;

/// <summary>
/// Integration tests for authentication endpoints
/// Validates complete authentication flow: register, login, refresh, logout
/// </summary>
public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactoryFixture>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactoryFixture _factory;

    public AuthenticationIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        // Nothing to do on initialization
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clear database after each test
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    [Fact(DisplayName = "Register new user returns 201 Created with user data")]
    public async Task RegisterUser_ValidRequest_ReturnsCreatedWithUserData()
    {
        // Arrange
        var request = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var userResponse = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(userResponse);
        Assert.Equal(request.Username, userResponse.Username);
        Assert.Equal(request.Email, userResponse.Email);
        Assert.Equal(request.DisplayName, userResponse.DisplayName);
        Assert.NotEqual(Guid.Empty, userResponse.Id);
    }

    [Fact(DisplayName = "Register with duplicate username returns 400 Bad Request")]
    public async Task RegisterUser_DuplicateUsername_ReturnsBadRequest()
    {
        // Arrange - Register first user
        var firstRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test1@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User 1"
        };
        await _client.PostAsJsonAsync("/api/users/register", firstRequest);

        // Act - Try to register with same username
        var secondRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test2@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User 2"
        };
        var response = await _client.PostAsJsonAsync("/api/users/register", secondRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Username already exists", content);
    }

    [Fact(DisplayName = "Register with duplicate email returns 400 Bad Request")]
    public async Task RegisterUser_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - Register first user
        var firstRequest = new RegisterUserRequest
        {
            Username = "testuser1",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User 1"
        };
        await _client.PostAsJsonAsync("/api/users/register", firstRequest);

        // Act - Try to register with same email
        var secondRequest = new RegisterUserRequest
        {
            Username = "testuser2",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User 2"
        };
        var response = await _client.PostAsJsonAsync("/api/users/register", secondRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email already exists", content);
    }

    [Fact(DisplayName = "Login with username returns 200 OK with access and refresh tokens")]
    public async Task Login_WithUsername_ReturnsTokens()
    {
        // Arrange - Register user
        var registerRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        // Act - Login with username
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "SecurePassword123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResponse);
        Assert.NotNull(loginResponse.AccessToken);
        Assert.NotNull(loginResponse.RefreshToken);
        Assert.Equal("Bearer", loginResponse.TokenType);
        Assert.True(loginResponse.ExpiresIn > 0);
        Assert.NotNull(loginResponse.User);
        Assert.Equal("testuser", loginResponse.User.Username);
    }

    [Fact(DisplayName = "Login with email returns 200 OK with tokens")]
    public async Task Login_WithEmail_ReturnsTokens()
    {
        // Arrange - Register user
        var registerRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        // Act - Login with email
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "test@example.com",
            Password = "SecurePassword123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResponse);
        Assert.NotNull(loginResponse.AccessToken);
        Assert.NotNull(loginResponse.RefreshToken);
    }

    [Fact(DisplayName = "Login with invalid credentials returns 401 Unauthorized")]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange - Register user
        var registerRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        // Act - Login with wrong password
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "WrongPassword!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // Note: Results.Unauthorized() returns no body by default
    }

    [Fact(DisplayName = "Refresh token returns new access and refresh tokens")]
    public async Task RefreshToken_ValidRefreshToken_ReturnsNewTokens()
    {
        // Arrange - Register and login
        var registerRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "SecurePassword123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);

        // Act - Refresh token
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        };
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        Assert.NotNull(refreshResponse);
        Assert.NotNull(refreshResponse.AccessToken);
        Assert.NotNull(refreshResponse.RefreshToken);
        Assert.NotEqual(loginData.AccessToken, refreshResponse.AccessToken);
        Assert.NotEqual(loginData.RefreshToken, refreshResponse.RefreshToken);
    }

    [Fact(DisplayName = "Refresh with invalid token returns 401 Unauthorized")]
    public async Task RefreshToken_InvalidToken_ReturnsUnauthorized()
    {
        // Act - Try to refresh with invalid token
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // Note: Results.Unauthorized() returns no body by default
    }

    [Fact(DisplayName = "Logout revokes refresh token")]
    public async Task Logout_ValidRefreshToken_RevokesToken()
    {
        // Arrange - Register and login
        var registerRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "SecurePassword123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);

        // Act - Logout
        var logoutRequest = new LogoutRequest
        {
            RefreshToken = loginData.RefreshToken
        };
        var response = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

        // Assert logout successful
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Try to refresh with revoked token
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        };
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact(DisplayName = "Get current user without authentication returns 401 Unauthorized")]
    public async Task GetCurrentUser_NoAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "Get current user with valid token returns user data")]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserData()
    {
        // Arrange - Register and login
        var registerRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "SecurePassword123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);

        // Act - Get current user with token
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginData.AccessToken);
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var userResponse = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(userResponse);
        Assert.Equal("testuser", userResponse.Username);
        Assert.Equal("test@example.com", userResponse.Email);
    }

    [Fact(DisplayName = "Complete authentication flow: register → login → refresh → logout")]
    public async Task CompleteAuthenticationFlow_AllStepsSucceed()
    {
        // Step 1: Register
        var registerRequest = new RegisterUserRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/users/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        // Step 2: Login
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "SecurePassword123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);

        // Step 3: Get current user
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginData.AccessToken);
        var getUserResponse = await _client.GetAsync("/api/auth/user");
        Assert.Equal(HttpStatusCode.OK, getUserResponse.StatusCode);

        // Step 4: Refresh token
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        };
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshData = await refreshResponse.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        Assert.NotNull(refreshData);

        // Step 5: Logout
        var logoutRequest = new LogoutRequest
        {
            RefreshToken = refreshData.RefreshToken
        };
        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // Verify refresh token is revoked
        var revokedRefreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = refreshData.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefreshResponse.StatusCode);
    }
}

