using Microsoft.EntityFrameworkCore;
using SobeSobe.Api.DTOs;
using SobeSobe.Api.Services;
using SobeSobe.Api.Services.Realtime;
using SobeSobe.Core.Entities;
using SobeSobe.Core.Enums;
using SobeSobe.Core.ValueObjects;
using SobeSobe.Infrastructure.Data;
using System.Text.Json;

namespace SobeSobe.Api.Endpoints;

public static class RoundEndpoints
{
    public static IEndpointRouteBuilder MapRoundEndpoints(this IEndpointRouteBuilder app)
    {
        // Select Trump endpoint (requires authentication, party player only)
        app.MapPost("/api/games/{id:guid}/rounds/current/trump", async (Guid id, SelectTrumpRequest request,
            HttpContext httpContext, ApplicationDbContext db, IGameEventBroadcaster gameEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Find game
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            if (game.Status != GameStatus.InProgress)
            {
                return Results.BadRequest(new { error = "Game is not in progress" });
            }

            // Get current round
            var currentRound = await db.Rounds
                .Where(r => r.GameId == id)
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefaultAsync();
            if (currentRound == null)
            {
                return Results.BadRequest(new { error = "No active round found" });
            }

            // Check if round is in TrumpSelection phase
            if (currentRound.Status != RoundStatus.TrumpSelection)
            {
                return Results.StatusCode(409); // Conflict - wrong phase
            }

            // Check if user is the party player
            if (currentRound.PartyPlayerUserId != userId)
            {
                return Results.StatusCode(403); // Forbidden - only party player can select trump
            }

            // Load hands to determine if initial cards were dealt
            var hands = await db.Hands
                .Where(h => h.RoundId == currentRound.Id)
                .ToListAsync();

            var hasInitialCards = hands.Any(h => h.Cards.Count > 0);

            if (request.SelectedBeforeDealing && hasInitialCards)
            {
                return Results.BadRequest(new { error = "Initial cards were already dealt" });
            }

            if (!request.SelectedBeforeDealing && !hasInitialCards)
            {
                return Results.BadRequest(new { error = "Initial cards must be dealt before selecting trump" });
            }

            if (request.SelectedBeforeDealing)
            {
                var activePlayers = game.PlayerSessions
                    .Where(ps => ps.IsActive)
                    .OrderBy(ps => ps.Position)
                    .ToList();

                if (activePlayers.Count == 0)
                {
                    return Results.BadRequest(new { error = "No active players in game" });
                }

                var knownCards = hands.SelectMany(h => h.Cards).ToList();
                var deck = EnsureRoundDeck(currentRound, knownCards);

                var partyPlayerPosition = activePlayers.First(ps => ps.UserId == currentRound.PartyPlayerUserId).Position;
                var playerPositions = activePlayers.Select(p => p.Position).ToList();
                var dealtCards = CardDealingService.DealCards(deck, playerPositions, partyPlayerPosition, 2);
                currentRound.Deck = deck;

                foreach (var player in activePlayers)
                {
                    var existingHand = hands.FirstOrDefault(h => h.PlayerSessionId == player.Id);
                    var cards = dealtCards[player.Position];
                    var cardsJson = JsonSerializer.Serialize(cards);

                    if (existingHand == null)
                    {
                        var hand = new Hand
                        {
                            RoundId = currentRound.Id,
                            PlayerSessionId = player.Id,
                            CardsJson = cardsJson,
                            InitialCardsJson = cardsJson
                        };
                        db.Hands.Add(hand);
                    }
                    else
                    {
                        existingHand.CardsJson = cardsJson;
                        existingHand.InitialCardsJson = cardsJson;
                    }
                }
            }

            // Calculate trick value based on trump suit and timing
            int trickValue;
            if (request.SelectedBeforeDealing)
            {
                // Selected before dealing (blind trump) - all values doubled
                trickValue = request.TrumpSuit switch
                {
                    TrumpSuit.Hearts => 4,
                    TrumpSuit.Diamonds => 2,
                    TrumpSuit.Clubs => 2,
                    TrumpSuit.Spades => 2,
                    _ => 1
                };
            }
            else
            {
                // Selected after receiving 2 cards - normal values
                trickValue = request.TrumpSuit switch
                {
                    TrumpSuit.Hearts => 2,
                    TrumpSuit.Diamonds => 1,
                    TrumpSuit.Clubs => 1,
                    TrumpSuit.Spades => 1,
                    _ => 1
                };
            }

            // Update round with trump selection
            currentRound.TrumpSuit = request.TrumpSuit;
            currentRound.TrumpSelectedBeforeDealing = request.SelectedBeforeDealing;
            currentRound.TrickValue = trickValue;

            // Move to next phase after trump selection
            currentRound.Status = RoundStatus.PlayerDecisions;

            await db.SaveChangesAsync();

            // Broadcast trump selected event
            await gameEvents.BroadcastTrumpSelectedAsync(
                game.Id.ToString(),
                request.TrumpSuit.ToString(),
                request.SelectedBeforeDealing,
                currentRound.TrickValue);

            // Return response
            var response = new SelectTrumpResponse
            {
                RoundId = currentRound.Id,
                TrumpSuit = currentRound.TrumpSuit,
                TrumpSelectedBeforeDealing = currentRound.TrumpSelectedBeforeDealing,
                TrickValue = currentRound.TrickValue
            };

            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("SelectTrump");

        // Player Decision endpoint (requires authentication, PlayerDecisions phase)
        app.MapPost("/api/games/{id:guid}/rounds/current/play-decision", async (Guid id, PlayDecisionRequest request, HttpContext httpContext, ApplicationDbContext db, IGameEventBroadcaster gameEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Find game with player sessions
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            if (game.Status != GameStatus.InProgress)
            {
                return Results.BadRequest(new { error = "Game is not in progress" });
            }

            // Get current round
            var currentRound = await db.Rounds
                .Where(r => r.GameId == id)
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefaultAsync();
            if (currentRound == null)
            {
                return Results.BadRequest(new { error = "No active round found" });
            }

            // Check if round is in PlayerDecisions phase
            if (currentRound.Status != RoundStatus.PlayerDecisions)
            {
                return Results.StatusCode(409); // Conflict - wrong phase
            }

            // Find player session for current user
            var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
            if (playerSession == null)
            {
                return Results.NotFound(new { error = "Player not in this game" });
            }

            // Validate decision rules
            // Rule 1: Party player must always play
            if (currentRound.PartyPlayerUserId == userId && !request.WillPlay)
            {
                return Results.BadRequest(new { error = "Party player cannot opt out" });
            }

            // Rule 2: If trump is Clubs, all players must play
            if (currentRound.TrumpSuit == TrumpSuit.Clubs && !request.WillPlay)
            {
                return Results.BadRequest(new { error = "Clubs trump forces all players to play" });
            }

            // Rule 3: Cannot sit out more than 2 consecutive rounds
            if (!request.WillPlay && playerSession.ConsecutiveRoundsOut >= 2)
            {
                return Results.BadRequest(new { error = "Cannot sit out more than 2 consecutive rounds" });
            }

            // Rule 4: Players with 5 points or less must play (from game rules)
            if (!request.WillPlay && playerSession.CurrentPoints <= 5)
            {
                return Results.BadRequest(new { error = "Players with 5 points or less must play" });
            }

            // Update consecutive rounds out counter
            if (!request.WillPlay)
            {
                playerSession.ConsecutiveRoundsOut++;
            }
            else
            {
                // Reset counter if player decides to play
                playerSession.ConsecutiveRoundsOut = 0;
            }

            playerSession.LastDecisionRoundNumber = currentRound.RoundNumber;

            // Create or update Hand to track who's playing
            var existingHand = await db.Hands
                .FirstOrDefaultAsync(h => h.RoundId == currentRound.Id && h.PlayerSessionId == playerSession.Id);

            if (request.WillPlay)
            {
                if (existingHand == null)
                {
                    // Create hand for player (cards will be dealt later)
                    var hand = new Hand
                    {
                        RoundId = currentRound.Id,
                        PlayerSessionId = playerSession.Id,
                        CardsJson = "[]", // Empty for now, will be populated during dealing
                        InitialCardsJson = "[]"
                    };
                    db.Hands.Add(hand);
                }
            }
            else
            {
                // Player opts out - remove hand if it exists
                if (existingHand != null)
                {
                    db.Hands.Remove(existingHand);
                }
            }

            await db.SaveChangesAsync();

            await gameEvents.BroadcastPlayerDecisionAsync(game.Id.ToString(), playerSession.Position, request.WillPlay);

            // Return response
            var response = new PlayDecisionResponse
            {
                RoundId = currentRound.Id,
                PlayerSessionId = playerSession.Id,
                WillPlay = request.WillPlay,
                ConsecutiveRoundsOut = playerSession.ConsecutiveRoundsOut
            };

            var allDecisionsMade = game.PlayerSessions
                .Where(ps => ps.IsActive)
                .All(ps => ps.LastDecisionRoundNumber == currentRound.RoundNumber);

            if (!allDecisionsMade)
            {
                return Results.Ok(response);
            }

            await db.Entry(currentRound).ReloadAsync();
            if (currentRound.Status != RoundStatus.PlayerDecisions)
            {
                return Results.Ok(response);
            }

            var hands = await db.Hands
                .Where(h => h.RoundId == currentRound.Id)
                .Include(h => h.PlayerSession)
                .ToListAsync();

            if (hands.Count == 0)
            {
                return Results.Ok(response);
            }

            var needsAdditionalCards = hands.Any(h => h.Cards.Count < 5);
            if (needsAdditionalCards)
            {
                var knownCards = hands.SelectMany(h => h.Cards).ToList();
                var deck = EnsureRoundDeck(currentRound, knownCards);

                var handsNeedingCards = hands
                    .Where(h => h.PlayerSession != null && h.Cards.Count < 5)
                    .ToList();
                var playingPlayerPositions = handsNeedingCards
                    .Select(h => h.PlayerSession!.Position)
                    .ToList();
                var partyPlayerPosition = game.PlayerSessions.First(ps => ps.UserId == currentRound.PartyPlayerUserId).Position;
                var dealtCards = CardDealingService.DealCards(deck, playingPlayerPositions, partyPlayerPosition, 3);
                currentRound.Deck = deck;

                foreach (var hand in handsNeedingCards)
                {
                    if (hand.PlayerSession == null)
                    {
                        continue;
                    }

                    var currentCards = hand.Cards;
                    var newCards = dealtCards[hand.PlayerSession.Position];
                    currentCards.AddRange(newCards);
                    hand.CardsJson = JsonSerializer.Serialize(currentCards);

                    if (string.IsNullOrEmpty(hand.InitialCardsJson) || hand.InitialCardsJson == "[]")
                    {
                        hand.InitialCardsJson = hand.CardsJson;
                    }
                }
            }

            currentRound.Status = RoundStatus.CardExchange;
            await db.SaveChangesAsync();

            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("PlayDecision");

        // Deal Cards endpoint (requires authentication, handles automatic dealing based on phase)
        app.MapPost("/api/games/{id:guid}/rounds/current/deal-cards", async (Guid id, HttpContext httpContext, ApplicationDbContext db, IGameEventBroadcaster gameEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Find game with player sessions
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            if (game.Status != GameStatus.InProgress)
            {
                return Results.BadRequest(new { error = "Game is not in progress" });
            }

            // Get current round
            var currentRound = await db.Rounds
                .Where(r => r.GameId == id)
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefaultAsync();
            if (currentRound == null)
            {
                return Results.BadRequest(new { error = "No active round found" });
            }

            // Find player's session
            var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
            if (playerSession == null)
            {
                return Results.NotFound(new { error = "Player not in this game" });
            }

            // Load hands for current round
            var hands = await db.Hands
                .Where(h => h.RoundId == currentRound.Id)
                .Include(h => h.PlayerSession)
                .ToListAsync();

            // Determine dealing logic based on round status
            if (currentRound.Status == RoundStatus.TrumpSelection)
            {
                if (playerSession.UserId != currentRound.PartyPlayerUserId)
                {
                    return Results.BadRequest(new { error = "Only the party player can deal initial cards" });
                }

                var initialCardsDealt = hands.Any(h => h.Cards.Count > 0);
                if (initialCardsDealt)
                {
                    return Results.BadRequest(new { error = "Initial cards were already dealt" });
                }

                var activePlayers = game.PlayerSessions.Where(ps => ps.IsActive).OrderBy(ps => ps.Position).ToList();
                if (activePlayers.Count == 0)
                {
                    return Results.BadRequest(new { error = "No active players in game" });
                }

                var knownCards = hands.SelectMany(h => h.Cards).ToList();
                var deck = EnsureRoundDeck(currentRound, knownCards);

                var partyPlayerPosition = activePlayers.First(ps => ps.UserId == currentRound.PartyPlayerUserId).Position;
                var playerPositions = activePlayers.Select(p => p.Position).ToList();
                var dealtCards = CardDealingService.DealCards(deck, playerPositions, partyPlayerPosition, 2);
                currentRound.Deck = deck;

                foreach (var player in activePlayers)
                {
                    var existingHand = hands.FirstOrDefault(h => h.PlayerSessionId == player.Id);
                    var cards = dealtCards[player.Position];
                    var cardsJson = JsonSerializer.Serialize(cards);

                    if (existingHand == null)
                    {
                        var hand = new Hand
                        {
                            RoundId = currentRound.Id,
                            PlayerSessionId = player.Id,
                            CardsJson = cardsJson,
                            InitialCardsJson = cardsJson
                        };
                        db.Hands.Add(hand);
                    }
                    else
                    {
                        existingHand.CardsJson = cardsJson;
                        existingHand.InitialCardsJson = cardsJson;
                    }
                }

                await db.SaveChangesAsync();

                await gameEvents.BroadcastInitialCardsDealtAsync(game.Id.ToString(), 2, activePlayers.Count);

                return Results.Ok(new
                {
                    message = "Dealt initial cards to all players",
                    roundId = currentRound.Id,
                    status = currentRound.Status.ToString(),
                    playersDealt = activePlayers.Count
                });
            }
            else if (currentRound.Status == RoundStatus.Dealing)
            {
                // Trump selected before dealing - deal 5 cards to all active players
                var activePlayers = game.PlayerSessions.Where(ps => ps.IsActive).OrderBy(ps => ps.Position).ToList();

                if (activePlayers.Count == 0)
                {
                    return Results.BadRequest(new { error = "No active players in game" });
                }

                var knownCards = hands.SelectMany(h => h.Cards).ToList();
                var deck = EnsureRoundDeck(currentRound, knownCards);

                // Deal 5 cards to each active player
                var partyPlayerPosition = activePlayers.First(ps => ps.UserId == currentRound.PartyPlayerUserId).Position;
                var playerPositions = activePlayers.Select(p => p.Position).ToList();
                var dealtCards = CardDealingService.DealCards(deck, playerPositions, partyPlayerPosition, 5);
                currentRound.Deck = deck;

                // Create hands for all active players
                foreach (var player in activePlayers)
                {
                    var existingHand = hands.FirstOrDefault(h => h.PlayerSessionId == player.Id);
                    var cards = dealtCards[player.Position];
                    var cardsJson = JsonSerializer.Serialize(cards);

                    if (existingHand == null)
                    {
                        var hand = new Hand
                        {
                            RoundId = currentRound.Id,
                            PlayerSessionId = player.Id,
                            CardsJson = cardsJson,
                            InitialCardsJson = cardsJson
                        };
                        db.Hands.Add(hand);
                    }
                    else
                    {
                        existingHand.CardsJson = cardsJson;
                        existingHand.InitialCardsJson = cardsJson;
                    }
                }

                // Move to CardExchange phase
                currentRound.Status = RoundStatus.CardExchange;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "Dealt 5 cards to all players",
                    roundId = currentRound.Id,
                    status = currentRound.Status.ToString(),
                    playersDealt = activePlayers.Count
                });
            }
            else if (currentRound.Status == RoundStatus.PlayerDecisions)
            {
                // Check if all active players have made their decisions
                var activePlayers = game.PlayerSessions.Where(ps => ps.IsActive).ToList();

                var allDecisionsMade = activePlayers.All(ps => ps.LastDecisionRoundNumber == currentRound.RoundNumber);
                if (!allDecisionsMade)
                {
                    return Results.BadRequest(new { error = "Not all players have made their decisions yet" });
                }

                // Trump selected after 2 cards - deal remaining 3 cards to players with hands
                // Note: In real implementation, 2 cards should have been dealt already
                // For now, we'll deal 3 more cards to complete the 5-card hands

                var knownCards = hands.SelectMany(h => h.Cards).ToList();
                var deck = EnsureRoundDeck(currentRound, knownCards);

                var handsNeedingCards = hands.Where(h => h.PlayerSession != null && h.Cards.Count < 5).ToList();
                if (handsNeedingCards.Count == 0)
                {
                    return Results.Ok(new
                    {
                        message = "Players already have full hands",
                        roundId = currentRound.Id,
                        status = currentRound.Status.ToString(),
                        playersDealt = 0
                    });
                }

                var playingPlayerPositions = handsNeedingCards.Select(h => h.PlayerSession!.Position).ToList();
                var partyPlayerPosition = game.PlayerSessions.First(ps => ps.UserId == currentRound.PartyPlayerUserId).Position;
                var dealtCards = CardDealingService.DealCards(deck, playingPlayerPositions, partyPlayerPosition, 3);
                currentRound.Deck = deck;

                // Add 3 cards to each existing hand
                foreach (var hand in handsNeedingCards)
                {
                    if (hand.PlayerSession == null) continue;

                    var currentCards = JsonSerializer.Deserialize<List<Card>>(hand.CardsJson) ?? new List<Card>();
                    var newCards = dealtCards[hand.PlayerSession.Position];
                    currentCards.AddRange(newCards);
                    hand.CardsJson = JsonSerializer.Serialize(currentCards);

                    // Update initial cards if not set
                    if (string.IsNullOrEmpty(hand.InitialCardsJson) || hand.InitialCardsJson == "[]")
                    {
                        hand.InitialCardsJson = hand.CardsJson;
                    }
                }

                // Move to CardExchange phase
                currentRound.Status = RoundStatus.CardExchange;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "Dealt 3 additional cards to players",
                    roundId = currentRound.Id,
                    status = currentRound.Status.ToString(),
                    playersDealt = handsNeedingCards.Count
                });
            }
            else
            {
                return Results.StatusCode(409); // Conflict - wrong phase for dealing
            }
        })
        .RequireAuthorization()
        .WithName("DealCards");

        // Exchange Cards endpoint (requires authentication)
        app.MapPost("/api/games/{id:guid}/rounds/current/exchange-cards", async (Guid id, ExchangeCardsRequest request, HttpContext httpContext, ApplicationDbContext db) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Validate request
            if (request.CardsToExchange.Count > 3)
            {
                return Results.BadRequest(new { error = "Cannot exchange more than 3 cards" });
            }

            // Validate all cards are valid
            foreach (var card in request.CardsToExchange)
            {
                if (!card.IsValid())
                {
                    return Results.BadRequest(new { error = $"Invalid card: {card}" });
                }
            }

            // Find game with player sessions
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            if (game.Status != GameStatus.InProgress)
            {
                return Results.BadRequest(new { error = "Game is not in progress" });
            }

            // Get current round with hands
            var currentRound = await db.Rounds
                .Include(r => r.Hands)
                .ThenInclude(h => h.PlayerSession)
                .Where(r => r.GameId == id)
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefaultAsync();
            if (currentRound == null)
            {
                return Results.NotFound(new { error = "No active round found" });
            }

            // Verify round is in CardExchange phase
            if (currentRound.Status != RoundStatus.CardExchange)
            {
                return Results.StatusCode(409); // Conflict - wrong phase
            }

            // Find player's session
            var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId);
            if (playerSession == null)
            {
                return Results.NotFound(new { error = "Player not in this game" });
            }

            // Find player's hand
            var hand = currentRound.Hands.FirstOrDefault(h => h.PlayerSessionId == playerSession.Id);
            if (hand == null)
            {
                return Results.BadRequest(new { error = "Player is not playing this round" });
            }

            // Get current cards
            var currentCards = hand.Cards;

            // Validate player has all cards they want to exchange
            foreach (var cardToExchange in request.CardsToExchange)
            {
                var hasCard = currentCards.Any(c => c.Suit == cardToExchange.Suit && c.Rank == cardToExchange.Rank);
                if (!hasCard)
                {
                    return Results.BadRequest(new { error = $"You don't have the card: {cardToExchange}" });
                }
            }

            // If no cards to exchange, return current hand
            if (request.CardsToExchange.Count == 0)
            {
                return Results.Ok(new ExchangeCardsResponse
                {
                    RoundId = currentRound.Id,
                    PlayerSessionId = playerSession.Id,
                    CardsExchanged = 0,
                    NewHand = currentCards
                });
            }

            // Remove cards from hand
            foreach (var cardToExchange in request.CardsToExchange)
            {
                var cardToRemove = currentCards.First(c => c.Suit == cardToExchange.Suit && c.Rank == cardToExchange.Rank);
                currentCards.Remove(cardToRemove);
            }

            var knownCards = currentRound.Hands.SelectMany(h => h.Cards).ToList();
            var deck = EnsureRoundDeck(currentRound, knownCards);

            if (deck.Count < request.CardsToExchange.Count)
            {
                return Results.BadRequest(new { error = "Not enough cards remaining in deck" });
            }

            // Draw replacement cards
            var newCards = deck.Take(request.CardsToExchange.Count).ToList();
            deck.RemoveRange(0, request.CardsToExchange.Count);
            currentCards.AddRange(newCards);
            currentRound.Deck = deck;

            // Update hand
            hand.CardsJson = JsonSerializer.Serialize(currentCards);
            await db.SaveChangesAsync();

            return Results.Ok(new ExchangeCardsResponse
            {
                RoundId = currentRound.Id,
                PlayerSessionId = playerSession.Id,
                CardsExchanged = request.CardsToExchange.Count,
                NewHand = currentCards
            });
        })
        .RequireAuthorization()
        .WithName("ExchangeCards");

        // Play Card endpoint (requires authentication)
        app.MapPost("/api/games/{id:guid}/rounds/current/play-card", async (Guid id, PlayCardRequest request,
            HttpContext httpContext, ApplicationDbContext db, TrickTakingService trickService,
            IGameEventBroadcaster gameEvents) =>
        {
            // Get user ID from JWT claims
            var userIdClaim = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Validate card
            if (!request.Card.IsValid())
            {
                return Results.BadRequest(new { error = "Invalid card" });
            }

            // Find game with current round, player sessions, hands, and tricks
            var game = await db.Games
                .Include(g => g.PlayerSessions)
                    .ThenInclude(ps => ps.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return Results.NotFound(new { error = "Game not found" });
            }

            if (game.Status != GameStatus.InProgress)
            {
                return Results.BadRequest(new { error = "Game is not in progress" });
            }

            // Get current round
            var currentRound = await db.Rounds
                .Include(r => r.Hands)
                .Include(r => r.Tricks)
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefaultAsync(r => r.GameId == id);

            if (currentRound == null)
            {
                return Results.NotFound(new { error = "No active round found" });
            }

            // Validate round is in Playing or CardExchange phase
            if (currentRound.Status != RoundStatus.Playing && currentRound.Status != RoundStatus.CardExchange)
            {
                return Results.Conflict(new { error = $"Round is not in playing phase (current status: {currentRound.Status})" });
            }

            // Auto-transition from CardExchange to Playing if needed
            if (currentRound.Status == RoundStatus.CardExchange)
            {
                currentRound.Status = RoundStatus.Playing;
            }

            // Find player session
            var playerSession = game.PlayerSessions.FirstOrDefault(ps => ps.UserId == userId && ps.IsActive);
            if (playerSession == null)
            {
                return Results.NotFound(new { error = "Player not found in game" });
            }

            // Get player's hand
            var hand = currentRound.Hands.FirstOrDefault(h => h.PlayerSessionId == playerSession.Id);
            if (hand == null)
            {
                return Results.BadRequest(new { error = "Player is not playing this round" });
            }

            var playerHand = hand.Cards;

            // Get or create current trick
            var currentTrickNumber = currentRound.CurrentTrickNumber == 0 ? 1 : currentRound.CurrentTrickNumber;
            var currentTrick = currentRound.Tricks.FirstOrDefault(t => t.TrickNumber == currentTrickNumber);

            if (currentTrick == null)
            {
                // Create new trick - party player leads first trick
                var partyPlayerSession = game.PlayerSessions.First(ps => ps.UserId == currentRound.PartyPlayerUserId);
                currentTrick = new Trick
                {
                    RoundId = currentRound.Id,
                    TrickNumber = currentTrickNumber,
                    LeadPlayerSessionId = currentTrickNumber == 1 ? partyPlayerSession.Id : currentRound.Tricks
                        .Where(t => t.TrickNumber == currentTrickNumber - 1)
                        .First().WinnerPlayerSessionId!.Value
                };
                db.Tricks.Add(currentTrick);
                currentRound.CurrentTrickNumber = currentTrickNumber;
            }

            var cardsPlayed = currentTrick.CardsPlayed;

            // Determine whose turn it is
            var activePlayers = game.PlayerSessions
                .Where(ps => currentRound.Hands.Any(h => h.PlayerSessionId == ps.Id))
                .OrderBy(ps => ps.Position)
                .ToList();

            var expectedPlayerSessionId = currentTrick.LeadPlayerSessionId;
            if (cardsPlayed.Count > 0)
            {
                var lastPlayerSessionId = cardsPlayed.Last().PlayerSessionId;
                var lastPlayerPosition = activePlayers.First(p => p.Id == lastPlayerSessionId).Position;
                var nextPosition = trickService.GetNextPlayerPosition(lastPlayerPosition, activePlayers.Select(p => p.Position).ToList());
                expectedPlayerSessionId = activePlayers.First(p => p.Position == nextPosition).Id;
            }

            if (expectedPlayerSessionId != playerSession.Id)
            {
                return Results.Json(new { error = "Not your turn" }, statusCode: 403);
            }

            // Get Ace of trump for validation
            var aceOfTrump = new Card { Rank = "Ace", Suit = currentRound.TrumpSuit.ToString() };

            // Validate card play
            var (isValid, errorMessage) = trickService.ValidateCardPlay(
                request.Card,
                playerHand,
                cardsPlayed,
                currentRound.TrumpSuit,
                aceOfTrump);

            if (!isValid)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            // Add card to trick
            cardsPlayed.Add(new CardPlayed
            {
                PlayerSessionId = playerSession.Id,
                Card = request.Card
            });
            currentTrick.CardsPlayed = cardsPlayed;

            // Remove card from player's hand
            playerHand.Remove(playerHand.First(c => c.Rank == request.Card.Rank && c.Suit == request.Card.Suit));
            hand.CardsJson = JsonSerializer.Serialize(playerHand);

            // Check if trick is completed (all active players have played)
            var trickCompleted = cardsPlayed.Count == activePlayers.Count;

            if (!trickCompleted)
            {
                // Trick not completed, return next player
                await db.SaveChangesAsync();

                // Broadcast card played event
                await gameEvents.BroadcastCardPlayedAsync(
                    game.Id.ToString(),
                    playerSession.Position,
                    request.Card.Rank,
                    request.Card.Suit,
                    currentTrickNumber);

                var nextPosition = trickService.GetNextPlayerPosition(playerSession.Position, activePlayers.Select(p => p.Position).ToList());

                return Results.Ok(new PlayCardResponse
                {
                    RoundId = currentRound.Id,
                    TrickNumber = currentTrickNumber,
                    Card = request.Card,
                    TrickCompleted = false,
                    NextPlayerPosition = nextPosition,
                    Winner = null,
                    NextTrickLeader = null,
                    RoundCompleted = false,
                    Scores = null,
                    GameCompleted = false
                });
            }

            // Trick completed - determine winner
            var winnerSessionId = trickService.DetermineTrickWinner(cardsPlayed, currentRound.TrumpSuit);
            currentTrick.WinnerPlayerSessionId = winnerSessionId;
            currentTrick.CompletedAt = DateTime.UtcNow;

            var winnerSession = activePlayers.First(p => p.Id == winnerSessionId);

            // Check if round is completed (all 5 tricks played)
            var roundCompleted = currentTrickNumber == 5;

            if (!roundCompleted)
            {
                // Round continues - prepare for next trick
                currentRound.CurrentTrickNumber++;
                await db.SaveChangesAsync();

                // Broadcast card played event
                await gameEvents.BroadcastCardPlayedAsync(
                    game.Id.ToString(),
                    playerSession.Position,
                    request.Card.Rank,
                    request.Card.Suit,
                    currentTrickNumber);

                // Broadcast trick completed event
                await gameEvents.BroadcastTrickCompletedAsync(
                    game.Id.ToString(),
                    currentTrickNumber,
                    winnerSession.Position,
                    cardsPlayed.Select(cp =>
                    {
                        var ps = activePlayers.First(p => p.Id == cp.PlayerSessionId);
                        return (Position: ps.Position, Rank: cp.Card.Rank, Suit: cp.Card.Suit);
                    }).ToList());

                return Results.Ok(new PlayCardResponse
                {
                    RoundId = currentRound.Id,
                    TrickNumber = currentTrickNumber,
                    Card = request.Card,
                    TrickCompleted = true,
                    NextPlayerPosition = null,
                    Winner = new TrickWinner
                    {
                        Position = winnerSession.Position,
                        UserId = winnerSession.UserId,
                        DisplayName = winnerSession.User!.DisplayName
                    },
                    NextTrickLeader = winnerSession.Position,
                    RoundCompleted = false,
                    Scores = null,
                    GameCompleted = false
                });
            }

            // Round completed - calculate scores
            currentRound.Status = RoundStatus.Completed;
            currentRound.CompletedAt = DateTime.UtcNow;

            var partyPlayerSessionId = game.PlayerSessions.First(ps => ps.UserId == currentRound.PartyPlayerUserId).Id;
            var roundScores = trickService.CalculateRoundScores(
                activePlayers,
                currentRound.Tricks.ToList(),
                currentRound.TrickValue,
                partyPlayerSessionId);

            // Update player points and create score history
            var scoreResponses = new List<RoundScore>();
            foreach (var (PlayerSessionId, TricksWon, PointsChange, Reason) in roundScores)
            {
                var player = activePlayers.First(p => p.Id == PlayerSessionId);
                player.CurrentPoints += PointsChange;

                var scoreHistory = new ScoreHistory
                {
                    GameId = game.Id,
                    PlayerSessionId = PlayerSessionId,
                    RoundId = currentRound.Id,
                    PointsChange = PointsChange,
                    PointsAfter = player.CurrentPoints,
                    Reason = Reason,
                    CreatedAt = DateTime.UtcNow
                };
                db.ScoreHistories.Add(scoreHistory);

                scoreResponses.Add(new RoundScore
                {
                    Position = player.Position,
                    PointsChange = PointsChange,
                    PointsAfter = player.CurrentPoints,
                    TricksWon = TricksWon,
                    Penalty = Reason == ScoreReason.NoTricksNormalPenalty || Reason == ScoreReason.NoTricksPartyPenalty,
                    IsPartyPlayer = PlayerSessionId == partyPlayerSessionId
                });
            }

            // Check if game is completed
            var gameCompleted = trickService.IsGameComplete(game.PlayerSessions.ToList());
            if (gameCompleted)
            {
                game.Status = GameStatus.Completed;
                game.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                var activePlayersForNextRound = game.PlayerSessions
                    .Where(ps => ps.IsActive)
                    .OrderBy(ps => ps.Position)
                    .ToList();

                if (activePlayersForNextRound.Count > 0)
                {
                    var nextPartyPlayer = GetNextPartyPlayer(activePlayersForNextRound, currentRound.PartyPlayerUserId);
                    var nextRoundNumber = currentRound.RoundNumber + 1;
                    var nextRoundDeck = CardDealingService.CreateDeck();
                    CardDealingService.ShuffleDeck(nextRoundDeck);

                    var nextRound = new Round
                    {
                        GameId = game.Id,
                        RoundNumber = nextRoundNumber,
                        PartyPlayerUserId = nextPartyPlayer.UserId,
                        Status = RoundStatus.TrumpSelection,
                        TrumpSuit = TrumpSuit.Hearts,
                        TrumpSelectedBeforeDealing = false,
                        TrickValue = 0,
                        CurrentTrickNumber = 0,
                        StartedAt = DateTime.UtcNow,
                        Deck = nextRoundDeck
                    };

                    db.Rounds.Add(nextRound);
                    game.CurrentRoundNumber = nextRoundNumber;
                }
            }

            await db.SaveChangesAsync();

            // Broadcast card played event
            await gameEvents.BroadcastCardPlayedAsync(
                game.Id.ToString(),
                playerSession.Position,
                request.Card.Rank,
                request.Card.Suit,
                currentTrickNumber);

            // Broadcast trick completed event
            await gameEvents.BroadcastTrickCompletedAsync(
                game.Id.ToString(),
                currentTrickNumber,
                winnerSession.Position,
                cardsPlayed.Select(cp =>
                {
                    var ps = activePlayers.First(p => p.Id == cp.PlayerSessionId);
                    return (Position: ps.Position, Rank: cp.Card.Rank, Suit: cp.Card.Suit);
                }).ToList());

            // Broadcast round completed event
            await gameEvents.BroadcastRoundCompletedAsync(
                game.Id.ToString(),
                currentRound.Id.ToString(),
                currentRound.RoundNumber,
                scoreResponses.Select(s => (
                    Position: s.Position,
                    PointsChange: s.PointsChange,
                    PointsAfter: s.PointsAfter,
                    TricksWon: s.TricksWon,
                    IsPenalty: s.Penalty,
                    IsPartyPlayer: s.IsPartyPlayer
                )).ToList());

            // Broadcast game completed event if game is over
            if (gameCompleted)
            {
                var winner = game.PlayerSessions
                    .OrderBy(ps => ps.CurrentPoints)
                    .First();

                await gameEvents.BroadcastGameCompletedAsync(
                    game.Id.ToString(),
                    winner.Position,
                    winner.UserId.ToString(),
                    game.CompletedAt!.Value,
                    game.PlayerSessions.Select(ps => (
                        Position: ps.Position,
                        UserId: ps.UserId.ToString(),
                        FinalPoints: ps.CurrentPoints,
                        PrizeWon: Math.Max(0, 20 - ps.CurrentPoints) * 0.05
                    )).ToList());
            }

            return Results.Ok(new PlayCardResponse
            {
                RoundId = currentRound.Id,
                TrickNumber = currentTrickNumber,
                Card = request.Card,
                TrickCompleted = true,
                NextPlayerPosition = null,
                Winner = new TrickWinner
                {
                    Position = winnerSession.Position,
                    UserId = winnerSession.UserId,
                    DisplayName = winnerSession.User!.DisplayName
                },
                NextTrickLeader = null,
                RoundCompleted = true,
                Scores = scoreResponses,
                GameCompleted = gameCompleted
            });
        })
        .RequireAuthorization()
        .WithName("PlayCard");

        return app;
    }

    private static List<Card> EnsureRoundDeck(Round currentRound, IEnumerable<Card> knownCards)
    {
        var existingDeck = currentRound.Deck;
        if (existingDeck.Count > 0)
        {
            return existingDeck;
        }

        var deck = CardDealingService.CreateDeck();

        foreach (var card in knownCards)
        {
            var cardToRemove = deck.FirstOrDefault(c => c.Suit == card.Suit && c.Rank == card.Rank);
            if (cardToRemove != null)
            {
                deck.Remove(cardToRemove);
            }
        }

        CardDealingService.ShuffleDeck(deck);
        currentRound.Deck = deck;
        return deck;
    }

    // Determines the next party player by position order (counter-clockwise).
    private static PlayerSession GetNextPartyPlayer(IReadOnlyList<PlayerSession> activePlayers, Guid currentPartyUserId)
    {
        var currentIndex = -1;
        for (var index = 0; index < activePlayers.Count; index++)
        {
            if (activePlayers[index].UserId == currentPartyUserId)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex == -1)
        {
            return activePlayers[0];
        }

        var nextIndex = (currentIndex + 1) % activePlayers.Count;
        return activePlayers[nextIndex];
    }
}
