namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "ProtocolLimits" configuration section.
/// Defines structural size limits enforced while parsing protocol messages, independent of
/// any room or business rule (those are enforced separately once rooms exist).
/// </summary>
public sealed class ProtocolLimitsSettings
{
    public const string SectionName = "ProtocolLimits";

    public int MaxRoomIdLength { get; init; } = 64;
    public int MaxRoomNameLength { get; init; } = 64;
    public int MaxDisplayNameLength { get; init; } = 32;
    public int MaxPasswordLength { get; init; } = 128;
    public int MaxCiphertextLength { get; init; } = 8_000;
    public int MaxNonceLength { get; init; } = 64;
    public int MaxParticipantIdLength { get; init; } = 64;
    public int MaxReconnectTokenLength { get; init; } = 128;
    public int MaxSdpLength { get; init; } = 10_000;
    public int MaxIceCandidateLength { get; init; } = 2_000;
    public int MaxSdpMidLength { get; init; } = 32;
}