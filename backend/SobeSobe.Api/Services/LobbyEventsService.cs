using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SobeSobe.Api.Protos;
using SobeSobe.Infrastructure.Data;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace SobeSobe.Api.Services;

/// <summary>
/// gRPC service for streaming lobby events to connected clients.
/// </summary>
public class LobbyEventsService : LobbyEvents.LobbyEventsBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LobbyEventsService> _logger;

    private static readonly ConcurrentBag<IServerStreamWriter<LobbyEvent>> _subscribers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyEventsService"/> class.
    /// </summary>
    public LobbyEventsService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<LobbyEventsService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Subscribes the caller to lobby events.
    /// </summary>
    public override async Task SubscribeLobby(
        LobbySubscribeRequest request,
        IServerStreamWriter<LobbyEvent> responseStream,
        ServerCallContext context)
    {
        try
        {
            var accessToken = ExtractAccessToken(request.AccessToken, context);
            if (accessToken is null)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing access token"));
            }

            var userId = await ValidateAccessTokenAsync(accessToken);
            if (userId is null)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid access token"));
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id.ToString().Equals(userId, StringComparison.InvariantCultureIgnoreCase));
            if (!userExists)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
            }

            _logger.LogInformation("User {UserId} subscribed to lobby events", userId);

            _subscribers.Add(responseStream);

            await responseStream.WriteAsync(new LobbyEvent
            {
                Type = LobbyEventType.LobbyListChanged,
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
            });

            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, context.CancellationToken);
            }

            _logger.LogInformation("User {UserId} unsubscribed from lobby events", userId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected from lobby events");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SubscribeLobby");
            throw;
        }
    }

    /// <summary>
    /// Broadcasts a lobby event to all connected subscribers.
    /// </summary>
    public static async Task BroadcastLobbyEventAsync(LobbyEvent lobbyEvent)
    {
        if (_subscribers.IsEmpty)
        {
            return;
        }

        var tasks = new List<Task>();
        foreach (var stream in _subscribers)
        {
            tasks.Add(stream.WriteAsync(lobbyEvent));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Validates a JWT access token and returns the user ID when valid.
    /// </summary>
    private async Task<string?> ValidateAccessTokenAsync(string accessToken)
    {
        try
        {
            var jwtSecret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured");
            var jwtAudience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience not configured");

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };

            var principal = await tokenHandler.ValidateTokenAsync(accessToken, validationParameters);
            if (!principal.IsValid)
            {
                return null;
            }

            return FindUserIdClaim(principal.ClaimsIdentity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate access token");
            return null;
        }
    }

    private static string? FindUserIdClaim(System.Security.Claims.ClaimsIdentity? identity)
    {
        if (identity is null)
        {
            return null;
        }

        return identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? identity.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? identity.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
               ?? identity.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
    }

    private static string? ExtractAccessToken(string? accessToken, ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var httpHeader = httpContext?.Request.Headers.Authorization.ToString();
        var token = NormalizeAccessToken(httpHeader);
        if (token is not null)
        {
            return token;
        }

        var httpFallback = httpContext?.Request.Headers["x-access-token"].ToString();
        token = NormalizeAccessToken(httpFallback);
        if (token is not null)
        {
            return token;
        }

        var header = context.RequestHeaders.FirstOrDefault(h => h.Key == "authorization").Value;
        token = NormalizeAccessToken(header);
        if (token is not null)
        {
            return token;
        }

        var metadataFallback = context.RequestHeaders.FirstOrDefault(h => h.Key == "x-access-token").Value;
        token = NormalizeAccessToken(metadataFallback);
        if (token is not null)
        {
            return token;
        }

        return NormalizeAccessToken(accessToken);
    }

    private static string? NormalizeAccessToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var token = accessToken.Trim();
        const string bearerPrefix = "Bearer ";
        if (token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[bearerPrefix.Length..].Trim();
        }

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
