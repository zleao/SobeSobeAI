# gRPC Real-Time Game Events

## Overview

The SobeSobe game uses gRPC for real-time bidirectional communication between clients and the server. This allows players to receive instant notifications about game events without polling.

## Service Definition

The `GameEvents` gRPC service is defined in `backend/SobeSobe.Api/Protos/game-events.proto` and provides three RPC methods:

1. **Subscribe** - Subscribe to a stream of game events for a specific game
2. **SendAction** - Send player actions (not yet implemented - use REST API for now)
3. **Heartbeat** - Keep connection alive and check server status

## Subscribing to Game Events

### Authentication

All gRPC calls require a valid JWT access token obtained from the login endpoint.

### Endpoint

```
https://localhost:7123/sobesobe.game.GameEvents/Subscribe
```

### Request

```protobuf
message SubscribeRequest {
  string game_id = 1;      // Game ID to subscribe to
  string access_token = 2;  // JWT access token
}
```

### Response Stream

The server returns a stream of `GameEvent` messages:

```protobuf
message GameEvent {
  string game_id = 1;
  EventType type = 2;
  google.protobuf.Timestamp timestamp = 3;
  
  oneof payload {
    PlayerJoinedEvent player_joined = 10;
    PlayerLeftEvent player_left = 11;
    GameStartedEvent game_started = 12;
    RoundStartedEvent round_started = 13;
    TrumpSelectedEvent trump_selected = 14;
    CardDealtEvent card_dealt = 15;
    PlayDecisionEvent play_decision = 16;
    CardsExchangedEvent cards_exchanged = 17;
    CardPlayedEvent card_played = 18;
    TrickCompletedEvent trick_completed = 19;
    RoundCompletedEvent round_completed = 20;
    GameCompletedEvent game_completed = 21;
    PlayerTurnEvent player_turn = 22;
    ErrorEvent error = 99;
  }
}
```

## Event Types

### PLAYER_JOINED (0)
Broadcast when a player joins the game.

**Payload:**
- `user_id` - User ID of the player
- `username` - Username
- `display_name` - Display name
- `position` - Player position (0-4)

### PLAYER_LEFT (1)
Broadcast when a player leaves the game.

**Payload:**
- `user_id` - User ID of the player
- `position` - Player position

### GAME_STARTED (2)
Broadcast when the game starts.

**Payload:**
- `started_at` - Timestamp when game started
- `dealer_position` - Position of the dealer
- `players` - List of all players with current points

### TRUMP_SELECTED (4)
Broadcast when the party player selects trump suit.

**Payload:**
- `trump_suit` - Selected trump suit (Hearts, Diamonds, Clubs, Spades)
- `selected_before_dealing` - Whether trump was selected blind
- `trick_value` - Points per trick (1, 2, or 4)

### CARD_PLAYED (8)
Broadcast when a player plays a card.

**Payload:**
- `position` - Player position who played
- `card` - Card that was played (rank and suit)
- `trick_number` - Current trick number (1-5)

### TRICK_COMPLETED (9)
Broadcast when a trick is completed.

**Payload:**
- `trick_number` - Trick number (1-5)
- `winner_position` - Position of the player who won the trick
- `cards_played` - All cards played in the trick

### ROUND_COMPLETED (10)
Broadcast when a round completes (after 5 tricks).

**Payload:**
- `round_id` - Round ID
- `round_number` - Round number
- `scores` - Score changes for all players

### GAME_COMPLETED (11)
Broadcast when the game is won.

**Payload:**
- `game_id` - Game ID
- `winner_position` - Position of the winner
- `winner_user_id` - User ID of the winner
- `completed_at` - Timestamp
- `final_scores` - Final scores and prizes for all players

## Error Handling

### Authentication Errors

- **UNAUTHENTICATED** - Invalid or expired access token
- **NOT_FOUND** - Game not found
- **PERMISSION_DENIED** - User is not a player in the game

### Connection Errors

If the connection is lost, the client should:
1. Attempt to reconnect with exponential backoff
2. Re-subscribe to the game
3. Use the REST API `GET /api/games/{id}/state` endpoint to get the current game state

## Client Implementation Example (C#)

