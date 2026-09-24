using System.Text.Json.Serialization;

namespace Eceni.Harness;

internal sealed record WorkDefinitionModel(
    [property: JsonPropertyName("schema_version")] string? SchemaVersion,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("purpose")] string? Purpose,
    [property: JsonPropertyName("target")] TargetModel? Target,
    [property: JsonPropertyName("authoritative_inputs")] IReadOnlyList<AuthoritativeInputModel>? AuthoritativeInputs,
    [property: JsonPropertyName("governance")] GovernanceModel? Governance,
    [property: JsonPropertyName("authority")] AuthorityModel? Authority,
    [property: JsonPropertyName("execution")] ExecutionModel? Execution,
    [property: JsonPropertyName("acceptance")] AcceptanceModel? Acceptance,
    [property: JsonPropertyName("obligations")] ObligationsModel? Obligations);

internal sealed record TargetModel(
    [property: JsonPropertyName("repository")] string? Repository,
    [property: JsonPropertyName("ref")] string? Ref,
    [property: JsonPropertyName("work_item")] WorkItemModel? WorkItem);

internal sealed record WorkItemModel(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("url")] string? Url);

internal sealed record AuthoritativeInputModel(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("revision_kind")] string? RevisionKind,
    [property: JsonPropertyName("revision")] string? Revision);

internal sealed record GovernanceModel(
    [property: JsonPropertyName("baseline")] GovernanceBaselineModel? Baseline,
    [property: JsonPropertyName("applicability_record")] ApplicabilityRecordModel? ApplicabilityRecord);

internal sealed record GovernanceBaselineModel(
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("repository")] string? Repository,
    [property: JsonPropertyName("commit")] string? Commit);

internal sealed record ApplicabilityRecordModel(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("revision")] string? Revision);

internal sealed record AuthorityModel(
    [property: JsonPropertyName("grants")] IReadOnlyList<CapabilityModel>? Grants,
    [property: JsonPropertyName("prohibitions")] IReadOnlyList<CapabilityModel>? Prohibitions);

internal sealed record CapabilityModel(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("repository")] string? Repository,
    [property: JsonPropertyName("capability")] string? Capability,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("source")] string? Source);

internal sealed record ExecutionModel(
    [property: JsonPropertyName("change_repositories")] IReadOnlyList<string>? ChangeRepositories,
    [property: JsonPropertyName("reference_only_repositories")] IReadOnlyList<string>? ReferenceOnlyRepositories);

internal sealed record AcceptanceModel(
    [property: JsonPropertyName("implementer_role")] string? ImplementerRole,
    [property: JsonPropertyName("criteria")] IReadOnlyList<AcceptanceCriterionModel>? Criteria,
    [property: JsonPropertyName("independent_verification")] IndependentVerificationModel? IndependentVerification);

internal sealed record AcceptanceCriterionModel(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("criterion")] string? Criterion,
    [property: JsonPropertyName("evidence_required")] IReadOnlyList<string>? EvidenceRequired,
    [property: JsonPropertyName("assessor")] string? Assessor,
    [property: JsonPropertyName("independent_verification_required")] bool? IndependentVerificationRequired);

internal sealed record IndependentVerificationModel(
    [property: JsonPropertyName("required")] bool? Required,
    [property: JsonPropertyName("verifier_role")] string? VerifierRole,
    [property: JsonPropertyName("derived_from")] IReadOnlyList<string>? DerivedFrom,
    [property: JsonPropertyName("provenance_required")] bool? ProvenanceRequired);

internal sealed record ObligationsModel(
    [property: JsonPropertyName("decisions")] IReadOnlyList<DecisionModel>? Decisions,
    [property: JsonPropertyName("deferrals")] IReadOnlyList<TrackedObligationModel>? Deferrals,
    [property: JsonPropertyName("follow_ups")] IReadOnlyList<TrackedObligationModel>? FollowUps);

internal sealed record DecisionModel(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("authority")] string? Authority,
    [property: JsonPropertyName("basis")] string? Basis,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("reassessment_triggers")] IReadOnlyList<string>? ReassessmentTriggers);

internal sealed record TrackedObligationModel(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("trigger")] string? Trigger,
    [property: JsonPropertyName("acceptance_conditions")] IReadOnlyList<string>? AcceptanceConditions,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("affected_source")] string? AffectedSource,
    [property: JsonPropertyName("rationale")] string? Rationale,
    [property: JsonPropertyName("may_proceed")] string? MayProceed,
    [property: JsonPropertyName("may_not_proceed")] string? MayNotProceed);
