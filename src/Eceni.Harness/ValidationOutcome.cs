namespace Eceni.Harness;

internal sealed record ValidationOutcome(
    IReadOnlyList<Finding> Findings,
    WorkDefinitionModel? Definition);
