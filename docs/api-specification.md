# SobeSobe API Specification

Complete API contracts for REST endpoints and gRPC services with OpenAPI and Protobuf specifications.

**Table of Contents**
- [API Overview](#api-overview)
- [Authentication](#authentication)
- [REST API Endpoints](#rest-api-endpoints)
  - [Authentication Endpoints](#authentication-endpoints)
  - [User Management Endpoints](#user-management-endpoints)
  - [Game Lobby Endpoints](#game-lobby-endpoints)
  - [Game Action Endpoints](#game-action-endpoints)
- [gRPC Services](#grpc-services)
- [Request/Response Models](#requestresponse-models)
- [Error Response Standards](#error-response-standards)
- [Validation Rules](#validation-rules)

---

## API Overview

**Base URL:** `https://api.sobesobe.com` (production) or `https://localhost:7001` (development)

**Protocols:**
- **REST API:** HTTP/JSON for stateless operations (authentication, game management)
- **gRPC:** For real-time bidirectional game events (card plays, trick completion, score updates)

**Authentication:** OAuth 2.0 / OpenID Connect with JWT Bearer tokens

**API Versioning:** Prefix all endpoints with `/api/v1`

---

## Authentication

All API requests (except `/auth/register` and `/auth/login`) require an `Authorization` header:

```
Authorization: Bearer <JWT_TOKEN>
```

**Token Expiry:**
- Access Token: 15 minutes
- Refresh Token: 7 days

---

## REST API Endpoints

### Authentication Endpoints

#### POST /api/auth/register
Register a new user account.

**Request Body:**
```json
{
  "username": "player123",
  "email": "player@example.com",
  "password": "SecureP@ssw0rd!",
  "displayName": "Player One"
}
```

**Response (201 Created):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "player123",
  "email": "player@example.com",
  "displayName": "Player One",
  "createdAt": "2026-02-02T13:00:00Z"
}
```

**Validation Rules:**
- Username: 3-20 characters, alphanumeric + underscore, unique
- Email: Valid email format, unique
- Password: Min 8 characters, must contain uppercase, lowercase, number, special char
- DisplayName: 1-50 characters

---

#### POST /api/auth/login
Authenticate and receive access/refresh tokens.

**Request Body:**
```json
{
  "usernameOrEmail": "player123",
  "password": "SecureP@ssw0rd!"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 900,
  "tokenType": "Bearer",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "player123",
    "displayName": "Player One",
    "avatarUrl": null
  }
}
```

---

#### POST /api/auth/refresh
Refresh access token using refresh token.

**Request Body:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "bmV3IHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```

---

#### POST /api/auth/logout
Invalidate refresh token and logout.

**Request Body:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

**Response (204 No Content)**

---

#### GET /api/auth/user
Get currently authenticated user profile.

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "player123",
  "email": "player@example.com",
  "displayName": "Player One",
  "avatarUrl": null,
  "createdAt": "2026-02-01T10:00:00Z",
  "lastLoginAt": "2026-02-02T13:00:00Z",
  "statistics": {
    "totalGamesPlayed": 42,
    "totalWins": 15,
    "totalPointsScored": 850,
    "totalPrizeWon": 12.50
  }
}
```

---

### User Management Endpoints

#### GET /api/users/{userId}
Get public user profile by ID.

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "player123",
  "displayName": "Player One",
  "avatarUrl": null,
  "statistics": {
    "totalGamesPlayed": 42,
    "totalWins": 15,
    "winRate": 0.357
  }
}
```

---

#### PUT /api/users/{userId}
Update user profile (own profile only).

**Request Body:**
```json
{
  "displayName": "Updated Name",
  "avatarUrl": "https://example.com/avatar.jpg"
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "player123",
  "displayName": "Updated Name",
  "avatarUrl": "https://example.com/avatar.jpg"
}
```

---

#### GET /api/users/{userId}/statistics
Get detailed game statistics for a user.

**Response (200 OK):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalGamesPlayed": 42,
  "totalWins": 15,
  "totalLosses": 27,
  "winRate": 0.357,
  "totalPointsScored": 850,
  "totalPrizeWon": 12.50,
  "averagePointsPerGame": 20.24,
  "bestGame": {
    "gameId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "finalPoints": 0,
    "prizeWon": 4.00,
    "playedAt": "2026-01-28T18:30:00Z"
  }
}
```

---

### Game Lobby Endpoints

#### GET /api/games
List available games with filtering and pagination.

**Query Parameters:**
- `status` (optional): Filter by game status (Waiting, InProgress, Completed)
- `createdBy` (optional): Filter by creator user ID
- `page` (default: 1): Page number
- `pageSize` (default: 20): Items per page

**Response (200 OK):**
```json
{
  "games": [
    {
      "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "createdBy": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "username": "player123",
        "displayName": "Player One"
      },
      "status": "Waiting",
      "maxPlayers": 5,
      "currentPlayers": 3,
      "players": [
        {
          "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "username": "player123",
          "displayName": "Player One",
          "position": 0
        },
        {
          "userId": "8d7e5432-1234-5678-9abc-def012345678",
          "username": "player456",
          "displayName": "Player Two",
          "position": 1
        },
        {
          "userId": "9e8f6543-2345-6789-abcd-ef0123456789",
          "username": "player789",
          "displayName": "Player Three",
          "position": 2
        }
      ],
      "createdAt": "2026-02-02T12:30:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalPages": 3,
    "totalItems": 52
  }
}
```

---

#### POST /api/games
Create a new game.

**Request Body:**
```json
{
  "maxPlayers": 5
}
```

**Response (201 Created):**
```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "createdBy": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "player123",
    "displayName": "Player One"
  },
  "status": "Waiting",
  "maxPlayers": 5,
  "currentPlayers": 1,
  "players": [
    {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "username": "player123",
      "displayName": "Player One",
      "position": 0,
      "currentPoints": 20
    }
  ],
  "createdAt": "2026-02-02T13:00:00Z"
}
```

**Validation Rules:**
- maxPlayers: Must be between 2 and 5
- Creator automatically joins at position 0

---

#### GET /api/games/{gameId}
Get detailed game information.

**Response (200 OK):**
```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "createdBy": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "player123",
    "displayName": "Player One"
  },
  "status": "InProgress",
  "maxPlayers": 5,
  "currentPlayers": 5,
  "currentDealerPosition": 2,
  "currentRoundNumber": 3,
  "players": [
    {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "username": "player123",
      "displayName": "Player One",
      "position": 0,
      "currentPoints": 15,
      "isActive": true,
      "consecutiveRoundsOut": 0
    }
  ],
  "createdAt": "2026-02-02T12:30:00Z",
  "startedAt": "2026-02-02T12:35:00Z"
}
```

---

#### POST /api/games/{gameId}/join
Join an existing game.

**Response (200 OK):**
```json
{
  "gameId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "playerSession": {
    "id": "4fb96f75-6828-5673-c4gd-3d074g77bgb7",
    "userId": "8d7e5432-1234-5678-9abc-def012345678",
    "position": 1,
    "currentPoints": 20,
    "isActive": true,
    "joinedAt": "2026-02-02T13:00:00Z"
  }
}
```

**Error Responses:**
- `400 Bad Request`: Game is full
- `400 Bad Request`: Game has already started
- `409 Conflict`: User already in this game

---

#### POST /api/games/{gameId}/leave
Leave a game (only before it starts or after abandoning).

**Response (204 No Content)**

**Error Responses:**
- `400 Bad Request`: Cannot leave game in progress
- `404 Not Found`: User not in this game

---

#### DELETE /api/games/{gameId}
Cancel a game (creator only, game must be in Waiting status).

**Response (204 No Content)**

**Error Responses:**
- `403 Forbidden`: Only creator can cancel game
- `400 Bad Request`: Cannot cancel game that has started

---

#### POST /api/games/{gameId}/start
Start the game (creator only, requires min 2 players).

**Response (200 OK):**
```json
{
  "gameId": "7c9e6679-7425-40de-944b-e07fc-1f90ae7",
  "status": "InProgress",
  "startedAt": "2026-02-02T13:00:00Z",
  "currentRoundNumber": 1,
  "currentDealerPosition": 0
}
```

**Error Responses:**
- `403 Forbidden`: Only creator can start game
- `400 Bad Request`: Need at least 2 players to start
- `400 Bad Request`: Game already started

---

### Game Action Endpoints

#### GET /api/games/{gameId}/state
Get current game state (includes current round, hands, tricks).

**Response (200 OK):**
```json
{
  "gameId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "status": "InProgress",
  "currentRoundNumber": 3,
  "currentRound": {
    "id": "5ga07f86-8939-6784-d5he-4e185h88chc8",
    "roundNumber": 3,
    "dealer": {
      "position": 2,
      "userId": "9e8f6543-2345-6789-abcd-ef0123456789",
      "displayName": "Player Three"
    },
    "partyPlayer": {
      "position": 1,
      "userId": "8d7e5432-1234-5678-9abc-def012345678",
      "displayName": "Player Two"
    },
    "trumpSuit": "Hearts",
    "trumpSelectedBeforeDealing": false,
    "trickValue": 2,
    "currentTrickNumber": 3,
    "status": "Playing",
    "activePlayers": [0, 1, 2, 3, 4]
  },
  "playerHand": {
    "cards": [
      {"rank": "Ace", "suit": "Hearts"},
      {"rank": "7", "suit": "Clubs"}
    ]
  },
  "currentTrick": {
    "trickNumber": 3,
    "leadPlayerPosition": 1,
    "cardsPlayed": [
      {"position": 1, "card": {"rank": "King", "suit": "Diamonds"}},
      {"position": 2, "card": {"rank": "Queen", "suit": "Diamonds"}}
    ]
  },
  "players": [
    {
      "position": 0,
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "displayName": "Player One",
      "currentPoints": 15,
      "isActive": true,
      "cardCount": 2
    }
  ]
}
```

**Notes:**
- `playerHand` only shows cards for the authenticated user
- Other players' hands show only `cardCount`
- `cardsPlayed` shows cards in the order they were played

---

#### POST /api/games/{gameId}/rounds/current/trump
Select trump suit (party player only, during TrumpSelection phase).

**Request Body:**
```json
{
  "trumpSuit": "Hearts",
  "selectedBeforeDealing": false
}
```

**Response (200 OK):**
```json
{
  "roundId": "5ga07f86-8939-6784-d5he-4e185h88chc8",
  "trumpSuit": "Hearts",
  "trumpSelectedBeforeDealing": false,
  "trickValue": 2
}
```

**Validation Rules:**
- `trumpSuit`: Must be one of: Hearts, Diamonds, Clubs, Spades
- `selectedBeforeDealing`: If true, trump must be Hearts and trickValue becomes 4
- Only party player can select trump
- Can only be called during TrumpSelection phase

**Error Responses:**
- `403 Forbidden`: Only party player can select trump
- `400 Bad Request`: Invalid trump suit
- `400 Bad Request`: Only Hearts can be selected before dealing
- `409 Conflict`: Wrong round phase

---

#### POST /api/games/{gameId}/rounds/current/play-decision
Decide whether to play in the current round (opt-in/opt-out).

**Request Body:**
```json
{
  "willPlay": true
}
```

**Response (200 OK):**
```json
{
  "roundId": "5ga07f86-8939-6784-d5he-4e185h88chc8",
  "playerSessionId": "4fb96f75-6828-5673-c4gd-3d074g77bgb7",
  "willPlay": true,
  "consecutiveRoundsOut": 0
}
```

**Validation Rules:**
- Cannot sit out more than 2 consecutive rounds
- Party player is automatically opted in (cannot opt out)
- Dealer is automatically opted in (cannot opt out)
- If trump is Clubs, all players must play

**Error Responses:**
- `400 Bad Request`: Already sat out 2 consecutive rounds
- `400 Bad Request`: Party player cannot opt out
- `400 Bad Request`: Dealer cannot opt out
- `400 Bad Request`: Clubs trump forces all players to play
- `409 Conflict`: Wrong round phase

---

#### POST /api/games/{gameId}/rounds/current/exchange-cards
Exchange up to 3 cards with the dealer (CardExchange phase).

**Request Body:**
```json
{
  "cardsToExchange": [
    {"rank": "2", "suit": "Diamonds"},
    {"rank": "3", "suit": "Clubs"}
  ]
}
```

**Response (200 OK):**
```json
{
  "roundId": "5ga07f86-8939-6784-d5he-4e185h88chc8",
  "newCards": [
    {"rank": "King", "suit": "Hearts"},
    {"rank": "Queen", "suit": "Spades"}
  ],
  "hand": [
    {"rank": "Ace", "suit": "Hearts"},
    {"rank": "7", "suit": "Clubs"},
    {"rank": "King", "suit": "Hearts"},
    {"rank": "Queen", "suit": "Spades"},
    {"rank": "5", "suit": "Diamonds"}
  ]
}
```

**Validation Rules:**
- Can exchange 0-3 cards
- Cards must be in player's hand
- Cannot exchange Ace of trump
- Only available during CardExchange phase
- Non-playing players cannot exchange

**Error Responses:**
- `400 Bad Request`: Cannot exchange more than 3 cards
- `400 Bad Request`: Card not in hand
- `400 Bad Request`: Cannot exchange Ace of trump
- `409 Conflict`: Wrong round phase

---

#### POST /api/games/{gameId}/rounds/current/play-card
Play a card during trick-taking phase.

**Request Body:**
```json
{
  "card": {
    "rank": "Ace",
    "suit": "Hearts"
  }
}
```

**Response (200 OK):**
```json
{
  "roundId": "5ga07f86-8939-6784-d5he-4e185h88chc8",
  "trickNumber": 3,
  "card": {"rank": "Ace", "suit": "Hearts"},
  "trickCompleted": false,
  "nextPlayerPosition": 1
}
```

**Response if trick completed (200 OK):**
```json
{
  "roundId": "5ga07f86-8939-6784-d5he-4e185h88chc8",
  "trickNumber": 3,
  "card": {"rank": "Ace", "suit": "Hearts"},
  "trickCompleted": true,
  "winner": {
    "position": 0,
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "displayName": "Player One"
  },
  "nextTrickLeader": 0,
  "roundCompleted": false
}
```

**Response if round completed (200 OK):**
```json
{
  "roundId": "5ga07f86-8939-6784-d5he-4e185h88chc8",
  "trickNumber": 5,
  "card": {"rank": "7", "suit": "Clubs"},
  "trickCompleted": true,
  "roundCompleted": true,
  "scores": [
    {
      "position": 0,
      "pointsChange": -4,
      "pointsAfter": 11,
      "tricksWon": 2
    },
    {
      "position": 1,
      "pointsChange": 10,
      "pointsAfter": 30,
      "tricksWon": 0,
      "penalty": true,
      "isPartyPlayer": true
    }
  ],
  "gameCompleted": false
}
```

**Validation Rules:**
- Card must be in player's hand
- Must follow suit if able (have cards of lead suit)
- If cannot follow suit:
  - Can play any card (trump or off-suit)
- If trump is led and player has trump:
  - Must play higher trump if able (escalation rule)
- Ace of trump must be played if:
  - Player has it and trump is led
  - Player cannot follow suit and has Ace of trump

**Error Responses:**
- `400 Bad Request`: Card not in hand
- `400 Bad Request`: Must follow suit
- `400 Bad Request`: Must play higher trump
- `400 Bad Request`: Must play Ace of trump
- `403 Forbidden`: Not your turn
- `409 Conflict`: Wrong round phase

---

#### GET /api/games/{gameId}/scores
Get complete score history for the game.

**Response (200 OK):**
```json
{
  "gameId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "players": [
    {
      "position": 0,
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "displayName": "Player One",
      "currentPoints": 11
    }
  ],
  "scoreHistory": [
    {
      "roundNumber": 1,
      "scores": [
        {
          "position": 0,
          "pointsChange": -2,
          "pointsAfter": 18,
          "reason": "TricksWon",
          "tricksWon": 2
        }
      ]
    },
    {
      "roundNumber": 2,
      "scores": [
        {
          "position": 0,
          "pointsChange": -4,
          "pointsAfter": 14,
          "reason": "TricksWon",
          "tricksWon": 2
        }
      ]
    }
  ]
}
```

---

## gRPC Services

### Service Definition (game-events.proto)

```protobuf
syntax = "proto3";

package sobesobe.game;

import "google/protobuf/timestamp.proto";

// Game events streaming service
service GameEvents {
  // Subscribe to game events for a specific game
  rpc Subscribe(SubscribeRequest) returns (stream GameEvent);
  
  // Send player action (alternative to REST API for real-time play)
  rpc SendAction(PlayerAction) returns (ActionResponse);
  
  // Heartbeat to keep connection alive
  rpc Heartbeat(HeartbeatRequest) returns (HeartbeatResponse);
}

// Subscribe to game events
message SubscribeRequest {
  string game_id = 1;
  string access_token = 2;
}

// Game event (server → client)
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

enum EventType {
  PLAYER_JOINED = 0;
  PLAYER_LEFT = 1;
  GAME_STARTED = 2;
  ROUND_STARTED = 3;
  TRUMP_SELECTED = 4;
  CARD_DEALT = 5;
  PLAY_DECISION = 6;
  CARDS_EXCHANGED = 7;
  CARD_PLAYED = 8;
  TRICK_COMPLETED = 9;
  ROUND_COMPLETED = 10;
  GAME_COMPLETED = 11;
  PLAYER_TURN = 12;
  ERROR = 99;
}

// Event: Player joined game
message PlayerJoinedEvent {
  string user_id = 1;
  string username = 2;
  string display_name = 3;
  int32 position = 4;
}

// Event: Player left game
message PlayerLeftEvent {
  string user_id = 1;
  int32 position = 2;
}

// Event: Game started
message GameStartedEvent {
  google.protobuf.Timestamp started_at = 1;
  int32 dealer_position = 2;
  repeated PlayerInfo players = 3;
}

// Event: New round started
message RoundStartedEvent {
  string round_id = 1;
  int32 round_number = 2;
  int32 dealer_position = 3;
  int32 party_player_position = 4;
}

// Event: Trump suit selected
message TrumpSelectedEvent {
  string trump_suit = 1;
  bool selected_before_dealing = 2;
  int32 trick_value = 3;
}

// Event: Card dealt to player (only sent to specific player)
message CardDealtEvent {
  repeated Card cards = 1;
}

// Event: Player decided to play or sit out
message PlayDecisionEvent {
  int32 position = 1;
  bool will_play = 2;
}

// Event: Player exchanged cards
message CardsExchangedEvent {
  int32 position = 1;
  int32 card_count = 2;
  // New cards only sent to the player who exchanged
  repeated Card new_cards = 3;
}

// Event: Card played
message CardPlayedEvent {
  int32 position = 1;
  Card card = 2;
  int32 trick_number = 3;
}

// Event: Trick completed
message TrickCompletedEvent {
  int32 trick_number = 1;
  int32 winner_position = 2;
  repeated CardPlay cards_played = 3;
}

// Event: Round completed
message RoundCompletedEvent {
  string round_id = 1;
  int32 round_number = 2;
  repeated RoundScore scores = 3;
}

// Event: Game completed
message GameCompletedEvent {
  string game_id = 1;
  int32 winner_position = 2;
  string winner_user_id = 3;
  repeated FinalScore final_scores = 4;
  google.protobuf.Timestamp completed_at = 5;
}

// Event: Player's turn to act
message PlayerTurnEvent {
  int32 position = 1;
  string action_required = 2; // "SELECT_TRUMP", "PLAY_DECISION", "EXCHANGE_CARDS", "PLAY_CARD"
  int32 timeout_seconds = 3;
}

// Event: Error occurred
message ErrorEvent {
  string error_code = 1;
  string message = 2;
  map<string, string> details = 3;
}

// Player action (client → server)
message PlayerAction {
  string game_id = 1;
  string access_token = 2;
  ActionType action_type = 3;
  
  oneof action {
    SelectTrumpAction select_trump = 10;
    PlayDecisionAction play_decision = 11;
    ExchangeCardsAction exchange_cards = 12;
    PlayCardAction play_card = 13;
  }
}

enum ActionType {
  SELECT_TRUMP = 0;
  PLAY_DECISION = 1;
  EXCHANGE_CARDS = 2;
  PLAY_CARD = 3;
}

message SelectTrumpAction {
  string trump_suit = 1;
  bool selected_before_dealing = 2;
}

message PlayDecisionAction {
  bool will_play = 1;
}

message ExchangeCardsAction {
  repeated Card cards_to_exchange = 1;
}

message PlayCardAction {
  Card card = 1;
}

// Action response
message ActionResponse {
  bool success = 1;
  string error_code = 2;
  string error_message = 3;
}

// Heartbeat
message HeartbeatRequest {
  string game_id = 1;
}

message HeartbeatResponse {
  google.protobuf.Timestamp server_time = 1;
}

// Common types
message Card {
  string rank = 1; // "Ace", "7", "King", "Queen", "Jack", "6", "5", "4", "3", "2"
  string suit = 2; // "Hearts", "Diamonds", "Clubs", "Spades"
}

message PlayerInfo {
  string user_id = 1;
  string username = 2;
  string display_name = 3;
  int32 position = 4;
  int32 current_points = 5;
}

message CardPlay {
  int32 position = 1;
  Card card = 2;
}

message RoundScore {
  int32 position = 1;
  int32 points_change = 2;
  int32 points_after = 3;
  int32 tricks_won = 4;
  bool is_penalty = 5;
  bool is_party_player = 6;
}

message FinalScore {
  int32 position = 1;
  string user_id = 2;
  int32 final_points = 3;
  double prize_won = 4;
}
```

---

## Request/Response Models

### Common Models

#### Card
```json
{
  "rank": "Ace",
  "suit": "Hearts"
}
```
**Valid Ranks:** Ace, 7, King, Queen, Jack, 6, 5, 4, 3, 2  
**Valid Suits:** Hearts, Diamonds, Clubs, Spades

#### Player
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "player123",
  "displayName": "Player One",
  "avatarUrl": null,
  "position": 0,
  "currentPoints": 15
}
```

---

## Error Response Standards

All error responses follow a consistent format:

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error message",
    "details": {
      "additionalInfo": "value"
    },
    "timestamp": "2026-02-02T13:00:00Z"
  }
}
```

### Error Codes

#### Authentication Errors (401)
- `INVALID_CREDENTIALS`: Username/password incorrect
- `TOKEN_EXPIRED`: Access token expired
- `TOKEN_INVALID`: Access token malformed or invalid
- `REFRESH_TOKEN_INVALID`: Refresh token invalid

#### Authorization Errors (403)
- `FORBIDDEN`: User lacks permission for this action
- `NOT_GAME_CREATOR`: Only game creator can perform this action
- `NOT_PARTY_PLAYER`: Only party player can select trump
- `NOT_YOUR_TURN`: Not your turn to play

#### Validation Errors (400)
- `INVALID_REQUEST`: Request body validation failed
- `INVALID_CARD`: Card format invalid
- `INVALID_TRUMP_SUIT`: Trump suit invalid
- `INVALID_MAX_PLAYERS`: maxPlayers must be 2-5
- `CARD_NOT_IN_HAND`: Played card not in hand
- `MUST_FOLLOW_SUIT`: Must follow suit when able
- `MUST_PLAY_TRUMP`: Must play higher trump (escalation rule)
- `MUST_PLAY_ACE_OF_TRUMP`: Mandatory Ace of trump play
- `CANNOT_EXCHANGE_ACE_OF_TRUMP`: Cannot exchange Ace of trump
- `TOO_MANY_CARDS_TO_EXCHANGE`: Can only exchange 0-3 cards
- `CONSECUTIVE_ROUNDS_OUT_LIMIT`: Already sat out 2 consecutive rounds
- `PARTY_PLAYER_CANNOT_OPT_OUT`: Party player must play
- `DEALER_CANNOT_OPT_OUT`: Dealer must play
- `CLUBS_TRUMP_FORCES_PLAY`: Clubs trump requires all players to play
- `GAME_FULL`: Game has reached max players
- `GAME_ALREADY_STARTED`: Game already started
- `GAME_NOT_STARTED`: Game not started yet
- `MIN_PLAYERS_REQUIRED`: Need at least 2 players to start

#### State Errors (409)
- `WRONG_GAME_PHASE`: Action not valid in current game phase
- `WRONG_ROUND_PHASE`: Action not valid in current round phase
- `ALREADY_IN_GAME`: User already in this game

#### Not Found Errors (404)
- `GAME_NOT_FOUND`: Game not found
- `USER_NOT_FOUND`: User not found
- `ROUND_NOT_FOUND`: Round not found

### Example Error Responses

**Must Follow Suit:**
```json
{
  "error": {
    "code": "MUST_FOLLOW_SUIT",
    "message": "You must follow suit when possible",
    "details": {
      "requiredSuit": "Hearts",
      "playedCard": {
        "rank": "5",
        "suit": "Clubs"
      },
      "cardsInHandOfRequiredSuit": [
        {"rank": "3", "suit": "Hearts"}
      ]
    },
    "timestamp": "2026-02-02T13:00:00Z"
  }
}
```

**Trump Escalation:**
```json
{
  "error": {
    "code": "MUST_PLAY_TRUMP",
    "message": "You must play a higher trump card",
    "details": {
      "trumpSuit": "Hearts",
      "highestTrumpPlayed": {
        "rank": "King",
        "suit": "Hearts"
      },
      "playedCard": {
        "rank": "Jack",
        "suit": "Hearts"
      },
      "higherTrumpsInHand": [
        {"rank": "Ace", "suit": "Hearts"}
      ]
    },
    "timestamp": "2026-02-02T13:00:00Z"
  }
}
```

---

## Validation Rules

### Trump Selection
- **Allowed Suits:** Hearts, Diamonds, Clubs, Spades
- **Before Dealing:** Only Hearts allowed, sets trickValue = 4
- **After Dealing:** Any suit, trickValue = 1 or 2
- **Clubs Trump:** Forces all players to play (no opt-out)
- **Only Party Player:** Can select trump

### Player Decisions
- **Opt-Out Limit:** Max 2 consecutive rounds
- **Party Player:** Cannot opt out
- **Dealer:** Cannot opt out
- **Clubs Trump:** Cannot opt out

### Card Exchange
- **Max Cards:** 0-3 cards
- **Cannot Exchange:** Ace of trump
- **Phase:** Only during CardExchange phase
- **Active Players Only:** Non-playing players cannot exchange

### Trick-Taking
1. **Follow Suit:** Must play card of lead suit if able
2. **Trump Escalation:** If trump led and have trump, must play higher trump if able
3. **Ace of Trump Mandatory:** Must play Ace of trump if:
   - Trump is led and have Ace of trump
   - Cannot follow suit and have Ace of trump
4. **Free Play:** If cannot follow suit and no Ace of trump constraint, can play any card

### Scoring
- **Tricks Won:** Reduce points by (tricksWon × trickValue)
- **Zero Tricks Penalty:**
  - 1-point tricks: +5 points
  - 2-point tricks: +10 points
  - 4-point tricks: +20 points
- **Party Player:** Penalty is doubled if party player gets zero tricks
- **Win Condition:** First to 0 or fewer points wins
- **Prize:** Winner receives €0.05 per remaining point from each player

---

## Postman Collection

A complete Postman collection is available at:
```
/docs/postman/SobeSobe_API.postman_collection.json
```

Import this collection to test all endpoints with pre-configured requests and examples.
