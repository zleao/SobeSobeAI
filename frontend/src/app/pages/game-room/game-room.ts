import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Game, GameResponse, getGameStatusValue } from '../../services/game';
import { Auth } from '../../services/auth';
import { CommonModule } from '@angular/common';
import { GameRealtime } from '../../services/realtime/game-realtime';

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
  
  private gameId: string = '';
  private isRefreshingToken = false;
  private isDestroyed = false;

  constructor(
    private gameService: Game,
    private authService: Auth,
    private route: ActivatedRoute,
    private router: Router,
    private gameRealtime: GameRealtime
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

    this.connectGameEvents();
  }

  ngOnDestroy() {
    this.isDestroyed = true;
    void this.gameRealtime.disconnect();
  }

  private loadGame() {
    this.loading.set(true);
    this.error.set(null);

    this.gameService.getGame(this.gameId).subscribe({
      next: (game) => {
        this.game.set(game);
        this.loading.set(false);
        this.checkIfCreator(game);

        const status = getGameStatusValue(game.status);

        if (status === 1) {
          this.router.navigate(['/game-board', game.id]);
        }

        if (status === 3) {
          this.router.navigate(['/lobby'], {
            state: { redirectMessage: 'Game was abandoned. Redirecting to lobby.' }
          });
        }
      },
      error: (err) => {
        console.error('Error loading game:', err);
        this.error.set('Failed to load game details');
        this.loading.set(false);
        
        // If game not found, navigate back to lobby
        if (err.status === 404) {
          this.router.navigate(['/lobby'], {
            state: { redirectMessage: 'Game was deleted. Redirecting to lobby.' }
          });
        }
      }
    });
  }

  private checkIfCreator(game: GameResponse) {
    const currentUser = this.authService.currentUser();
    this.isCreator.set(currentUser?.id === game.createdBy);
  }

  private connectGameEvents(): void {
    void this.gameRealtime.connect(
      this.gameId,
      () => this.loadGame(),
      error => this.handleRealtimeClose(error)
    ).catch(error => this.handleRealtimeClose(error));
  }

  private handleRealtimeClose(_: Error | undefined): void {
    if (this.isDestroyed) {
      return;
    }

    this.refreshTokenAndReconnect(() => this.connectGameEvents());
  }

  private refreshTokenAndReconnect(reconnect: () => void): void {
    if (this.isRefreshingToken) {
      return;
    }

    this.isRefreshingToken = true;
    this.authService.refreshAccessToken().subscribe({
      next: () => {
        this.isRefreshingToken = false;
        reconnect();
      },
      error: () => {
        this.isRefreshingToken = false;
        this.authService.logout();
      }
    });
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

    const currentGame = this.game();
    const deletingGame = this.isCreator() && currentGame && getGameStatusValue(currentGame.status) === 0;
    if (deletingGame) {
      const confirmed = window.confirm(
        'Leaving will delete the game and remove all players. This cannot be undone. Do you want to continue?'
      );
      if (!confirmed) {
        return;
      }
    }

    this.leavingGame.set(true);
    this.error.set(null);

    this.gameService.leaveGame(this.gameId).subscribe({
      next: () => {
        if (deletingGame) {
          this.router.navigate(['/lobby'], {
            state: { redirectMessage: 'Game was deleted. Redirecting to lobby.' }
          });
          return;
        }

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

    const status = getGameStatusValue(game.status);

    switch (status) {
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

    const status = getGameStatusValue(game.status);

    switch (status) {
      case 0: return 'Waiting';
      case 1: return 'In Progress';
      case 2: return 'Completed';
      case 3: return 'Abandoned';
      default: return 'Unknown';
    }
  }

  getStatusSubtitle(): string {
    const game = this.game();
    if (!game) {
      return '';
    }

    const status = getGameStatusValue(game.status);

    switch (status) {
      case 0:
        return 'Waiting for players to join...';
      case 1:
        return 'Game is in progress.';
      case 2:
        return 'Game has completed.';
      case 3:
        return 'Game was abandoned.';
      default:
        return '';
    }
  }

  getCurrentUserName(): string {
    return this.authService.currentUser()?.displayName
      || this.authService.currentUser()?.username
      || 'Unknown player';
  }
}
