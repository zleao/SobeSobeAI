import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { interval, Subscription, switchMap } from 'rxjs';
import { Game, GameStateResponse, Card } from '../../services/game';
import { Auth } from '../../services/auth';

@Component({
  selector: 'app-game-board',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './game-board.html',
})
export class GameBoard implements OnInit, OnDestroy {
  gameState = signal<GameStateResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  selectedCard = signal<Card | null>(null);
  makingDecision = signal(false);
  cardsToExchange = signal<Card[]>([]);
  exchangingCards = signal(false);
  
  private gameId: string = '';
  private pollSubscription?: Subscription;

  constructor(
    private route: ActivatedRoute,
    public router: Router,
    private gameService: Game,
    public authService: Auth
  ) {}

  ngOnInit() {
    this.gameId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.gameId) {
      this.router.navigate(['/lobby']);
      return;
    }

    // Load initial game state
    this.loadGameState();

    // Poll for updates every 2 seconds
    this.pollSubscription = interval(2000)
      .pipe(switchMap(() => this.gameService.getGameState(this.gameId)))
      .subscribe({
        next: (state) => {
          this.gameState.set(state);
          this.loading.set(false);
          
          // Navigate away if game is completed
          if (state.status === 2) { // Completed
            this.router.navigate(['/lobby']);
          }
        },
        error: (err) => {
          console.error('Error polling game state:', err);
          if (err.status === 404) {
            this.router.navigate(['/lobby']);
          }
        },
      });
  }

  ngOnDestroy() {
    this.pollSubscription?.unsubscribe();
  }

  loadGameState() {
    this.loading.set(true);
    this.gameService.getGameState(this.gameId).subscribe({
      next: (state) => {
        this.gameState.set(state);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading game state:', err);
        this.error.set('Failed to load game state');
        this.loading.set(false);
        if (err.status === 404) {
          this.router.navigate(['/lobby']);
        }
      },
    });
  }

  selectCard(card: Card) {
    if (this.selectedCard()?.suit === card.suit && this.selectedCard()?.rank === card.rank) {
      this.selectedCard.set(null); // Deselect if clicking the same card
    } else {
      this.selectedCard.set(card);
    }
  }

  playSelectedCard() {
    const card = this.selectedCard();
    if (!card) return;

    this.gameService.playCard(this.gameId, { card }).subscribe({
      next: () => {
        this.selectedCard.set(null);
        this.loadGameState(); // Refresh state after playing
      },
      error: (err) => {
        console.error('Error playing card:', err);
        this.error.set(err.error?.error?.message || 'Failed to play card');
        setTimeout(() => this.error.set(null), 3000);
      },
    });
  }

  selectTrump(suit: string, beforeDealing: boolean) {
    this.gameService.selectTrump(this.gameId, { trumpSuit: suit, selectedBeforeDealing: beforeDealing }).subscribe({
      next: () => {
        this.loadGameState();
      },
      error: (err) => {
        console.error('Error selecting trump:', err);
        this.error.set(err.error?.error || err.error?.message || 'Failed to select trump');
        setTimeout(() => this.error.set(null), 3000);
      },
    });
  }

  getCardSymbol(suit: string): string {
    const symbols: { [key: string]: string } = {
      Hearts: '♥',
      Diamonds: '♦',
      Clubs: '♣',
      Spades: '♠',
    };
    return symbols[suit] || '';
  }

  getCardColor(suit: string): string {
    return suit === 'Hearts' || suit === 'Diamonds' ? 'text-red-600' : 'text-gray-900';
  }

  getRankDisplay(rank: string): string {
    const rankMap: { [key: string]: string } = {
      Ace: 'A',
      King: 'K',
      Queen: 'Q',
      Jack: 'J',
    };
    return rankMap[rank] || rank;
  }

  isMyTurn(): boolean {
    const state = this.gameState();
    const currentUser = this.authService.currentUser();
    if (!state || !currentUser || !state.currentRound) return false;

    const myPlayer = state.players.find(p => p.userId === currentUser.id);
    if (!myPlayer) return false;

    const currentTrick = state.currentRound.currentTrick;
    if (!currentTrick) return false;

    // Determine next player to play
    const cardsPlayedCount = currentTrick.cardsPlayed.length;
    if (cardsPlayedCount === 0) {
      // First card of trick - lead player plays
      return myPlayer.id === currentTrick.leadPlayerSessionId;
    } else {
      // Find next player in counter-clockwise order
      const lastPlayerPosition = currentTrick.cardsPlayed[cardsPlayedCount - 1].playerPosition;
      const activePlayers = state.players
        .filter(p => p.leftAt === null && p.isActive)
        .sort((a, b) => a.position - b.position);
      if (cardsPlayedCount >= activePlayers.length) {
        return false;
      }
      const lastIndex = activePlayers.findIndex(p => p.position === lastPlayerPosition);
      const nextPlayer = activePlayers[(lastIndex + 1) % activePlayers.length];
      return myPlayer.position === nextPlayer.position;
    }
  }

  getRoundStatusText(status: number): string {
    const statusMap: { [key: number]: string } = {
      0: 'Dealing',
      1: 'Trump Selection',
      2: 'Player Decisions',
      3: 'Card Exchange',
      4: 'Playing',
      5: 'Completed',
    };
    return statusMap[status] || 'Unknown';
  }

  makePlayDecision(willPlay: boolean) {
    this.makingDecision.set(true);
    this.gameService.makePlayDecision(this.gameId, willPlay).subscribe({
      next: () => {
        this.makingDecision.set(false);
        this.loadGameState();
      },
      error: (err) => {
        console.error('Error making play decision:', err);
        this.error.set(err.error?.error?.message || 'Failed to make decision');
        setTimeout(() => this.error.set(null), 3000);
        this.makingDecision.set(false);
      },
    });
  }

  isPartyPlayer(): boolean {
    const state = this.gameState();
    const currentUser = this.authService.currentUser();
    if (!state || !currentUser || !state.currentRound) return false;

    return state.currentRound.partyPlayerUserId === currentUser.id;
  }

  isDealer(): boolean {
    const state = this.gameState();
    const currentUser = this.authService.currentUser();
    if (!state || !currentUser || !state.currentRound) return false;

    return state.currentRound.dealerUserId === currentUser.id;
  }

  getMyPoints(): number {
    const state = this.gameState();
    const currentUser = this.authService.currentUser();
    if (!state || !currentUser) return 0;

    const myPlayer = state.players.find(p => p.userId === currentUser.id);
    return myPlayer?.currentPoints || 0;
  }

  getCurrentUserName(): string {
    return this.authService.currentUser()?.displayName || this.authService.currentUser()?.username || 'Unknown player';
  }

  getPartyPlayerName(): string {
    const state = this.gameState();
    if (!state || !state.currentRound) return 'Unknown player';
    const partyPlayer = state.players.find(p => p.userId === state.currentRound!.partyPlayerUserId);
    return partyPlayer?.displayName || partyPlayer?.username || 'Unknown player';
  }

  getMyHand(): Card[] {
    const state = this.gameState();
    const currentUser = this.authService.currentUser();
    if (!state || !currentUser) return [];

    const myPlayer = state.players.find(p => p.userId === currentUser.id);
    return myPlayer?.hand ?? [];
  }

  toggleCardForExchange(card: Card) {
    const cards = this.cardsToExchange();
    const index = cards.findIndex(c => c.suit === card.suit && c.rank === card.rank);
    
    if (index >= 0) {
      // Remove card from selection
      this.cardsToExchange.set([...cards.slice(0, index), ...cards.slice(index + 1)]);
    } else {
      // Add card to selection (max 3)
      if (cards.length < 3) {
        this.cardsToExchange.set([...cards, card]);
      }
    }
  }

  isCardSelectedForExchange(card: Card): boolean {
    return this.cardsToExchange().some(c => c.suit === card.suit && c.rank === card.rank);
  }

  confirmCardExchange() {
    const cards = this.cardsToExchange();
    this.exchangingCards.set(true);

    this.gameService.exchangeCards(this.gameId, cards).subscribe({
      next: () => {
        this.exchangingCards.set(false);
        this.cardsToExchange.set([]);
        this.loadGameState();
      },
      error: (err) => {
        console.error('Error exchanging cards:', err);
        this.error.set(err.error?.error?.message || 'Failed to exchange cards');
        setTimeout(() => this.error.set(null), 3000);
        this.exchangingCards.set(false);
      },
    });
  }

  getWinnerLabel(trick: { winnerPlayerSessionId: string | null }): string {
    if (!trick.winnerPlayerSessionId) {
      return 'Unknown';
    }
    const state = this.gameState();
    if (!state) {
      return 'Unknown';
    }
    const winner = state.players.find(p => p.id === trick.winnerPlayerSessionId);
    if (!winner) {
      return 'Unknown';
    }
    return `${winner.displayName} (Position ${winner.position})`;
  }
}
