namespace nexo.WebSockets.Protocol;

/// <summary>
/// Defines the protocol versions this server understands.
/// </summary>
public static class ProtocolVersion
{
    /// <summary>The current protocol version emitted by the server.</summary>
    public const int Current = 1;

    /// <summary>The oldest client protocol version the server still accepts.</summary>
    public const int MinimumSupported = 1;
}