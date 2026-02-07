import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface GameListItem {
  id: string;
  createdBy: UserSummary;
  status: GameStatus; // 0=Waiting, 1=InProgress, 2=Completed, 3=Abandoned
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
  userId: string;
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
  createdBy: string;
  createdByUsername: string;
  status: GameStatus;
  maxPlayers: number;
  currentPlayerCount: number;
  currentDealerIndex: number | null;
  currentRoundNumber: number;
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
}

export interface Card {
  suit: string; // Hearts, Diamonds, Clubs, Spades
  rank: string; // Ace, 7, King, Queen, Jack, 6, 5, 4, 3, 2
}

export interface GameStateResponse {
  id: string;
  createdBy: string;
  createdByUsername: string;
  status: GameStatus;
  maxPlayers: number;
  currentPlayerCount: number;
  currentDealerIndex: number | null;
  currentRoundNumber: number;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  players: PlayerStateResponse[];
  currentRound: RoundStateResponse | null;
}

export interface PlayerStateResponse {
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
  hand?: Card[] | null;
}

export interface RoundStateResponse {
  id: string;
  roundNumber: number;
  dealerUserId: string;
  partyPlayerUserId: string;
  trumpSuit: string;
  trumpSelectedBeforeDealing: boolean;
  trickValue: number;
  currentTrickNumber: number;
  status: RoundStatus; // 0=Dealing, 1=TrumpSelection, 2=PlayerDecisions, 3=CardExchange, 4=Playing, 5=Completed
  tricks: TrickStateResponse[];
  currentTrick: TrickStateResponse | null;
  startedAt: string;
  completedAt: string | null;
}

export interface TrickStateResponse {
  id: string;
  trickNumber: number;
  leadPlayerSessionId: string;
  winnerPlayerSessionId: string | null;
  cardsPlayed: CardPlayedResponse[];
  completedAt: string | null;
}

export interface CardPlayedResponse {
  playerSessionId: string;
  playerPosition: number;
  card: Card;
}

export type GameStatus = 'Waiting' | 'InProgress' | 'Completed' | 'Abandoned' | 0 | 1 | 2 | 3;
export type RoundStatus = 'Dealing' | 'TrumpSelection' | 'PlayerDecisions' | 'CardExchange' | 'Playing' | 'Completed'
  | 0 | 1 | 2 | 3 | 4 | 5;

export function getGameStatusValue(status: GameStatus): number {
  if (typeof status === 'number') {
    return status;
  }

  switch (status) {
    case 'Waiting':
      return 0;
    case 'InProgress':
      return 1;
    case 'Completed':
      return 2;
    case 'Abandoned':
      return 3;
    default:
      return -1;
  }
}

export function getRoundStatusValue(status: RoundStatus): number {
  if (typeof status === 'number') {
    return status;
  }

  switch (status) {
    case 'Dealing':
      return 0;
    case 'TrumpSelection':
      return 1;
    case 'PlayerDecisions':
      return 2;
    case 'CardExchange':
      return 3;
    case 'Playing':
      return 4;
    case 'Completed':
      return 5;
    default:
      return -1;
  }
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

  listGames(options?: {
    status?: number;
    availableOnly?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<ListGamesResponse> {
    const page = options?.page ?? 1;
    const pageSize = options?.pageSize ?? 20;
    let url = `${this.API_URL}/games?page=${page}&pageSize=${pageSize}`;
    if (options?.status !== undefined) {
      url += `&status=${options.status}`;
    }
    if (options?.availableOnly !== undefined) {
      url += `&availableOnly=${options.availableOnly}`;
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

  abandonGame(id: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/abandon`, {});
  }

  startGame(id: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/start`, {});
  }

  getGameState(id: string): Observable<GameStateResponse> {
    return this.http.get<GameStateResponse>(`${this.API_URL}/games/${id}/state`);
  }

  makePlayDecision(id: string, willPlay: boolean): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/rounds/current/play-decision`, { willPlay });
  }

  exchangeCards(id: string, cards: Card[]): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/rounds/current/exchange-cards`, { cardsToExchange: cards });
  }

  selectTrump(id: string, request: SelectTrumpRequest): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/rounds/current/trump`, request);
  }

  playCard(id: string, request: PlayCardRequest): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/games/${id}/rounds/current/play-card`, request);
  }
}
