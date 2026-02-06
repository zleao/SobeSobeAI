import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { Auth } from '../../services/auth';
import { Game, GameListItem } from '../../services/game';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import * as grpcWeb from 'grpc-web';
import { LobbyEvent, LobbyEventsClient } from '../../services/grpc/lobby-events';

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
  showCreateGameModal = signal(false);
  selectedMaxPlayers = signal<number>(5);
  private lobbyStream?: grpcWeb.ClientReadableStream<LobbyEvent>;
  private isRefreshingToken = false;

  constructor(
    public authService: Auth,
    private gameService: Game,
    private router: Router,
    private lobbyEventsClient: LobbyEventsClient
  ) {}

  ngOnInit(): void {
    this.loadGames();
    this.connectLobbyEvents();
  }

  ngOnDestroy(): void {
    this.lobbyStream?.cancel();
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
    this.gameService.listGames().subscribe({
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
    const accessToken = this.authService.getAccessToken();
    if (!accessToken) {
      this.refreshTokenAndReconnect(() => this.connectLobbyEvents());
      return;
    }

    this.lobbyStream?.cancel();
    this.lobbyStream = this.lobbyEventsClient.subscribeLobby(accessToken);

    this.lobbyStream.on('data', () => {
      this.loadGames({ showErrors: false });
    });

    this.lobbyStream.on('error', (error: grpcWeb.RpcError) => {
      console.error('Lobby event stream error:', error);
      this.handleStreamError(error, () => this.connectLobbyEvents());
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

  getStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Waiting';
      case 1: return 'In Progress';
      case 2: return 'Completed';
      case 3: return 'Abandoned';
      default: return 'Unknown';
    }
  }
}