```csharp
using Grpc.Net.Client;
using SobeSobe.Api.Protos;

// Create gRPC channel
var channel = GrpcChannel.ForAddress("https://localhost:7123");
var client = new GameEvents.GameEventsClient(channel);

// Subscribe to game events
var request = new SubscribeRequest
{
    GameId = gameId,
    AccessToken = jwtToken
};

using var call = client.Subscribe(request);

// Read events from stream
await foreach (var gameEvent in call.ResponseStream.ReadAllAsync())
{
    switch (gameEvent.Type)
    {
        case EventType.PlayerJoined:
            Console.WriteLine($"Player {gameEvent.PlayerJoined.Username} joined");
            break;
            
        case EventType.CardPlayed:
            Console.WriteLine($"Player at position {gameEvent.CardPlayed.Position} played {gameEvent.CardPlayed.Card.Rank} of {gameEvent.CardPlayed.Card.Suit}");
            break;
            
        case EventType.TrickCompleted:
            Console.WriteLine($"Trick {gameEvent.TrickCompleted.TrickNumber} won by player at position {gameEvent.TrickCompleted.WinnerPosition}");
            break;
            
        // Handle other event types...
    }
}
```

## Broadcasting Events from REST Endpoints

The backend uses helper extension methods in `GameEventExtensions` to broadcast events:

```csharp
using SobeSobe.Api.Extensions;

// Broadcast player joined
await GameEventExtensions.BroadcastPlayerJoinedAsync(
    gameId: game.Id.ToString(),
    userId: user.Id.ToString(),
    username: user.Username,
    displayName: user.DisplayName,
    position: playerSession.Position
);

// Broadcast card played
await GameEventExtensions.BroadcastCardPlayedAsync(
    gameId: game.Id.ToString(),
    position: playerSession.Position,
    rank: card.Rank,
    suit: card.Suit,
    trickNumber: trick.TrickNumber
);
```

## Integration Status

### ✅ Implemented
- gRPC service infrastructure
- Subscribe endpoint with authentication
- Heartbeat endpoint
- Event broadcasting system
- Helper extension methods for all event types

### 🚧 Partially Integrated
- REST endpoints need to be updated to broadcast events
- Event broadcasting currently available but not yet called from endpoints

### ❌ Not Yet Implemented
- SendAction RPC method (use REST API for actions)
- Browser-compatible gRPC-Web support (requires gRPC-Web proxy or SignalR fallback)

## Next Steps

1. Add event broadcasting calls to existing REST endpoints:
   - POST /api/games/{id}/join → BroadcastPlayerJoinedAsync
   - POST /api/games/{id}/leave → BroadcastPlayerLeftAsync
   - POST /api/games/{id}/start → BroadcastGameStartedAsync
   - POST /api/games/{id}/rounds/current/trump → BroadcastTrumpSelectedAsync
   - POST /api/games/{id}/rounds/current/play-card → BroadcastCardPlayedAsync, BroadcastTrickCompletedAsync
   - Complete round → BroadcastRoundCompletedAsync
   - Complete game → BroadcastGameCompletedAsync

2. Implement SignalR hub as fallback for browser clients (gRPC-Web alternative)

3. Add connection recovery and reconnection logic in clients

4. Add performance monitoring for gRPC streams

## Testing

### Manual Testing with grpcurl

```bash
# Subscribe to game events
grpcurl -d '{"game_id": "your-game-id", "access_token": "your-jwt-token"}' \
  -plaintext localhost:7123 \
  sobesobe.game.GameEvents/Subscribe

# Heartbeat
grpcurl -d '{"game_id": "your-game-id"}' \
  -plaintext localhost:7123 \
  sobesobe.game.GameEvents/Heartbeat
```

### Testing with .NET Client

See the client implementation example above.

## Security Considerations

- All gRPC calls require valid JWT authentication
- Only players in a game can subscribe to that game's events
- Access tokens are validated on every Subscribe call
- Connection automatically closes on authentication failure
- Sensitive information (other players' hands) is not broadcast

## Performance Notes

- The server can handle multiple concurrent subscribers per game
- Events are broadcast asynchronously to all subscribers
- Failed write attempts to disconnected clients are handled gracefully
- Consider implementing subscriber cleanup for long-lived games
