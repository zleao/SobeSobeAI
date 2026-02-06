import { Injectable } from '@angular/core';
import * as grpcWeb from 'grpc-web';
import { concatBytes, decodeFields, encodeInt32Field, encodeStringField } from './proto-utils';

export const gameEventTypes = {
  playerJoined: 0,
  playerLeft: 1,
  gameStarted: 2,
  roundStarted: 3,
  trumpSelected: 4,
  cardDealt: 5,
  playDecision: 6,
  cardsExchanged: 7,
  cardPlayed: 8,
  trickCompleted: 9,
  roundCompleted: 10,
  gameCompleted: 11,
  playerTurn: 12,
  error: 99,
} as const;

export class SubscribeRequest {
  gameId = '';
  accessToken = '';

  constructor(...args: unknown[]) {
    const init = args[0] as Partial<SubscribeRequest> | undefined;
    if (init) {
      Object.assign(this, init);
    }
  }

  static deserializeBinary(bytes: Uint8Array): SubscribeRequest {
    const request = new SubscribeRequest();
    const decoder = new TextDecoder();
    for (const field of decodeFields(bytes)) {
      if (field.fieldNumber === 1 && field.wireType === 2) {
        request.gameId = decoder.decode(field.value as Uint8Array);
      }
      if (field.fieldNumber === 2 && field.wireType === 2) {
        request.accessToken = decoder.decode(field.value as Uint8Array);
      }
    }
    return request;
  }

  serializeBinary(): Uint8Array {
    return concatBytes(
      encodeStringField(1, this.gameId),
      encodeStringField(2, this.accessToken)
    );
  }
}

export class GameEvent {
  gameId = '';
  type = 0;

  constructor(...args: unknown[]) {
    const init = args[0] as Partial<GameEvent> | undefined;
    if (init) {
      Object.assign(this, init);
    }
  }

  static deserializeBinary(bytes: Uint8Array): GameEvent {
    const event = new GameEvent();
    const decoder = new TextDecoder();
    for (const field of decodeFields(bytes)) {
      if (field.fieldNumber === 1 && field.wireType === 2) {
        event.gameId = decoder.decode(field.value as Uint8Array);
      }
      if (field.fieldNumber === 2 && field.wireType === 0) {
        event.type = Number(field.value);
      }
    }
    return event;
  }

  serializeBinary(): Uint8Array {
    return concatBytes(
      encodeStringField(1, this.gameId),
      encodeInt32Field(2, this.type)
    );
  }
}

@Injectable({
  providedIn: 'root',
})
export class GameEventsClient {
  private readonly client = new grpcWeb.GrpcWebClientBase({ format: 'binary' });
  private readonly hostname = globalThis.location?.origin ?? '';

  subscribeGame(gameId: string, accessToken: string): grpcWeb.ClientReadableStream<GameEvent> {
    const request = new SubscribeRequest({ gameId, accessToken });
    const metadata = {
      authorization: `Bearer ${accessToken}`,
      'x-access-token': accessToken,
    };
    return this.client.serverStreaming(
      `${this.hostname}/sobesobe.game.GameEvents/Subscribe`,
      request,
      metadata,
      methodDescriptorSubscribe
    );
  }
}

const methodDescriptorSubscribe = new grpcWeb.MethodDescriptor(
  '/sobesobe.game.GameEvents/Subscribe',
  grpcWeb.MethodType.SERVER_STREAMING,
  SubscribeRequest,
  GameEvent,
  (request: SubscribeRequest) => request.serializeBinary(),
  GameEvent.deserializeBinary
);
