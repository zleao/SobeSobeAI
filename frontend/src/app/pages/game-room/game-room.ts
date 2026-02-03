import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Game, GameResponse } from '../../services/game';
import { Auth } from '../../services/auth';
import { CommonModule } from '@angular/common';
import { interval, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-game-room',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './game-room.html',
})
export class GameRoom implements OnInit, OnDestroy {
  game = signal<GameResponse | null>(null);
  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  isCreator = signal<boolean>(false);
  startingGame = signal<boolean>(false);
  leavingGame = signal<boolean>(false);
  
  private pollSubscription?: Subscription;
  private gameId: string = '';

  constructor(
    private gameService: Game,
    private authService: Auth,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit() {
    // Get game ID from route parameter
    this.gameId = this.route.snapshot.paramMap.get('id') || '';
    
    if (!this.gameId) {
      this.router.navigate(['/lobby']);
      return;
    }

    // Load game details
    this.loadGame();

    // Poll for updates every 2 seconds
    this.pollSubscription = interval(2000)
      .pipe(switchMap(() => this.gameService.getGame(this.gameId)))
      .subscribe({
        next: (game) => {
          this.game.set(game);
          this.checkIfCreator(game);
          
          // If game has started, navigate to game board
          if (game.status === 1) { // InProgress
            this.router.navigate(['/game', game.id]);
          }
        },
        error: (err) => {
          console.error('Error polling game:', err);
          // If game not found, navigate back to lobby
          if (err.status === 404) {
            this.router.navigate(['/lobby']);
          }
        }
      });
  }

  ngOnDestroy() {
    this.pollSubscription?.unsubscribe();
  }

  private loadGame() {
    this.loading.set(true);
    this.error.set(null);

    this.gameService.getGame(this.gameId).subscribe({
      next: (game) => {
        this.game.set(game);
        this.loading.set(false);
        this.checkIfCreator(game);
      },
      error: (err) => {
        console.error('Error loading game:', err);
        this.error.set('Failed to load game details');
        this.loading.set(false);
        
        // If game not found, navigate back to lobby
        if (err.status === 404) {
          this.router.navigate(['/lobby']);
        }
      }
    });
  }

  private checkIfCreator(game: GameResponse) {
    const currentUser = this.authService.currentUser();
    this.isCreator.set(currentUser?.id === game.createdBy.id);
  }

  startGame() {
    if (!this.isCreator() || this.startingGame()) {
      return;
    }

    this.startingGame.set(true);
    this.error.set(null);

    this.gameService.startGame(this.gameId).subscribe({
      next: () => {
        // Will be redirected by polling when status changes
        this.startingGame.set(false);
      },
      error: (err) => {
        console.error('Error starting game:', err);
        this.error.set(err.error?.message || 'Failed to start game');
        this.startingGame.set(false);
      }
    });
  }

  leaveGame() {
    if (this.leavingGame()) {
      return;
    }

    this.leavingGame.set(true);
    this.error.set(null);

    this.gameService.leaveGame(this.gameId).subscribe({
      next: () => {
        this.router.navigate(['/lobby']);
      },
      error: (err) => {
        console.error('Error leaving game:', err);
        this.error.set(err.error?.message || 'Failed to leave game');
        this.leavingGame.set(false);
      }
    });
  }

  getStatusBadgeClass(): string {
    const game = this.game();
    if (!game) return '';

    switch (game.status) {
      case 0: return 'bg-yellow-100 text-yellow-800';
      case 1: return 'bg-green-100 text-green-800';
      case 2: return 'bg-blue-100 text-blue-800';
      case 3: return 'bg-red-100 text-red-800';
      default: return '';
    }
  }

  getStatusText(): string {
    const game = this.game();
    if (!game) return '';

    switch (game.status) {
      case 0: return 'Waiting';
      case 1: return 'In Progress';
      case 2: return 'Completed';
      case 3: return 'Abandoned';
      default: return 'Unknown';
    }
  }
}
