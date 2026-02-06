import { Injectable } from '@angular/core';
import * as grpcWeb from 'grpc-web';
import { concatBytes, decodeFields, encodeInt32Field, encodeStringField } from './proto-utils';

export const lobbyEventTypes = {
  lobbyListChanged: 0,
  gameCreated: 1,
  gameUpdated: 2,
  gameRemoved: 3,
} as const;

export class LobbySubscribeRequest {
  accessToken = '';

  constructor(...args: unknown[]) {
    const init = args[0] as Partial<LobbySubscribeRequest> | undefined;
    if (init) {
      Object.assign(this, init);
    }
  }

  static deserializeBinary(bytes: Uint8Array): LobbySubscribeRequest {
    const request = new LobbySubscribeRequest();
    const decoder = new TextDecoder();
    for (const field of decodeFields(bytes)) {
      if (field.fieldNumber === 1 && field.wireType === 2) {
        request.accessToken = decoder.decode(field.value as Uint8Array);
      }
    }
    return request;
  }

  serializeBinary(): Uint8Array {
    return concatBytes(encodeStringField(1, this.accessToken));
  }
}

export class LobbyEvent {
  type = 0;
  gameId = '';

  constructor(...args: unknown[]) {
    const init = args[0] as Partial<LobbyEvent> | undefined;
    if (init) {
      Object.assign(this, init);
    }
  }

  static deserializeBinary(bytes: Uint8Array): LobbyEvent {
    const event = new LobbyEvent();
    const decoder = new TextDecoder();
    for (const field of decodeFields(bytes)) {
      if (field.fieldNumber === 1 && field.wireType === 0) {
        event.type = Number(field.value);
      }
      if (field.fieldNumber === 3 && field.wireType === 2) {
        event.gameId = decoder.decode(field.value as Uint8Array);
      }
    }
    return event;
  }

  serializeBinary(): Uint8Array {
    return concatBytes(
      encodeInt32Field(1, this.type),
      encodeStringField(3, this.gameId)
    );
  }
}

@Injectable({
  providedIn: 'root',
})
export class LobbyEventsClient {
  private readonly client = new grpcWeb.GrpcWebClientBase({ format: 'binary' });
  private readonly hostname = globalThis.location?.origin ?? '';

  subscribeLobby(accessToken: string): grpcWeb.ClientReadableStream<LobbyEvent> {
    const request = new LobbySubscribeRequest({ accessToken });
    const metadata = {
      authorization: `Bearer ${accessToken}`,
      'x-access-token': accessToken,
    };
    return this.client.serverStreaming(
      `${this.hostname}/sobesobe.game.LobbyEvents/SubscribeLobby`,
      request,
      metadata,
      methodDescriptorSubscribeLobby
    );
  }
}

const methodDescriptorSubscribeLobby = new grpcWeb.MethodDescriptor(
  '/sobesobe.game.LobbyEvents/SubscribeLobby',
  grpcWeb.MethodType.SERVER_STREAMING,
  LobbySubscribeRequest,
  LobbyEvent,
  (request: LobbySubscribeRequest) => request.serializeBinary(),
  LobbyEvent.deserializeBinary
);
