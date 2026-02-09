import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Auth } from '../auth';
import type { RealtimeEvent } from './game-realtime';

@Injectable({
  providedIn: 'root',
})
export class LobbyRealtime {
  private connection?: HubConnection;
  private startPromise?: Promise<void>;
  private isStopping = false;

  constructor(private authService: Auth) {}

  async connect(
    onEvent: (event: RealtimeEvent) => void,
    onClose?: (error?: Error) => void
  ): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      return;
    }

    await this.disconnect();

    const origin = globalThis.location?.origin ?? '';
    const url = `${origin}/hubs/lobby`;

    this.connection = new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: async () => (await this.getAccessToken()) ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('LobbyEvent', (event: RealtimeEvent) => onEvent(event));
    this.connection.onreconnecting(error => {
      console.warn('Lobby hub reconnecting...', error ?? '');
    });
    this.connection.onreconnected(connectionId => {
      console.info('Lobby hub reconnected', connectionId ?? '');
    });
    this.connection.onclose(error => {
      if (this.isUnauthorizedError(error)) {
        this.authService.logout();
      }
      onClose?.(error ?? undefined);
    });

    this.startPromise = this.connection.start();
    try {
      await this.startPromise;
    } catch (error) {
      if (this.isStopping) {
        return;
      }
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      return;
    }

    this.isStopping = true;
    try {
      if (this.startPromise) {
        await this.startPromise;
      }
    } catch {
      // Ignore start errors when stopping.
    }

    if (this.connection.state !== HubConnectionState.Disconnected) {
      await this.connection.stop();
    }

    this.connection = undefined;
    this.startPromise = undefined;
    this.isStopping = false;
  }

  private async getAccessToken(): Promise<string | null> {
    return this.authService.getValidAccessToken();
  }

  private isUnauthorizedError(error?: Error): boolean {
    if (!error?.message) {
      return false;
    }

    return /401|unauthorized/i.test(error.message);
  }
}
