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
        this.error.set(err.error?.error?.message || 'Failed to select trump');
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
      return myPlayer.position === currentTrick.leadPlayerPosition;
    } else if (cardsPlayedCount < state.players.length) {
      // Find next player in counter-clockwise order
      const lastPlayerPosition = currentTrick.cardsPlayed[cardsPlayedCount - 1].position;
      const activePlayers = state.players.filter(p => p.leftAt === null).sort((a, b) => a.position - b.position);
      const lastIndex = activePlayers.findIndex(p => p.position === lastPlayerPosition);
      const nextPlayer = activePlayers[(lastIndex + 1) % activePlayers.length];
      return myPlayer.position === nextPlayer.position;
    }

    return false;
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
}
