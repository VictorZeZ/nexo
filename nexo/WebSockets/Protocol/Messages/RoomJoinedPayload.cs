using nexo.Rooms.Models;

namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by the server to confirm a client has successfully joined (created, joined, or resumed) a room.
/// </summary>
public sealed record RoomJoinedPayload
{
    public required string RoomId { get; init; }
    public required string RoomName { get; init; }
    public required string ParticipantId { get; init; }
    public required RoomVisibility Visibility { get; init; }

    /// <summary>
    /// A fresh bearer token for resuming this exact session if the connection drops. Store it
    /// and discard any previous one — each token is single-use and is replaced every time.
    /// </summary>
    public required string ReconnectToken { get; init; }
}