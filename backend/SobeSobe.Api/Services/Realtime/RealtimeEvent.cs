namespace SobeSobe.Api.Services.Realtime;

/// <summary>
/// Represents a lightweight realtime event sent to SignalR clients.
/// </summary>
public sealed record RealtimeEvent(string Type, object? Payload);
