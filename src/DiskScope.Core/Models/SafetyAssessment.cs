namespace DiskScope.Core.Models;

public sealed record SafetyAssessment(
    SafetyCategory Category,
    string Title,
    string Explanation,
    string Recommendation,
    bool DeletionBlocked,
    bool RequiresElevatedWarning);
