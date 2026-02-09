import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, firstValueFrom } from 'rxjs';

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
  user: User;
}

export interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
}

export interface User {
  id: string;
  username: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  createdAt: string;
  lastLoginAt: string;
  gamesPlayed: number;
  gamesWon: number;
  totalPointsScored: number;
  totalPrizeWon: number;
}

@Injectable({
  providedIn: 'root',
})
export class Auth {
  private readonly API_URL = '/api';
  private readonly ACCESS_TOKEN_KEY = 'access_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';
  private readonly USER_KEY = 'user';
  private refreshInFlight?: Promise<RefreshTokenResponse>;

  private currentUserSignal = signal<User | null>(this.loadUserFromStorage());
  
  public readonly currentUser = this.currentUserSignal.asReadonly();
  public readonly isAuthenticated = computed(() => this.currentUser() !== null);

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  register(request: RegisterRequest): Observable<User> {
    return this.http.post<User>(`${this.API_URL}/users/register`, request);
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.API_URL}/auth/login`, request).pipe(
      tap(response => {
        this.saveTokens(response.accessToken, response.refreshToken);
        this.setCurrentUser(response.user);
      })
    );
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${this.API_URL}/auth/logout`, { refreshToken }).subscribe();
    }
    this.clearTokens();
    this.currentUserSignal.set(null);
    this.router.navigate(['/login']);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  refreshAccessToken(): Observable<RefreshTokenResponse> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }
    return this.http.post<RefreshTokenResponse>(`${this.API_URL}/auth/refresh`, { refreshToken }).pipe(
      tap(response => {
        this.saveTokens(response.accessToken, response.refreshToken);
      })
    );
  }

  async getValidAccessToken(): Promise<string | null> {
    const token = this.getAccessToken();
    if (token && !this.isTokenExpired(token)) {
      return token;
    }

    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      return null;
    }

    try {
      if (!this.refreshInFlight) {
        this.refreshInFlight = firstValueFrom(this.refreshAccessToken());
      }

      const response = await this.refreshInFlight;
      return response.accessToken;
    } catch {
      this.clearTokens();
      this.currentUserSignal.set(null);
      return null;
    } finally {
      this.refreshInFlight = undefined;
    }
  }

  private saveTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem(this.ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
  }

  private clearTokens(): void {
    localStorage.removeItem(this.ACCESS_TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
  }

  private isTokenExpired(token: string): boolean {
    const payload = this.decodeJwtPayload(token);
    if (!payload?.exp) {
      return true;
    }

    // Allow a small clock skew to reduce edge-case 401s.
    const skewSeconds = 30;
    const nowSeconds = Math.floor(Date.now() / 1000);
    return payload.exp <= nowSeconds + skewSeconds;
  }

  private decodeJwtPayload(token: string): { exp?: number } | null {
    const parts = token.split('.');
    if (parts.length !== 3) {
      return null;
    }

    try {
      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const decoded = atob(payload);
      return JSON.parse(decoded) as { exp?: number };
    } catch {
      return null;
    }
  }

  private setCurrentUser(user: User): void {
    this.currentUserSignal.set(user);
    localStorage.setItem(this.USER_KEY, JSON.stringify(user));
  }

  private loadUserFromStorage(): User | null {
    const userJson = localStorage.getItem(this.USER_KEY);
    if (!userJson) return null;
    try {
      return JSON.parse(userJson) as User;
    } catch {
      return null;
    }
  }
}
