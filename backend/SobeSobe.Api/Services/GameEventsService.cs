using Grpc.Core;
using SobeSobe.Api.Protos;
using SobeSobe.Infrastructure.Data;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SobeSobe.Api.Services;

/// <summary>
/// gRPC service for streaming game events to connected clients
/// </summary>
public class GameEventsService : GameEvents.GameEventsBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GameEventsService> _logger;
    
    // Thread-safe dictionary to track subscribers by game ID
    // Each game can have multiple subscribers (players watching the game)
    private static readonly ConcurrentDictionary<string, ConcurrentBag<IServerStreamWriter<GameEvent>>> 
        _subscribers = new();

    public GameEventsService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<GameEventsService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to game events stream
    /// </summary>
    public override async Task Subscribe(
        SubscribeRequest request,
        IServerStreamWriter<GameEvent> responseStream,
        ServerCallContext context)
    {
        try
        {
            // Validate access token and extract user ID
            var userId = await ValidateAccessTokenAsync(request.AccessToken);
            if (userId == null)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid access token"));
            }

            // Verify game exists
            var gameExists = await _context.Games.AnyAsync(g => g.Id.ToString() == request.GameId);
            if (!gameExists)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Game not found"));
            }

            // Verify user is a player in the game
            var isPlayer = await _context.PlayerSessions
                .AnyAsync(ps => ps.GameId.ToString() == request.GameId && ps.UserId.ToString() == userId);
            
            if (!isPlayer)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "You are not a player in this game"));
            }

            _logger.LogInformation("User {UserId} subscribed to game {GameId}", userId, request.GameId);

            // Add this stream to the subscribers for this game
            var subscribers = _subscribers.GetOrAdd(request.GameId, _ => new ConcurrentBag<IServerStreamWriter<GameEvent>>());
            subscribers.Add(responseStream);

            // Send initial connection confirmation event
            await responseStream.WriteAsync(new GameEvent
            {
                GameId = request.GameId,
                Type = EventType.Error, // Using ERROR as a generic notification type
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                Error = new ErrorEvent
                {
                    ErrorCode = "CONNECTED",
                    Message = "Successfully connected to game event stream"
                }
            });

            // Keep the stream open until client disconnects or cancellation is requested
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, context.CancellationToken);
            }

            _logger.LogInformation("User {UserId} unsubscribed from game {GameId}", userId, request.GameId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected from game {GameId}", request.GameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Subscribe for game {GameId}", request.GameId);
            throw;
        }
        finally
        {
            // Remove this stream from subscribers when connection closes
            if (_subscribers.TryGetValue(request.GameId, out var subscribers))
            {
                // Note: ConcurrentBag doesn't support removal, so we'll leave it
                // In production, consider using a different data structure or cleanup mechanism
            }
        }
    }

    /// <summary>
    /// Send player action (alternative to REST API)
    /// </summary>
    public override Task<ActionResponse> SendAction(PlayerAction request, ServerCallContext context)
    {
        // TODO: Implement action handling
        // For now, we'll keep actions going through the REST API
        // This method can be implemented later for full bidirectional communication
        
        return Task.FromResult(new ActionResponse
        {
            Success = false,
            ErrorCode = "NOT_IMPLEMENTED",
            ErrorMessage = "Action handling via gRPC is not yet implemented. Please use REST API endpoints."
        });
    }

    /// <summary>
    /// Heartbeat to keep connection alive
    /// </summary>
    public override Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HeartbeatResponse
        {
            ServerTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        });
    }

    /// <summary>
    /// Broadcast an event to all subscribers of a game
    /// </summary>
    public static async Task BroadcastGameEventAsync(string gameId, GameEvent gameEvent)
    {
        if (_subscribers.TryGetValue(gameId, out var subscribers))
        {
            var tasks = new List<Task>();
            
            foreach (var stream in subscribers)
            {
                tasks.Add(stream.WriteAsync(gameEvent));
            }

            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Validate JWT access token and extract user ID
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

            var userId = principal.ClaimsIdentity?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate access token");
            return null;
        }
    }
}
