using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SobeSobe.Api.DTOs;
using SobeSobe.Core.Enums;
using SobeSobe.Infrastructure.Data;
using Xunit;

namespace SobeSobe.Tests.Integration;

[Collection("Integration Tests")]
public class GameManagementIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _accessToken1;
    private string? _accessToken2;
    private string? _accessToken3;
    private Guid _userId1;
    private Guid _userId2;
    private Guid _userId3;

    public GameManagementIntegrationTests()
    {
        _factory = new WebApplicationFactoryFixture();
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Reset database for clean slate
        var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();

        // Create three test users for multiplayer scenarios
        var registerResponse1 = await _client.PostAsJsonAsync("/api/users/register", new RegisterUserRequest
        {
            Username = "player1",
            Email = "player1@test.com",
            Password = "password123",
            DisplayName = "Player One"
        });
        var user1 = await registerResponse1.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        _userId1 = user1!.Id;

        var loginResponse1 = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UsernameOrEmail = "player1",
            Password = "password123"
        });
        var login1 = await loginResponse1.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        _accessToken1 = login1!.AccessToken;

        var registerResponse2 = await _client.PostAsJsonAsync("/api/users/register", new RegisterUserRequest
        {
            Username = "player2",
            Email = "player2@test.com",
            Password = "password123",
            DisplayName = "Player Two"
        });
        var user2 = await registerResponse2.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        _userId2 = user2!.Id;

        var loginResponse2 = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UsernameOrEmail = "player2",
            Password = "password123"
        });
        var login2 = await loginResponse2.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        _accessToken2 = login2!.AccessToken;

        var registerResponse3 = await _client.PostAsJsonAsync("/api/users/register", new RegisterUserRequest
        {
            Username = "player3",
            Email = "player3@test.com",
            Password = "password123",
            DisplayName = "Player Three"
        });
        var user3 = await registerResponse3.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        _userId3 = user3!.Id;

        var loginResponse3 = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UsernameOrEmail = "player3",
            Password = "password123"
        });
        var login3 = await loginResponse3.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        _accessToken3 = login3!.AccessToken;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Create game returns 201 with game details")]
    public async Task CreateGame_ValidRequest_ReturnsCreated()
    {
        // Setup
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);

        // Execute
        var response = await _client.SendAsync(request);

        // Verify
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);
        Assert.NotNull(game);
        Assert.Equal(5, game.MaxPlayers);
        Assert.Equal(SobeSobe.Core.Enums.GameStatus.Waiting, game.Status);
        Assert.Single(game.Players); // Creator auto-joins
        Assert.Equal(_userId1, game.Players[0].UserId);
        Assert.Equal(0, game.Players[0].Position);
        Assert.Equal(20, game.Players[0].CurrentPoints);
    }

    [Fact(DisplayName = "Create game without authentication returns 401")]
    public async Task CreateGame_NoAuth_ReturnsUnauthorized()
    {
        // Execute
        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequest { MaxPlayers = 5 });

        // Verify
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "List games returns paginated results")]
    public async Task ListGames_ReturnsGames()
    {
        // Setup - create multiple games
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 3 })
        };
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        await _client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 4 })
        };
        request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(request2);

        // Execute
        var response = await _client.GetAsync("/api/games?page=1&pageSize=10");

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listResponse = await response.Content.ReadFromJsonAsync<ListGamesResponse>(JsonOptions);
        Assert.NotNull(listResponse);
        Assert.Equal(2, listResponse.Pagination.TotalItems);
        Assert.Equal(1, listResponse.Pagination.TotalPages);
        Assert.Equal(2, listResponse.Games.Count);
    }

    [Fact(DisplayName = "Get game details returns full game state")]
    public async Task GetGame_ValidId_ReturnsGameDetails()
    {
        // Setup - create game
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        // Execute
        var response = await _client.GetAsync($"/api/games/{createdGame!.Id}");

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);
        Assert.NotNull(game);
        Assert.Equal(createdGame.Id, game.Id);
        Assert.Equal(_userId1, game.CreatedBy);
        Assert.Single(game.Players);
    }

    [Fact(DisplayName = "Get non-existent game returns 404")]
    public async Task GetGame_InvalidId_ReturnsNotFound()
    {
        // Execute
        var response = await _client.GetAsync($"/api/games/{Guid.NewGuid()}");

        // Verify
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Join game adds player to game")]
    public async Task JoinGame_ValidGame_AddsPlayer()
    {
        // Setup - player 1 creates game
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        // Execute - player 2 joins
        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        var response = await _client.SendAsync(joinRequest);

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var joinResponse = await response.Content.ReadFromJsonAsync<JoinGameResponse>(JsonOptions);
        Assert.NotNull(joinResponse);
        Assert.Equal(_userId2, joinResponse.PlayerSession.UserId);
        Assert.Equal(1, joinResponse.PlayerSession.Position); // Second player gets position 1
        Assert.Equal(20, joinResponse.PlayerSession.CurrentPoints);

        // Verify game now has 2 players
        var gameDetails = await _client.GetFromJsonAsync<GameResponse>($"/api/games/{game.Id}", JsonOptions);
        Assert.Equal(2, gameDetails!.Players.Count);
    }

    [Fact(DisplayName = "Join game without authentication returns 401")]
    public async Task JoinGame_NoAuth_ReturnsUnauthorized()
    {
        // Execute
        var response = await _client.PostAsync($"/api/games/{Guid.NewGuid()}/join", null);

        // Verify
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "Join game twice returns 409 Conflict")]
    public async Task JoinGame_AlreadyJoined_ReturnsConflict()
    {
        // Setup - create and join game
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest1 = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest1);

        // Execute - try to join again
        var joinRequest2 = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/join");
        joinRequest2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        var response = await _client.SendAsync(joinRequest2);

        // Verify
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "Join full game returns 400 Bad Request")]
    public async Task JoinGame_GameFull_ReturnsBadRequest()
    {
        // Setup - create game with max 2 players
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 2 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        // Player 2 joins (game now full)
        var joinRequest1 = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest1);

        // Execute - player 3 tries to join full game
        var joinRequest2 = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/join");
        joinRequest2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken3);
        var response = await _client.SendAsync(joinRequest2);

        // Verify
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Leave game removes player")]
    public async Task LeaveGame_ValidPlayer_RemovesFromGame()
    {
        // Setup - create game and join
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        // Execute - player 2 leaves
        var leaveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/leave");
        leaveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        var response = await _client.SendAsync(leaveRequest);

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify game now has only 1 player
        var gameDetails = await _client.GetFromJsonAsync<GameResponse>($"/api/games/{game.Id}", JsonOptions);
        Assert.Single(gameDetails!.Players);
        Assert.Equal(_userId1, gameDetails.Players[0].UserId);
    }

    [Fact(DisplayName = "Leave game as creator deletes game and removes all players")]
    public async Task LeaveGame_Creator_DeletesGame()
    {
        // Setup - create game, player 2 joins
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        // Execute - creator leaves
        var leaveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/leave");
        leaveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var response = await _client.SendAsync(leaveRequest);

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify game no longer exists
        var getResponse = await _client.GetAsync($"/api/games/{game.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(DisplayName = "Leave game as last player deletes game")]
    public async Task LeaveGame_LastPlayer_DeletesGame()
    {
        // Setup - create game
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        // Execute - last player leaves
        var leaveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/leave");
        leaveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var response = await _client.SendAsync(leaveRequest);

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify game no longer exists
        var getResponse = await _client.GetAsync($"/api/games/{game.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(DisplayName = "Leave game after start returns 400 Bad Request")]
    public async Task LeaveGame_GameStarted_ReturnsBadRequest()
    {
        // Setup - create game with 2 players
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/start");
        startRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        await _client.SendAsync(startRequest);

        // Execute - player 2 tries to leave after game started
        var leaveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/leave");
        leaveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        var response = await _client.SendAsync(leaveRequest);

        // Verify
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Cancel game as creator succeeds")]
    public async Task CancelGame_Creator_Succeeds()
    {
        // Setup - create game
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        // Execute - creator cancels
        var cancelRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/games/{game!.Id}");
        cancelRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var response = await _client.SendAsync(cancelRequest);

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify game no longer exists
        var getResponse = await _client.GetAsync($"/api/games/{game.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact(DisplayName = "Cancel game as non-creator returns 403")]
    public async Task CancelGame_NonCreator_ReturnsForbidden()
    {
        // Setup - create game, player 2 joins
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        // Execute - player 2 tries to cancel
        var cancelRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/games/{game.Id}");
        cancelRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        var response = await _client.SendAsync(cancelRequest);

        // Verify
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "Abandon game as creator sets status to Abandoned")]
    public async Task AbandonGame_Creator_UpdatesStatus()
    {
        // Setup - create game, player 2 joins, start game
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/start");
        startRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        await _client.SendAsync(startRequest);

        // Execute - creator abandons game
        var abandonRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/abandon");
        abandonRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var response = await _client.SendAsync(abandonRequest);

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var gameDetails = await _client.GetFromJsonAsync<GameResponse>($"/api/games/{game.Id}", JsonOptions);
        Assert.Equal((int)GameStatus.Abandoned, (int)gameDetails!.Status);
        Assert.NotNull(gameDetails.CompletedAt);
    }

    [Fact(DisplayName = "Abandon game as non-creator returns 403")]
    public async Task AbandonGame_NonCreator_ReturnsForbidden()
    {
        // Setup - create game, player 2 joins
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        // Execute - non-creator abandons
        var abandonRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/abandon");
        abandonRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        var response = await _client.SendAsync(abandonRequest);

        // Verify
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "Start game as creator succeeds")]
    public async Task StartGame_Creator_Succeeds()
    {
        // Setup - create game, player 2 joins
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        // Execute - creator starts game
        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/start");
        startRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var response = await _client.SendAsync(startRequest);

        // Verify
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var startResponse = await response.Content.ReadFromJsonAsync<StartGameResponse>(JsonOptions);
        Assert.NotNull(startResponse);
        Assert.Equal("InProgress", startResponse.Status);
        Assert.Equal(1, startResponse.CurrentRoundNumber);
    }

    [Fact(DisplayName = "Start game with one player returns 400")]
    public async Task StartGame_OnePlayer_ReturnsBadRequest()
    {
        // Setup - create game (only creator)
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        // Execute - try to start with only 1 player
        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/start");
        startRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var response = await _client.SendAsync(startRequest);

        // Verify
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Start game as non-creator returns 403")]
    public async Task StartGame_NonCreator_ReturnsForbidden()
    {
        // Setup - create game, player 2 joins
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/games")
        {
            Content = JsonContent.Create(new CreateGameRequest { MaxPlayers = 5 })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken1);
        var createResponse = await _client.SendAsync(createRequest);
        var game = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game!.Id}/join");
        joinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        await _client.SendAsync(joinRequest);

        // Execute - player 2 tries to start
        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{game.Id}/start");
        startRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken2);
        var response = await _client.SendAsync(startRequest);

        // Verify
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
