import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Game, GameResponse } from '../../services/game';
import { Auth } from '../../services/auth';
import { CommonModule } from '@angular/common';
import * as grpcWeb from 'grpc-web';
import { GameEvent, GameEventsClient } from '../../services/grpc/game-events';

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
  private gameStream?: grpcWeb.ClientReadableStream<GameEvent>;
  private isRefreshingToken = false;

  constructor(
    private gameService: Game,
    private authService: Auth,
    private route: ActivatedRoute,
    private router: Router,
    private gameEventsClient: GameEventsClient
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
    this.gameStream?.cancel();
  }

  private loadGame() {
    this.loading.set(true);
    this.error.set(null);

    this.gameService.getGame(this.gameId).subscribe({
      next: (game) => {
        this.game.set(game);
        this.loading.set(false);
        this.checkIfCreator(game);

        if (game.status === 1) {
          this.router.navigate(['/game-board', game.id]);
        }
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
    this.isCreator.set(currentUser?.id === game.createdBy);
  }

  private connectGameEvents() {
    const accessToken = this.authService.getAccessToken();
    if (!accessToken) {
      this.refreshTokenAndReconnect(() => this.connectGameEvents());
      return;
    }

    this.gameStream?.cancel();
    this.gameStream = this.gameEventsClient.subscribeGame(this.gameId, accessToken);

    this.gameStream.on('data', () => {
      this.loadGame();
    });

    this.gameStream.on('error', (error: grpcWeb.RpcError) => {
      console.error('Game event stream error:', error);
      this.handleStreamError(error, () => this.connectGameEvents());
    });
  }

  private handleStreamError(error: grpcWeb.RpcError, reconnect: () => void): void {
    if (error.code !== grpcWeb.StatusCode.UNAUTHENTICATED) {
      return;
    }

    this.refreshTokenAndReconnect(reconnect);
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
