# SobeSobe Database Schema

Complete database schema design and domain models to support all game mechanics.

**Table of Contents**
- [Entity Relationship Diagram](#entity-relationship-diagram)
- [Entity Definitions](#entity-definitions)
- [Relationships and Foreign Keys](#relationships-and-foreign-keys)
- [Data Validation Rules](#data-validation-rules)
- [Indexes for Performance](#indexes-for-performance)
- [Data Archival Strategy](#data-archival-strategy)
- [Migration Strategy](#migration-strategy)

---

## Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         USER                                 │
├─────────────────────────────────────────────────────────────┤
│ • Id (PK, Guid)                                             │
│ • Username (unique, indexed)                                │
│ • Email (unique, indexed)                                   │
│ • PasswordHash                                              │
│ • DisplayName                                               │
│ • AvatarUrl                                                 │
│ • CreatedAt                                                 │
│ • LastLoginAt                                               │
│ • TotalGamesPlayed                                          │
│ • TotalWins                                                 │
│ • TotalPointsScored                                         │
│ • TotalPrizeWon (decimal)                                   │
└────────────┬────────────────────────────────────────────────┘
             │
             │ 1:N (Creator)
             │
             ▼
┌─────────────────────────────────────────────────────────────┐
│                         GAME                                 │
├─────────────────────────────────────────────────────────────┤
│ • Id (PK, Guid)                                             │
│ • CreatedByUserId (FK → User.Id)                            │
│ • Status (Waiting, InProgress, Completed, Abandoned)        │
│ • MaxPlayers (2-5)                                          │
│ • CurrentDealerPosition (0-4)                               │
│ • CurrentRoundNumber                                        │
│ • CreatedAt                                                 │
│ • StartedAt                                                 │
│ • CompletedAt                                               │
│ • WinnerUserId (FK → User.Id, nullable)                     │
└────────────┬────────────────────────────────────────────────┘
             │
             │ 1:N
             │
             ├──────────────────────┬─────────────────────┐
             ▼                      ▼                     ▼
┌──────────────────────┐  ┌────────────────┐  ┌─────────────────┐
│   PLAYERSESSION      │  │     ROUND      │  │  SCOREHISTORY   │
├──────────────────────┤  ├────────────────┤  ├─────────────────┤
│ • Id (PK, Guid)      │  │ • Id (PK)      │  │ • Id (PK)       │
│ • GameId (FK)        │  │ • GameId (FK)  │  │ • GameId (FK)   │
│ • UserId (FK)        │  │ • RoundNumber  │  │ • PlayerSess... │
│ • Position (0-4)     │  │ • DealerUserId │  │ • RoundId (FK)  │
│ • CurrentPoints      │  │ • PartyUserId  │  │ • PointsChange  │
│ • IsActive           │  │ • TrumpSuit    │  │ • PointsAfter   │
│ • ConsecutiveOut     │  │ • TrumpBefore  │  │ • Reason        │
│ • JoinedAt           │  │ • TrickValue   │  │ • CreatedAt     │
│ • LeftAt             │  │ • CurrentTrick │  └─────────────────┘
└──────┬───────────────┘  │ • Status       │
       │                  │ • StartedAt    │
       │ 1:N              │ • CompletedAt  │
       │                  └────────┬───────┘
       │                           │
       │                           │ 1:N
       │                           ▼
       │                  ┌─────────────────┐
       │                  │     TRICK       │
       │                  ├─────────────────┤
       │                  │ • Id (PK)       │
       │                  │ • RoundId (FK)  │
       │                  │ • TrickNumber   │
       │                  │ • LeadPlayer... │
       │                  │ • WinnerPlay... │
       │                  │ • CardsPlayed   │
       │                  │ • CompletedAt   │
       │                  └─────────────────┘
       │
       │ 1:N
       ▼
┌──────────────────────┐
│        HAND          │
├──────────────────────┤
│ • Id (PK, Guid)      │
│ • RoundId (FK)       │
│ • PlayerSessionId    │
│ • Cards (JSON)       │
│ • InitialCards (JSON)│
│ • CreatedAt          │
└──────────────────────┘
```

---

## Entity Definitions

### User

Represents a registered player in the system.

**Properties:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | Guid | PK, NOT NULL | Unique user identifier |
| `Username` | string(20) | NOT NULL, UNIQUE | Unique username (3-20 chars) |
| `Email` | string(254) | NOT NULL, UNIQUE | User email address |
| `PasswordHash` | string(256) | NOT NULL | Hashed password (BCrypt/Argon2) |
| `DisplayName` | string(50) | NOT NULL | Display name shown to other players |
| `AvatarUrl` | string(500) | NULL | URL to user avatar image |
| `CreatedAt` | DateTime | NOT NULL | Account creation timestamp |
| `LastLoginAt` | DateTime | NULL | Last successful login timestamp |
| `TotalGamesPlayed` | int | NOT NULL, DEFAULT 0 | Total games participated in |
| `TotalWins` | int | NOT NULL, DEFAULT 0 | Total games won |
| `TotalPointsScored` | int | NOT NULL, DEFAULT 0 | Cumulative points earned across all games |
| `TotalPrizeWon` | decimal(10,2) | NOT NULL, DEFAULT 0.00 | Total prize money won (in Euros) |

**Validation Rules:**
- `Username`: 3-20 characters, alphanumeric + underscore, case-insensitive unique
- `Email`: Valid email format, case-insensitive unique
- `PasswordHash`: Minimum 60 characters (BCrypt hash length)
- `DisplayName`: 1-50 characters
- `TotalGamesPlayed` ≥ `TotalWins`

---

### Game

Represents a game instance from creation to completion.

**Properties:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | Guid | PK, NOT NULL | Unique game identifier |
| `CreatedByUserId` | Guid | FK → User.Id, NOT NULL | User who created the game |
| `Status` | enum | NOT NULL | Game status: Waiting, InProgress, Completed, Abandoned |
| `MaxPlayers` | int | NOT NULL | Maximum players (2-5) |
| `CurrentDealerPosition` | int | NULL | Current dealer position (0-based, 0-4) |
| `CurrentRoundNumber` | int | NOT NULL, DEFAULT 0 | Current round number (starts at 0) |
| `CreatedAt` | DateTime | NOT NULL | Game creation timestamp |
| `StartedAt` | DateTime | NULL | Game start timestamp (when status → InProgress) |
| `CompletedAt` | DateTime | NULL | Game completion timestamp |
| `WinnerUserId` | Guid | FK → User.Id, NULL | Winning user (first to ≤0 points) |

**Status Values:**
- `Waiting` (0): Game created, waiting for players to join
- `InProgress` (1): Game is actively being played
- `Completed` (2): Game finished with a winner
- `Abandoned` (3): Game was abandoned (e.g., too many players left)

**Validation Rules:**
- `MaxPlayers`: Must be between 2 and 5
- `CurrentDealerPosition`: Must be between 0 and (number of active players - 1)
- `CurrentRoundNumber` ≥ 0
- `StartedAt` must be ≥ `CreatedAt`
- `CompletedAt` must be ≥ `StartedAt`
- `WinnerUserId` must be one of the player sessions in the game

---

### Round

Represents a single round within a game, from dealing to scoring.

**Properties:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | Guid | PK, NOT NULL | Unique round identifier |
| `GameId` | Guid | FK → Game.Id, NOT NULL | Parent game |
| `RoundNumber` | int | NOT NULL | Round number within game (1-based) |
| `DealerUserId` | Guid | FK → User.Id, NOT NULL | User who is dealer this round |
| `PartyPlayerUserId` | Guid | FK → User.Id, NOT NULL | Party player (to right of dealer) |
| `TrumpSuit` | enum | NOT NULL | Trump suit: Hearts, Diamonds, Clubs, Spades |
| `TrumpSelectedBeforeDealing` | bool | NOT NULL | If true, trick values are doubled |
| `TrickValue` | int | NOT NULL | Points per trick (1, 2, or 4) |
| `CurrentTrickNumber` | int | NOT NULL, DEFAULT 0 | Current trick being played (0-5) |
| `Status` | enum | NOT NULL | Round status |
| `StartedAt` | DateTime | NOT NULL | Round start timestamp |
| `CompletedAt` | DateTime | NULL | Round completion timestamp |

**TrumpSuit Values:**
- `Hearts` (0): ❤️
- `Diamonds` (1): ♦️
- `Clubs` (2): ♣️
- `Spades` (3): ♠️

**Status Values:**
- `Dealing` (0): Cards being dealt
- `TrumpSelection` (1): Party player selecting trump
- `PlayerDecisions` (2): Players deciding to play or sit out
- `CardExchange` (3): Players exchanging cards
- `Playing` (4): Tricks being played
- `Completed` (5): Round finished, scoring applied

**Validation Rules:**
- `RoundNumber` must match `Game.CurrentRoundNumber` + 1 when created
- `TrickValue` must be 1, 2, or 4
- `TrickValue` calculation:
  - Hearts: base 2 (4 if before dealing)
  - Diamonds/Clubs/Spades: base 1 (2 if before dealing)
- `CurrentTrickNumber`: 0-5 (0 = not started, 1-5 = active trick)
- `DealerUserId` must be an active player in the game
- `PartyPlayerUserId` must be the player to the right (counter-clockwise) of dealer

---

### PlayerSession

Represents a player's participation in a specific game.

**Properties:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | Guid | PK, NOT NULL | Unique session identifier |
| `GameId` | Guid | FK → Game.Id, NOT NULL | Parent game |
| `UserId` | Guid | FK → User.Id, NOT NULL | Player user |
| `Position` | int | NOT NULL | Player position at table (0-4, counter-clockwise) |
| `CurrentPoints` | int | NOT NULL, DEFAULT 20 | Current point total |
| `IsActive` | bool | NOT NULL, DEFAULT true | If false, player has left the game |
| `ConsecutiveRoundsOut` | int | NOT NULL, DEFAULT 0 | Consecutive rounds sat out (0-2) |
| `JoinedAt` | DateTime | NOT NULL | When player joined game |
| `LeftAt` | DateTime | NULL | When player left game (if applicable) |

**Validation Rules:**
- `Position`: Must be 0-4 and unique within the game
- `CurrentPoints`: Can be negative
- `ConsecutiveRoundsOut`: Must be 0-2 (reset to 0 when player plays a round)
- Each game must have at least 2 active player sessions
- Each game cannot have more player sessions than `Game.MaxPlayers`
- `UserId` must be unique within a game (same user cannot join twice)

---

### Hand

Represents a player's hand of cards in a specific round.

**Properties:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | Guid | PK, NOT NULL | Unique hand identifier |
| `RoundId` | Guid | FK → Round.Id, NOT NULL | Parent round |
| `PlayerSessionId` | Guid | FK → PlayerSession.Id, NOT NULL | Player who owns this hand |
| `Cards` | JSON | NOT NULL | Array of current cards in hand |
| `InitialCards` | JSON | NOT NULL | Array of initial 5 cards (for audit) |
| `CreatedAt` | DateTime | NOT NULL | When hand was dealt |

**Card Structure (JSON):**
```json
[
  { "suit": "Hearts", "rank": "Ace" },
  { "suit": "Diamonds", "rank": "7" },
  { "suit": "Clubs", "rank": "King" },
  { "suit": "Spades", "rank": "Queen" },
  { "suit": "Hearts", "rank": "Jack" }
]
```

**Rank Values:** Ace, 7, King, Queen, Jack, 6, 5, 4, 3, 2  
**Suit Values:** Hearts, Diamonds, Clubs, Spades

**Validation Rules:**
- `Cards` array length: 0-5 (cards are removed as played)
- `InitialCards` array length: exactly 5
- Each card must have valid `suit` and `rank`
- No duplicate cards within a hand
- Cards must exist in the 40-card deck (no 8s, 9s, 10s)

---

### Trick

Represents a single trick within a round.

**Properties:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | Guid | PK, NOT NULL | Unique trick identifier |
| `RoundId` | Guid | FK → Round.Id, NOT NULL | Parent round |
| `TrickNumber` | int | NOT NULL | Trick number within round (1-5) |
| `LeadPlayerSessionId` | Guid | FK → PlayerSession.Id, NOT NULL | Player who led the trick |
| `WinnerPlayerSessionId` | Guid | FK → PlayerSession.Id, NULL | Player who won (NULL if incomplete) |
| `CardsPlayed` | JSON | NOT NULL | Array of cards played in order |
| `CompletedAt` | DateTime | NULL | When trick was completed |

**CardsPlayed Structure (JSON):**
```json
[
  { "playerSessionId": "guid1", "card": { "suit": "Hearts", "rank": "Ace" } },
  { "playerSessionId": "guid2", "card": { "suit": "Hearts", "rank": "7" } },
  { "playerSessionId": "guid3", "card": { "suit": "Hearts", "rank": "King" } }
]
```

**Validation Rules:**
- `TrickNumber`: 1-5 within a round
- `CardsPlayed` array length: Must equal number of players in round
- First card in `CardsPlayed` must belong to `LeadPlayerSessionId`
- `WinnerPlayerSessionId` must be one of the players who played a card
- Cards played must follow game rules (follow suit, trump escalation, etc.)

---

### ScoreHistory

Represents a point change for a player, creating an audit trail.

**Properties:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | Guid | PK, NOT NULL | Unique score entry identifier |
| `GameId` | Guid | FK → Game.Id, NOT NULL | Parent game |
| `PlayerSessionId` | Guid | FK → PlayerSession.Id, NOT NULL | Player whose score changed |
| `RoundId` | Guid | FK → Round.Id, NULL | Round that caused the change (NULL for initial) |
| `PointsChange` | int | NOT NULL | Points added/subtracted (can be negative) |
| `PointsAfter` | int | NOT NULL | Player's point total after this change |
| `Reason` | enum | NOT NULL | Reason for score change |
| `CreatedAt` | DateTime | NOT NULL | When score changed |

**Reason Values:**
- `GameStart` (0): Initial 20 points
- `TricksWon` (1): Reduced points from winning tricks
- `NoTricksNormalPenalty` (2): +5/+10/+20 penalty
- `NoTricksPartyPenalty` (3): Double penalty for party player
- `GameWon` (4): Player reached ≤0 points
- `ManualAdjustment` (5): Admin/debug adjustment

**Validation Rules:**
- `PointsAfter` = Previous `PointsAfter` + `PointsChange`
- For `GameStart` reason: `PointsChange` = 20, `PointsAfter` = 20
- First entry per player must have `Reason` = GameStart
- Entries must be chronologically ordered per player

---

## Relationships and Foreign Keys

### User → Game (One-to-Many)
- **Relationship:** A user can create multiple games
- **Foreign Key:** `Game.CreatedByUserId` → `User.Id`
- **On Delete:** RESTRICT (cannot delete user with games)

### User → PlayerSession (One-to-Many)
- **Relationship:** A user can have multiple player sessions across different games
- **Foreign Key:** `PlayerSession.UserId` → `User.Id`
- **On Delete:** RESTRICT (cannot delete user with active sessions)

### Game → PlayerSession (One-to-Many)
- **Relationship:** A game has multiple player sessions
- **Foreign Key:** `PlayerSession.GameId` → `Game.Id`
- **On Delete:** CASCADE (delete sessions when game is deleted)

### Game → Round (One-to-Many)
- **Relationship:** A game has multiple rounds
- **Foreign Key:** `Round.GameId` → `Game.Id`
- **On Delete:** CASCADE (delete rounds when game is deleted)

### Game → ScoreHistory (One-to-Many)
- **Relationship:** A game has multiple score history entries
- **Foreign Key:** `ScoreHistory.GameId` → `Game.Id`
- **On Delete:** CASCADE (delete history when game is deleted)

### Round → Trick (One-to-Many)
- **Relationship:** A round has exactly 5 tricks
- **Foreign Key:** `Trick.RoundId` → `Round.Id`
- **On Delete:** CASCADE (delete tricks when round is deleted)

### Round → Hand (One-to-Many)
- **Relationship:** A round has one hand per active player
- **Foreign Key:** `Hand.RoundId` → `Round.Id`
- **On Delete:** CASCADE (delete hands when round is deleted)

### PlayerSession → Hand (One-to-Many)
- **Relationship:** A player session has one hand per round they play
- **Foreign Key:** `Hand.PlayerSessionId` → `PlayerSession.Id`
- **On Delete:** CASCADE (delete hands when session is deleted)

### PlayerSession → ScoreHistory (One-to-Many)
- **Relationship:** A player session has multiple score history entries
- **Foreign Key:** `ScoreHistory.PlayerSessionId` → `PlayerSession.Id`
- **On Delete:** CASCADE (delete history when session is deleted)

---

## Data Validation Rules

### User Validation
```csharp
- Username: Regex("^[a-zA-Z0-9_]{3,20}$"), unique (case-insensitive)
- Email: EmailAddress attribute, unique (case-insensitive)
- PasswordHash: MinLength(60)
- DisplayName: Required, MaxLength(50)
- TotalGamesPlayed >= TotalWins
```

### Game Validation
```csharp
- MaxPlayers: Range(2, 5)
- CurrentRoundNumber >= 0
- Status must follow transition rules:
  Waiting → InProgress → (Completed | Abandoned)
- StartedAt >= CreatedAt
- CompletedAt >= StartedAt
- Must have 2-5 active PlayerSessions
```

### Round Validation
```csharp
- RoundNumber > 0
- TrickValue ∈ {1, 2, 4}
- CurrentTrickNumber: Range(0, 5)
- TrumpSuit ∈ {Hearts, Diamonds, Clubs, Spades}
- Status must follow transition:
  Dealing → TrumpSelection → PlayerDecisions → CardExchange → Playing → Completed
```

### PlayerSession Validation
```csharp
- Position: Range(0, 4), unique within game
- ConsecutiveRoundsOut: Range(0, 2)
- UserId unique within game
- CurrentPoints can be negative (no lower bound)
- Position must be < Game.MaxPlayers
```

### Hand Validation
```csharp
- Cards.Length: Range(0, 5)
- InitialCards.Length == 5
- All cards must be valid (suit + rank exist in game rules)
- No duplicate cards in same hand
- Cards must be from 40-card deck (no 8, 9, 10)
```

### Trick Validation
```csharp
- TrickNumber: Range(1, 5)
- CardsPlayed.Length == number of active players in round
- First card must be from LeadPlayerSessionId
- WinnerPlayerSessionId must be in CardsPlayed
- Cards must follow game rules (validated separately)
```

### ScoreHistory Validation
```csharp
- PointsAfter == Previous.PointsAfter + PointsChange
- First entry per player: Reason == GameStart, PointsChange == 20
- Chronological order per player (CreatedAt ascending)
```

---

## Indexes for Performance

### User Table
```sql
CREATE UNIQUE INDEX IX_User_Username ON User(Username);
CREATE UNIQUE INDEX IX_User_Email ON User(Email);
CREATE INDEX IX_User_LastLoginAt ON User(LastLoginAt DESC);
```

### Game Table
```sql
CREATE INDEX IX_Game_CreatedByUserId ON Game(CreatedByUserId);
CREATE INDEX IX_Game_Status ON Game(Status);
CREATE INDEX IX_Game_CreatedAt ON Game(CreatedAt DESC);
CREATE INDEX IX_Game_Status_CreatedAt ON Game(Status, CreatedAt DESC);
```

### PlayerSession Table
```sql
CREATE INDEX IX_PlayerSession_GameId ON PlayerSession(GameId);
CREATE INDEX IX_PlayerSession_UserId ON PlayerSession(UserId);
CREATE INDEX IX_PlayerSession_GameId_Position ON PlayerSession(GameId, Position);
CREATE UNIQUE INDEX IX_PlayerSession_GameId_UserId ON PlayerSession(GameId, UserId);
```

### Round Table
```sql
CREATE INDEX IX_Round_GameId ON Round(GameId);
CREATE INDEX IX_Round_GameId_RoundNumber ON Round(GameId, RoundNumber);
CREATE INDEX IX_Round_Status ON Round(Status);
```

### Hand Table
```sql
CREATE INDEX IX_Hand_RoundId ON Hand(RoundId);
CREATE INDEX IX_Hand_PlayerSessionId ON Hand(PlayerSessionId);
CREATE UNIQUE INDEX IX_Hand_RoundId_PlayerSessionId ON Hand(RoundId, PlayerSessionId);
```

### Trick Table
```sql
CREATE INDEX IX_Trick_RoundId ON Trick(RoundId);
CREATE INDEX IX_Trick_RoundId_TrickNumber ON Trick(RoundId, TrickNumber);
```

### ScoreHistory Table
```sql
CREATE INDEX IX_ScoreHistory_GameId ON ScoreHistory(GameId);
CREATE INDEX IX_ScoreHistory_PlayerSessionId ON ScoreHistory(PlayerSessionId);
CREATE INDEX IX_ScoreHistory_RoundId ON ScoreHistory(RoundId);
CREATE INDEX IX_ScoreHistory_CreatedAt ON ScoreHistory(CreatedAt);
```

**Index Strategy:**
- Unique indexes enforce business rules (username, email, game+user uniqueness)
- Foreign key columns are indexed for join performance
- Composite indexes support common query patterns (game+round, game+position)
- Timestamp indexes support pagination and recent game queries
- Status indexes support filtering active/completed games

---

## Data Archival Strategy

### Archival Goals
- Keep database size manageable
- Preserve historical data for statistics
- Maintain performance for active games

### Archival Rules

**Archive Completed Games After:**
- 90 days for games with status `Completed` or `Abandoned`
- Never archive games with status `Waiting` or `InProgress`

**Archival Process:**
1. Move old games to `ArchivedGames` table (same schema)
2. Move related entities to archive tables:
   - `ArchivedPlayerSessions`
   - `ArchivedRounds`
   - `ArchivedHands`
   - `ArchivedTricks`
   - `ArchivedScoreHistory`
3. Retain user aggregate statistics (TotalWins, TotalPointsScored, etc.)
4. Create compressed JSON snapshot of full game state for long-term storage

**Retention Policy:**
- Active games: No archival
- Completed games < 90 days: Live database
- Completed games 90-730 days (2 years): Archive tables
- Completed games > 730 days: Compressed JSON in blob storage
- User statistics: Never deleted

**Archive Tables:**
```sql
-- Same schema as live tables, plus:
CREATE TABLE ArchivedGames (
  ...all Game columns...,
  ArchivedAt DATETIME NOT NULL
);

-- Similar for other entities
```

**Blob Storage Format:**
```json
{
  "gameId": "guid",
  "archivedAt": "timestamp",
  "game": { /* full game object */ },
  "players": [ /* all player sessions */ ],
  "rounds": [ /* all rounds with tricks */ ],
  "scoreHistory": [ /* complete history */ ]
}
```

---

## Migration Strategy

### Phase 1: Development (Current)
- **Database:** SQLite
- **Location:** Local file `sobesobe.db`
- **EF Core Migrations:** Code-first migrations
- **Advantages:** Simple setup, no server required
- **Limitations:** Single connection, limited concurrent users

### Phase 2: Staging/Testing
- **Database:** Azure SQL Database (Basic tier or SQL Server in Docker)
- **Migration Path:**
  1. Generate EF Core migration scripts
  2. Test migrations against SQL Server
  3. Validate data types and constraints
  4. Performance test with realistic data volumes
- **Connection String:** Stored in Azure Key Vault

### Phase 3: Production
- **Database Options:**
  
  **Option A: Azure SQL Database**
  - Recommended tier: S0 (10 DTUs) for start
  - Automatic backups, point-in-time restore
  - Scale up as user base grows
  - Estimated cost: ~$15/month
  
  **Option B: Azure Cosmos DB (SQL API)**
  - Better for global distribution
  - Auto-scaling for variable loads
  - Higher cost but better performance at scale
  - Estimated cost: ~$25/month baseline

  **Recommendation:** Start with Azure SQL Database S0, migrate to higher tier or Cosmos DB as needed

### Migration Checklist
- [ ] Update connection strings in `appsettings.Production.json`
- [ ] Update EF Core provider from `Microsoft.EntityFrameworkCore.Sqlite` to `Microsoft.EntityFrameworkCore.SqlServer`
- [ ] Test all queries for SQL Server compatibility (some SQLite functions differ)
- [ ] Set up database backups (automated in Azure)
- [ ] Configure connection pooling and retry policies
- [ ] Update Aspire AppHost to use SQL Server container for local development
- [ ] Performance test under load (use Application Insights)

### Data Type Mapping (SQLite → SQL Server)
```
SQLite TEXT → SQL Server NVARCHAR(MAX) or VARCHAR(MAX)
SQLite INTEGER → SQL Server INT or BIGINT
SQLite REAL → SQL Server FLOAT or DECIMAL
SQLite BLOB → SQL Server VARBINARY(MAX)
```

**Potential Issues:**
- SQLite stores DateTimes as strings; SQL Server has native DATETIME2
- SQLite is case-insensitive by default; SQL Server depends on collation
- JSON support: SQLite uses JSON1 extension; SQL Server has native JSON functions
- Transactions: SQL Server requires explicit transaction management

---

## Summary

**Schema Highlights:**
- **7 Core Entities:** User, Game, PlayerSession, Round, Hand, Trick, ScoreHistory
- **Normalized Design:** 3NF compliance, minimal redundancy
- **Audit Trail:** ScoreHistory tracks all point changes
- **JSON Storage:** Cards stored as JSON for flexibility
- **Comprehensive Indexes:** Optimized for common query patterns
- **Archival Strategy:** Keeps database performant with 90-day archival
- **Migration Ready:** Clear path from SQLite → Azure SQL Database

**Next Steps:**
1. Implement domain models in `SobeSobe.Core` with EF Core annotations
2. Create `ApplicationDbContext` with entity configurations
3. Generate initial EF Core migration
4. Implement repository interfaces and patterns
5. Add validation attributes and business rule checks
