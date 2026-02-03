import { Component, signal, OnInit } from '@angular/core';
import { Auth } from '../../services/auth';
import { Game, GameListItem, CreateGameRequest } from '../../services/game';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';

@Component({
  selector: 'app-lobby',
  imports: [CommonModule],
  templateUrl: './lobby.html',
  styleUrl: './lobby.scss',
})
export class Lobby implements OnInit {
  games = signal<GameListItem[]>([]);
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  showCreateGameModal = signal(false);

  constructor(
    public authService: Auth,
    private gameService: Game,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadGames();
  }

  loadGames(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    // List only waiting games
    this.gameService.listGames(0).subscribe({
      next: (response) => {
        this.games.set(response.games);
        this.isLoading.set(false);
      },
      error: (error) => {
        this.errorMessage.set('Failed to load games. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  onCreateGame(): void {
    this.isLoading.set(true);
    this.gameService.createGame(5).subscribe({
      next: (game) => {
        this.isLoading.set(false);
        // Navigate to game room after creating
        this.router.navigate(['/game-room', game.id]);
      },
      error: (error) => {
        this.errorMessage.set('Failed to create game. Please try again.');
        this.isLoading.set(false);
      }
    });
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
