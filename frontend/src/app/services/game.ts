import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface GameListItem {
  id: string;
  createdBy: UserSummary;
  status: number; // 0=Waiting, 1=InProgress, 2=Completed, 3=Abandoned
  maxPlayers: number;
  currentPlayers: number;
  players: PlayerSummary[];
  createdAt: string;
}

export interface UserSummary {
  id: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
}

export interface PlayerSummary {
  position: number;
  username: string;
  displayName: string;
}

export interface ListGamesResponse {
  games: GameListItem[];
  page: number;
  pageSize: number;
  totalPages: number;
  totalItems: number;
}

export interface CreateGameRequest {
  maxPlayers: number;
}

export interface GameResponse {
  id: string;
  createdBy: UserSummary;
  status: number;
  maxPlayers: number;
  currentDealerPosition: number | null;
  currentRoundNumber: number | null;
  players: PlayerSessionResponse[];
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface PlayerSessionResponse {
  id: string;
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  position: number;
  currentPoints: number;
  isActive: boolean;
  consecutiveRoundsOut: number;
  joinedAt: string;
  leftAt: string | null;
}

export interface Card {
  suit: string; // Hearts, Diamonds, Clubs, Spades
  rank: string; // Ace, 7, King, Queen, Jack, 6, 5, 4, 3, 2
}

export interface GameStateResponse {
  id: string;
  status: number;
  maxPlayers: number;
  currentDealerPosition: number | null;
  currentRoundNumber: number | null;
  createdBy: UserSummary;
  players: PlayerStateResponse[];
  currentRound: RoundStateResponse | null;
  myHand: Card[] | null;
}

export interface PlayerStateResponse {
  playerSessionId: string;
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  position: number;
  currentPoints: number;
  consecutiveRoundsOut: number;
  joinedAt: string;
  leftAt: string | null;
}

export interface RoundStateResponse {
  roundId: string;
  roundNumber: number;
  dealerPosition: number;
  partyPlayerPosition: number;
  trumpSuit: string | null;
  trumpSelectedBeforeDealing: boolean;
  trickValue: number;
  currentTrickNumber: number;
  status: number; // 0=Dealing, 1=TrumpSelection, 2=PlayerDecisions, 3=CardExchange, 4=Playing, 5=Completed
  tricks: TrickStateResponse[];
  currentTrick: TrickStateResponse | null;
}

export interface TrickStateResponse {
  trickNumber: number;
  leadPlayerPosition: number;
  winnerPosition: number | null;
  cardsPlayed: CardPlayedResponse[];
}

export interface CardPlayedResponse {
  position: number;
  card: Card;
}

export interface SelectTrumpRequest {
  trumpSuit: string;
  selectedBeforeDealing: boolean;
}

export interface PlayCardRequest {
  card: Card;
}

@Injectable({
  providedIn: 'root',
})
export class Game {
  private readonly API_URL = '/api';

  constructor(private http: HttpClient) {}

  listGames(status?: number, page: number = 1, pageSize: number = 20): Observable<ListGamesResponse> {
    let url = `${this.API_URL}/games?page=${page}&pageSize=${pageSize}`;
    if (status !== undefined) {
      url += `&status=${status}`;
    }
    return this.http.get<ListGamesResponse>(url);
  }

  createGame(maxPlayers: number): Observable<GameResponse> {
    return this.http.post<GameResponse>(`${this.API_URL}/games`, { maxPlayers });
  }

  getGame(id: string): Observable<GameResponse> {
    return this.http.get<GameResponse>(`${this.API_URL}/games/${id}`);
  }

  joinGame(id: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/join`, {});
  }

  leaveGame(id: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/leave`, {});
  }

  startGame(id: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/start`, {});
  }

  getGameState(id: string): Observable<GameStateResponse> {
    return this.http.get<GameStateResponse>(`${this.API_URL}/games/${id}/state`);
  }

  selectTrump(id: string, request: SelectTrumpRequest): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/rounds/current/trump`, request);
  }

  playCard(id: string, request: PlayCardRequest): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/rounds/current/play-card`, request);
  }
}
