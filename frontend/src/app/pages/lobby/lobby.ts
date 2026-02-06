import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { Auth } from '../../services/auth';
import { Game, GameListItem, GameStatus, getGameStatusValue } from '../../services/game';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { LobbyRealtime } from '../../services/realtime/lobby-realtime';

@Component({
  selector: 'app-lobby',
  imports: [CommonModule],
  templateUrl: './lobby.html',
  styleUrl: './lobby.scss',
})
export class Lobby implements OnInit, OnDestroy {
  games = signal<GameListItem[]>([]);
  isLoadingGames = signal(false);
  isCreatingGame = signal(false);
  isManualRefresh = signal(false);
  errorMessage = signal<string | null>(null);
  toastMessage = signal<string | null>(null);
  showCreateGameModal = signal(false);
  selectedMaxPlayers = signal<number>(5);
  private isRefreshingToken = false;
  private isDestroyed = false;
  private toastTimeoutId?: number;

  constructor(
    public authService: Auth,
    private gameService: Game,
    private router: Router,
    private lobbyRealtime: LobbyRealtime
  ) {}

  ngOnInit(): void {
    this.showRedirectToast();
    this.loadGames();
    this.connectLobbyEvents();
  }

  ngOnDestroy(): void {
    this.isDestroyed = true;
    if (this.toastTimeoutId !== undefined) {
      window.clearTimeout(this.toastTimeoutId);
    }
    void this.lobbyRealtime.disconnect();
  }

  private showRedirectToast(): void {
    const message = history.state?.redirectMessage;
    if (typeof message !== 'string' || message.trim().length === 0) {
      return;
    }

    this.toastMessage.set(message);

    if (this.toastTimeoutId !== undefined) {
      window.clearTimeout(this.toastTimeoutId);
    }

    this.toastTimeoutId = window.setTimeout(() => {
      this.toastMessage.set(null);
    }, 3500);
  }

  loadGames(options?: { manual?: boolean; showErrors?: boolean }): void {
    if (this.isLoadingGames()) {
      return;
    }

    const manual = options?.manual ?? false;
    const showErrors = options?.showErrors ?? true;

    this.isLoadingGames.set(true);
    this.isManualRefresh.set(manual);
    if (showErrors) {
      this.errorMessage.set(null);
    }

    // List all games
    this.gameService.listGames({ availableOnly: true }).subscribe({
      next: (response) => {
        this.games.set(response.games);
        this.isLoadingGames.set(false);
        this.isManualRefresh.set(false);
      },
      error: () => {
        if (showErrors) {
          this.errorMessage.set('Failed to load games. Please try again.');
        }
        this.isLoadingGames.set(false);
        this.isManualRefresh.set(false);
      }
    });
  }

  onRefreshGames(): void {
    this.loadGames({ manual: true });
  }

  private connectLobbyEvents(): void {
    void this.lobbyRealtime.connect(
      () => this.loadGames({ showErrors: false }),
      error => this.handleRealtimeClose(error)
    ).catch(error => this.handleRealtimeClose(error));
  }

  private handleRealtimeClose(_: Error | undefined): void {
    if (this.isDestroyed) {
      return;
    }

    this.refreshTokenAndReconnect(() => this.connectLobbyEvents());
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


  openCreateGameModal(): void {
    this.showCreateGameModal.set(true);
    this.selectedMaxPlayers.set(5); // Default to 5 players
  }

  closeCreateGameModal(): void {
    this.showCreateGameModal.set(false);
  }

  onCreateGame(): void {
    this.isCreatingGame.set(true);
    this.showCreateGameModal.set(false);
    this.gameService.createGame(this.selectedMaxPlayers()).subscribe({
      next: (game) => {
        this.isCreatingGame.set(false);
        // Navigate to game room after creating
        this.router.navigate(['/game-room', game.id]);
      },
      error: () => {
        this.errorMessage.set('Failed to create game. Please try again.');
        this.isCreatingGame.set(false);
      }
    });
  }

  selectMaxPlayers(count: number): void {
    this.selectedMaxPlayers.set(count);
  }

  isCurrentUserInGame(game: GameListItem): boolean {
    const userId = this.authService.currentUser()?.id;
    if (!userId) {
      return false;
    }

    return game.players.some(player => player.userId === userId);
  }

  onOpenGame(gameId: string): void {
    this.router.navigate(['/game-room', gameId]);
  }

  onJoinGame(gameId: string): void {
    this.gameService.joinGame(gameId).subscribe({
      next: () => {
        // Navigate to game room after joining
        this.router.navigate(['/game-room', gameId]);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message || 'Failed to join game.');
      }
    });
  }

  onLogout(): void {
    this.authService.logout();
  }

  getStatusLabel(status: GameStatus): string {
    switch (getGameStatusValue(status)) {
      case 0: return 'Waiting';
      case 1: return 'In Progress';
      case 2: return 'Completed';
      case 3: return 'Abandoned';
      default: return 'Unknown';
    }
  }

  getStatusValue(status: GameStatus): number {
    return getGameStatusValue(status);
  }
}
