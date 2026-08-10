using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Application.Ports;

public interface IMasteryRepository
{
    Task<CertificationSummary> CreateCertificationAsync(
        string ownerId, string certificationId, string buildId, int buildVersion,
        string providerProfileId, string missionContentVersion, string rulesVersion,
        string scoreVersion, string certificationVersion, IReadOnlyList<PreparedRun> runs,
        DateTimeOffset now, CancellationToken cancellationToken);

    Task<CertificationSummary?> GetCertificationAsync(
        string ownerId, string certificationId, CancellationToken cancellationToken);

    Task<bool> HasCertificationEligibilityAsync(
        string ownerId, string buildId, int buildVersion, string providerProfileId,
        CancellationToken cancellationToken);

    Task<RunComparison?> GetComparisonAsync(
        string ownerId, RunId leftRunId, RunId rightRunId, CancellationToken cancellationToken);

    Task<PlayerUsageAllowance> GetUsageAllowanceAsync(
        string ownerId, int dailyLimitMicros, DateTimeOffset now, CancellationToken cancellationToken);

    Task<InternalRunDiagnostics?> GetRunDiagnosticsAsync(
        string ownerId, RunId runId, CancellationToken cancellationToken);

    Task<RunSummary?> ApplyEmergencyBurstAsync(
        string ownerId, RunId runId, string text, DateTimeOffset now,
        CancellationToken cancellationToken);
}
