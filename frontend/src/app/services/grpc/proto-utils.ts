export type WireValue = string | number | Uint8Array;

export interface DecodedField {
  fieldNumber: number;
  wireType: number;
  value: WireValue;
}

export function encodeStringField(fieldNumber: number, value: string): Uint8Array {
  const tag = encodeVarint((fieldNumber << 3) | 2);
  const payload = new TextEncoder().encode(value);
  const length = encodeVarint(payload.length);
  return concatBytes(tag, length, payload);
}

export function encodeInt32Field(fieldNumber: number, value: number): Uint8Array {
  const tag = encodeVarint((fieldNumber << 3) | 0);
  const payload = encodeVarint(value >>> 0);
  return concatBytes(tag, payload);
}

export function concatBytes(...chunks: Uint8Array[]): Uint8Array {
  const totalLength = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
  const result = new Uint8Array(totalLength);
  let offset = 0;
  for (const chunk of chunks) {
    result.set(chunk, offset);
    offset += chunk.length;
  }
  return result;
}

export function encodeVarint(value: number): Uint8Array {
  const bytes: number[] = [];
  let current = value >>> 0;
  while (current >= 0x80) {
    bytes.push((current & 0x7f) | 0x80);
    current >>>= 7;
  }
  bytes.push(current);
  return new Uint8Array(bytes);
}

export function decodeFields(buffer: Uint8Array): DecodedField[] {
  const fields: DecodedField[] = [];
  let offset = 0;

  while (offset < buffer.length) {
    const tagResult = decodeVarint(buffer, offset);
    const tag = tagResult.value;
    offset = tagResult.offset;

    const fieldNumber = tag >>> 3;
    const wireType = tag & 0x7;

    if (wireType === 2) {
      const lengthResult = decodeVarint(buffer, offset);
      const length = lengthResult.value;
      offset = lengthResult.offset;
      const value = buffer.slice(offset, offset + length);
      offset += length;
      fields.push({ fieldNumber, wireType, value });
      continue;
    }

    if (wireType === 0) {
      const valueResult = decodeVarint(buffer, offset);
      offset = valueResult.offset;
      fields.push({ fieldNumber, wireType, value: valueResult.value });
      continue;
    }

    throw new Error('Unsupported wire type');
  }

  return fields;
}

export function decodeVarint(buffer: Uint8Array, offset: number): { value: number; offset: number } {
  let result = 0;
  let shift = 0;
  let position = offset;

  while (position < buffer.length) {
    const byte = buffer[position++];
    result |= (byte & 0x7f) << shift;
    if ((byte & 0x80) === 0) {
      return { value: result >>> 0, offset: position };
    }
    shift += 7;
  }

  throw new Error('Invalid varint encoding');
}
