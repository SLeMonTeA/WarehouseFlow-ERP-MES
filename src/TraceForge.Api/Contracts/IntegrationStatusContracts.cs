namespace TraceForge.Api.Contracts;

public sealed record IntegrationChannelStatus(
    string Channel,
    string Direction,
    string Partner,
    int Pending,
    int Succeeded,
    int Failed,
    DateTimeOffset? LastSucceededAt,
    DateTimeOffset? LastFailedAt,
    string? LastError);

public sealed record IntegrationStatusResponse(
    string System,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<IntegrationChannelStatus> Channels);
