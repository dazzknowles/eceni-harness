using System.Text.Json;
using System.Text.RegularExpressions;

namespace Eceni.Harness;

internal static partial class WorkDefinitionValidator
{
    internal const string SchemaVersion = "eceni-harness.work-definition/1";

    private static readonly HashSet<string> ResultStates = new(StringComparer.Ordinal)
    {
        "open",
        "satisfied",
        "superseded",
    };

    internal static ValidationOutcome Validate(JsonElement document)
    {
        List<Finding> findings = [];
        if (document.ValueKind != JsonValueKind.Object)
        {
            return new ValidationOutcome(
                [new Finding("WD001", "$", "work definition must be a JSON object")],
                null);
        }

        HashSet<string> requiredTop =
        [
            "schema_version",
            "id",
            "purpose",
            "target",
            "authoritative_inputs",
            "governance",
            "authority",
            "execution",
            "acceptance",
            "obligations",
        ];
        RequireKeys(document, requiredTop, "$", findings);
        List<string> unknownKeys = document.EnumerateObject()
            .Select((JsonProperty property) => property.Name)
            .Where((string key) => !requiredTop.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach (string key in unknownKeys)
        {
            findings.Add(new Finding("WD002", $"$.{key}", "unknown top-level field"));
        }

        if (!TryGetString(document, "schema_version", out string? schemaVersion) || schemaVersion != SchemaVersion)
        {
            findings.Add(new Finding("WD003", "$.schema_version", $"must equal '{SchemaVersion}'"));
        }

        NonEmptyString(GetProperty(document, "id"), "$.id", findings);
        NonEmptyString(GetProperty(document, "purpose"), "$.purpose", findings);

        string? targetRepository = ValidateTarget(GetProperty(document, "target"), findings);
        Dictionary<string, SourceIdentity> inputIdentities = ValidateAuthoritativeInputs(
            GetProperty(document, "authoritative_inputs"),
            findings);
        ValidateGovernance(GetProperty(document, "governance"), inputIdentities, findings);

        HashSet<RepositoryCapability> grants = [];
        HashSet<RepositoryCapability> prohibitions = [];
        ValidateAuthority(GetProperty(document, "authority"), grants, prohibitions, findings);
        ValidateExecution(
            GetProperty(document, "execution"),
            targetRepository,
            grants,
            prohibitions,
            findings);
        ValidateAcceptance(GetProperty(document, "acceptance"), findings);
        ValidateObligations(GetProperty(document, "obligations"), findings);

        List<Finding> orderedFindings = findings.Distinct().Order().ToList();
        WorkDefinitionModel? definition = null;
        if (orderedFindings.Count == 0)
        {
            definition = JsonSerializer.Deserialize<WorkDefinitionModel>(document.GetRawText());
        }

        return new ValidationOutcome(orderedFindings, definition);
    }

    private static string? ValidateTarget(JsonElement value, List<Finding> findings)
    {
        const string path = "$.target";
        if (!Object(value, path, findings))
        {
            return null;
        }

        RequireKeys(value, ["repository", "ref", "work_item"], path, findings);
        string? repository = Url(GetProperty(value, "repository"), $"{path}.repository", findings);
        GitSha(GetProperty(value, "ref"), $"{path}.ref", findings);
        JsonElement workItem = GetProperty(value, "work_item");
        if (Object(workItem, $"{path}.work_item", findings))
        {
            RequireKeys(workItem, ["id", "url"], $"{path}.work_item", findings);
            NonEmptyString(GetProperty(workItem, "id"), $"{path}.work_item.id", findings);
            Url(GetProperty(workItem, "url"), $"{path}.work_item.url", findings);
        }

        return repository;
    }

    private static Dictionary<string, SourceIdentity> ValidateAuthoritativeInputs(
        JsonElement value,
        List<Finding> findings)
    {
        const string path = "$.authoritative_inputs";
        Dictionary<string, SourceIdentity> identitiesByRole = new(StringComparer.Ordinal);
        if (!Array(value, path, findings))
        {
            return identitiesByRole;
        }

        JsonElement.ArrayEnumerator inputs = value.EnumerateArray();
        if (!inputs.Any())
        {
            findings.Add(new Finding("WD004", path, "must not be empty"));
        }

        HashSet<string> roles = new(StringComparer.Ordinal);
        HashSet<SourceIdentity> identities = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            index++;
            if (!Object(item, itemPath, findings))
            {
                continue;
            }

            RequireKeys(item, ["role", "url", "revision_kind", "revision"], itemPath, findings);
            string? role = NonEmptyString(GetProperty(item, "role"), $"{itemPath}.role", findings);
            string? url = Url(GetProperty(item, "url"), $"{itemPath}.url", findings);
            string? revisionKind = GetString(GetProperty(item, "revision_kind"));
            string? revision;
            if (revisionKind == "git-commit")
            {
                revision = GitSha(GetProperty(item, "revision"), $"{itemPath}.revision", findings);
            }
            else if (revisionKind == "provider-id")
            {
                revision = NonEmptyString(GetProperty(item, "revision"), $"{itemPath}.revision", findings);
            }
            else
            {
                findings.Add(new Finding(
                    "WD015",
                    $"{itemPath}.revision_kind",
                    "must be 'git-commit' or 'provider-id'"));
                revision = null;
            }

            if (role is not null)
            {
                if (!roles.Add(role))
                {
                    findings.Add(new Finding("WD005", $"{itemPath}.role", "role must be unique"));
                }

                if (url is not null && revision is not null)
                {
                    identitiesByRole[role] = new SourceIdentity(url, revision);
                }
            }

            if (url is not null && revision is not null && !identities.Add(new SourceIdentity(url, revision)))
            {
                findings.Add(new Finding("WD006", itemPath, "duplicate authoritative input"));
            }
        }

        foreach (string role in new[] { "governance-baseline", "project-applicability" })
        {
            if (!roles.Contains(role))
            {
                findings.Add(new Finding("WD007", path, $"missing required role '{role}'"));
            }
        }

        return identitiesByRole;
    }

    private static void ValidateGovernance(
        JsonElement value,
        IReadOnlyDictionary<string, SourceIdentity> inputs,
        List<Finding> findings)
    {
        const string path = "$.governance";
        if (!Object(value, path, findings))
        {
            return;
        }

        RequireKeys(value, ["baseline", "applicability_record"], path, findings);
        JsonElement baseline = GetProperty(value, "baseline");
        if (Object(baseline, $"{path}.baseline", findings))
        {
            RequireKeys(baseline, ["version", "repository", "commit"], $"{path}.baseline", findings);
            NonEmptyString(GetProperty(baseline, "version"), $"{path}.baseline.version", findings);
            string? repository = Url(GetProperty(baseline, "repository"), $"{path}.baseline.repository", findings);
            string? commit = GitSha(GetProperty(baseline, "commit"), $"{path}.baseline.commit", findings);
            SourceIdentity actual = new(repository, commit);
            if (inputs.TryGetValue("governance-baseline", out SourceIdentity? expected) && actual != expected)
            {
                findings.Add(new Finding(
                    "WD016",
                    $"{path}.baseline",
                    "must match the governance-baseline authoritative input"));
            }
        }

        JsonElement applicability = GetProperty(value, "applicability_record");
        if (Object(applicability, $"{path}.applicability_record", findings))
        {
            RequireKeys(applicability, ["url", "revision"], $"{path}.applicability_record", findings);
            string? url = Url(GetProperty(applicability, "url"), $"{path}.applicability_record.url", findings);
            string? revision = GitSha(GetProperty(applicability, "revision"), $"{path}.applicability_record.revision", findings);
            SourceIdentity actual = new(url, revision);
            if (inputs.TryGetValue("project-applicability", out SourceIdentity? expected) && actual != expected)
            {
                findings.Add(new Finding(
                    "WD017",
                    $"{path}.applicability_record",
                    "must match the project-applicability authoritative input"));
            }
        }
    }

    private static void ValidateAuthority(
        JsonElement value,
        HashSet<RepositoryCapability> grants,
        HashSet<RepositoryCapability> prohibitions,
        List<Finding> findings)
    {
        const string path = "$.authority";
        if (!Object(value, path, findings))
        {
            return;
        }

        RequireKeys(value, ["grants", "prohibitions"], path, findings);
        grants.UnionWith(ValidateCapabilities(GetProperty(value, "grants"), $"{path}.grants", findings));
        prohibitions.UnionWith(ValidateCapabilities(GetProperty(value, "prohibitions"), $"{path}.prohibitions", findings));
        foreach (RepositoryCapability capability in grants.Intersect(prohibitions).Order())
        {
            findings.Add(new Finding(
                "AUTH001",
                path,
                $"'{capability.Capability}' for '{capability.Repository}' is both granted and prohibited"));
        }
    }

    private static HashSet<RepositoryCapability> ValidateCapabilities(
        JsonElement value,
        string path,
        List<Finding> findings)
    {
        HashSet<RepositoryCapability> result = [];
        if (!Array(value, path, findings))
        {
            return result;
        }

        if (!value.EnumerateArray().Any())
        {
            findings.Add(new Finding("AUTH008", path, "must not be empty"));
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            index++;
            if (!Object(item, itemPath, findings))
            {
                continue;
            }

            RequireKeys(item, ["id", "repository", "capability", "scope", "source"], itemPath, findings);
            NonEmptyString(GetProperty(item, "id"), $"{itemPath}.id", findings);
            string? repository = Url(GetProperty(item, "repository"), $"{itemPath}.repository", findings);
            string? capability = NonEmptyString(GetProperty(item, "capability"), $"{itemPath}.capability", findings);
            NonEmptyString(GetProperty(item, "scope"), $"{itemPath}.scope", findings);
            NonEmptyString(GetProperty(item, "source"), $"{itemPath}.source", findings);
            if (repository is not null && capability is not null && !result.Add(new RepositoryCapability(repository, capability)))
            {
                findings.Add(new Finding("AUTH009", itemPath, "duplicate repository capability"));
            }
        }

        return result;
    }

    private static void ValidateExecution(
        JsonElement value,
        string? targetRepository,
        IReadOnlySet<RepositoryCapability> grants,
        IReadOnlySet<RepositoryCapability> prohibitions,
        List<Finding> findings)
    {
        const string path = "$.execution";
        if (!Object(value, path, findings))
        {
            return;
        }

        RequireKeys(value, ["change_repositories", "reference_only_repositories"], path, findings);
        HashSet<string> changes = RepositoryList(GetProperty(value, "change_repositories"), $"{path}.change_repositories", findings);
        HashSet<string> references = RepositoryList(GetProperty(value, "reference_only_repositories"), $"{path}.reference_only_repositories", findings);
        foreach (string repository in changes.Intersect(references).Order(StringComparer.Ordinal))
        {
            findings.Add(new Finding("AUTH002", path, $"repository '{repository}' cannot be both writable and reference-only"));
        }

        foreach (string repository in changes.Order(StringComparer.Ordinal))
        {
            RepositoryCapability write = new(repository, "repository.write");
            if (!grants.Contains(write))
            {
                findings.Add(new Finding("AUTH003", $"{path}.change_repositories", $"repository '{repository}' lacks an explicit repository.write grant"));
            }

            if (prohibitions.Contains(write))
            {
                findings.Add(new Finding("AUTH004", $"{path}.change_repositories", $"repository '{repository}' has a repository.write prohibition"));
            }
        }

        foreach (string repository in references.Order(StringComparer.Ordinal))
        {
            if (!grants.Contains(new RepositoryCapability(repository, "repository.read")))
            {
                findings.Add(new Finding("AUTH005", $"{path}.reference_only_repositories", $"repository '{repository}' lacks an explicit repository.read grant"));
            }

            if (grants.Contains(new RepositoryCapability(repository, "repository.write")))
            {
                findings.Add(new Finding("AUTH006", $"{path}.reference_only_repositories", $"reference-only repository '{repository}' has a repository.write grant"));
            }
        }

        if (targetRepository is not null && !changes.Contains(targetRepository) && !references.Contains(targetRepository))
        {
            findings.Add(new Finding("AUTH007", "$.target.repository", "target repository must be declared writable or reference-only"));
        }
    }

    private static HashSet<string> RepositoryList(JsonElement value, string path, List<Finding> findings)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        if (!Array(value, path, findings))
        {
            return result;
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string? repository = Url(item, $"{path}[{index}]", findings);
            if (repository is not null && !result.Add(repository))
            {
                findings.Add(new Finding("WD008", $"{path}[{index}]", "duplicate repository"));
            }

            index++;
        }

        return result;
    }

    private static void ValidateAcceptance(JsonElement value, List<Finding> findings)
    {
        const string path = "$.acceptance";
        if (!Object(value, path, findings))
        {
            return;
        }

        RequireKeys(value, ["implementer_role", "criteria", "independent_verification"], path, findings);
        string? implementer = NonEmptyString(GetProperty(value, "implementer_role"), $"{path}.implementer_role", findings);
        JsonElement criteria = GetProperty(value, "criteria");
        bool independenceRequired = false;
        if (Array(criteria, $"{path}.criteria", findings))
        {
            if (!criteria.EnumerateArray().Any())
            {
                findings.Add(new Finding("ACC001", $"{path}.criteria", "must not be empty"));
            }

            HashSet<string> identifiers = new(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement item in criteria.EnumerateArray())
            {
                string itemPath = $"{path}.criteria[{index}]";
                index++;
                if (!Object(item, itemPath, findings))
                {
                    continue;
                }

                RequireKeys(item, ["id", "criterion", "evidence_required", "assessor", "independent_verification_required"], itemPath, findings);
                string? identifier = NonEmptyString(GetProperty(item, "id"), $"{itemPath}.id", findings);
                if (identifier is not null && !identifiers.Add(identifier))
                {
                    findings.Add(new Finding("ACC002", $"{itemPath}.id", "criterion id must be unique"));
                }

                NonEmptyString(GetProperty(item, "criterion"), $"{itemPath}.criterion", findings);
                ValidateNonEmptyStringArray(GetProperty(item, "evidence_required"), $"{itemPath}.evidence_required", "ACC003", findings);
                NonEmptyString(GetProperty(item, "assessor"), $"{itemPath}.assessor", findings);
                JsonElement criterionRequired = GetProperty(item, "independent_verification_required");
                if (criterionRequired.ValueKind != JsonValueKind.True && criterionRequired.ValueKind != JsonValueKind.False)
                {
                    findings.Add(new Finding("ACC004", $"{itemPath}.independent_verification_required", "must be a boolean"));
                }
                else if (criterionRequired.GetBoolean())
                {
                    independenceRequired = true;
                }
            }
        }

        JsonElement verification = GetProperty(value, "independent_verification");
        if (!Object(verification, $"{path}.independent_verification", findings))
        {
            return;
        }

        string verificationPath = $"{path}.independent_verification";
        RequireKeys(verification, ["required", "verifier_role", "derived_from", "provenance_required"], verificationPath, findings);
        JsonElement requiredValue = GetProperty(verification, "required");
        bool? verificationRequired = Boolean(requiredValue);
        if (verificationRequired is null)
        {
            findings.Add(new Finding("ACC005", $"{verificationPath}.required", "must be a boolean"));
        }

        if (independenceRequired && verificationRequired != true)
        {
            findings.Add(new Finding("ACC006", $"{verificationPath}.required", "must be true when any criterion requires independent verification"));
        }

        string? verifier = NonEmptyString(GetProperty(verification, "verifier_role"), $"{verificationPath}.verifier_role", findings);
        if (verificationRequired == true && implementer is not null && verifier == implementer)
        {
            findings.Add(new Finding("ACC007", $"{verificationPath}.verifier_role", "must differ from implementer_role"));
        }

        JsonElement derivedFrom = GetProperty(verification, "derived_from");
        if (Array(derivedFrom, $"{verificationPath}.derived_from", findings))
        {
            if (verificationRequired == true && !derivedFrom.EnumerateArray().Any())
            {
                findings.Add(new Finding("ACC008", $"{verificationPath}.derived_from", "must identify governing requirements or criteria"));
            }
            else
            {
                ValidateStringArrayEntries(derivedFrom, $"{verificationPath}.derived_from", findings);
            }
        }

        bool? provenance = Boolean(GetProperty(verification, "provenance_required"));
        if (provenance is null)
        {
            findings.Add(new Finding("ACC009", $"{verificationPath}.provenance_required", "must be a boolean"));
        }
        else if (verificationRequired == true && provenance == false)
        {
            findings.Add(new Finding("ACC010", $"{verificationPath}.provenance_required", "must be true for independent verification"));
        }
    }

    private static void ValidateObligations(JsonElement value, List<Finding> findings)
    {
        const string path = "$.obligations";
        if (!Object(value, path, findings))
        {
            return;
        }

        RequireKeys(value, ["decisions", "deferrals", "follow_ups"], path, findings);
        ValidateDecisions(GetProperty(value, "decisions"), findings);
        ValidateTracked(GetProperty(value, "deferrals"), "deferrals", findings);
        ValidateTracked(GetProperty(value, "follow_ups"), "follow_ups", findings);
    }

    private static void ValidateDecisions(JsonElement value, List<Finding> findings)
    {
        const string path = "$.obligations.decisions";
        if (!Array(value, path, findings))
        {
            return;
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            index++;
            if (!Object(item, itemPath, findings))
            {
                continue;
            }

            RequireKeys(item, ["id", "summary", "authority", "basis", "source", "reassessment_triggers"], itemPath, findings);
            foreach (string key in new[] { "id", "summary", "authority", "basis" })
            {
                NonEmptyString(GetProperty(item, key), $"{itemPath}.{key}", findings);
            }

            Url(GetProperty(item, "source"), $"{itemPath}.source", findings);
            ValidateNonEmptyStringArray(GetProperty(item, "reassessment_triggers"), $"{itemPath}.reassessment_triggers", "OBL001", findings);
        }
    }

    private static void ValidateTracked(JsonElement value, string kind, List<Finding> findings)
    {
        string path = $"$.obligations.{kind}";
        if (!Array(value, path, findings))
        {
            return;
        }

        HashSet<string> required = ["id", "summary", "owner", "status", "trigger", "acceptance_conditions", "source"];
        if (kind == "deferrals")
        {
            required.UnionWith(["affected_source", "rationale", "may_proceed", "may_not_proceed"]);
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            index++;
            if (!Object(item, itemPath, findings))
            {
                continue;
            }

            RequireKeys(item, required, itemPath, findings);
            foreach (string key in required.Where((string key) => key != "acceptance_conditions"))
            {
                if (key == "source")
                {
                    Url(GetProperty(item, key), $"{itemPath}.{key}", findings);
                }
                else
                {
                    NonEmptyString(GetProperty(item, key), $"{itemPath}.{key}", findings);
                }
            }

            string? status = GetString(GetProperty(item, "status"));
            if (status is null || !ResultStates.Contains(status))
            {
                findings.Add(new Finding("OBL002", $"{itemPath}.status", "must be one of ['open', 'satisfied', 'superseded']"));
            }

            ValidateNonEmptyStringArray(GetProperty(item, "acceptance_conditions"), $"{itemPath}.acceptance_conditions", "OBL003", findings);
        }
    }

    private static void ValidateNonEmptyStringArray(JsonElement value, string path, string emptyCode, List<Finding> findings)
    {
        if (!Array(value, path, findings))
        {
            return;
        }

        if (!value.EnumerateArray().Any())
        {
            findings.Add(new Finding(emptyCode, path, "must not be empty"));
            return;
        }

        ValidateStringArrayEntries(value, path, findings);
    }

    private static void ValidateStringArrayEntries(JsonElement value, string path, List<Finding> findings)
    {
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            NonEmptyString(item, $"{path}[{index}]", findings);
            index++;
        }
    }

    private static void RequireKeys(JsonElement value, HashSet<string> keys, string path, List<Finding> findings)
    {
        foreach (string key in keys.Order(StringComparer.Ordinal))
        {
            if (!value.TryGetProperty(key, out JsonElement _))
            {
                findings.Add(new Finding("WD009", $"{path}.{key}", "required field is missing"));
            }
        }
    }

    private static bool Object(JsonElement value, string path, List<Finding> findings)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        findings.Add(new Finding("WD010", path, "must be an object"));
        return false;
    }

    private static bool Array(JsonElement value, string path, List<Finding> findings)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        findings.Add(new Finding("WD011", path, "must be an array"));
        return false;
    }

    private static string? NonEmptyString(JsonElement value, string path, List<Finding> findings)
    {
        string? result = GetString(value);
        if (string.IsNullOrWhiteSpace(result))
        {
            findings.Add(new Finding("WD012", path, "must be a non-empty string"));
            return null;
        }

        return result;
    }

    private static string? Url(JsonElement value, string path, List<Finding> findings)
    {
        string? result = NonEmptyString(value, path, findings);
        if (result is not null && !result.StartsWith("https://", StringComparison.Ordinal))
        {
            findings.Add(new Finding("WD013", path, "must be an https URL"));
            return null;
        }

        return result;
    }

    private static string? GitSha(JsonElement value, string path, List<Finding> findings)
    {
        string? result = NonEmptyString(value, path, findings);
        if (result is not null && !GitShaPattern().IsMatch(result))
        {
            findings.Add(new Finding("WD014", path, "must be a full lowercase 40-character Git commit"));
            return null;
        }

        return result;
    }

    private static bool? Boolean(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return null;
    }

    private static JsonElement GetProperty(JsonElement value, string name)
    {
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out JsonElement property)
            ? property
            : default;
    }

    private static string? GetString(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool TryGetString(JsonElement value, string name, out string? result)
    {
        result = GetString(GetProperty(value, name));
        return result is not null;
    }

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex GitShaPattern();

    private sealed record SourceIdentity(string? Url, string? Revision);

    private sealed record RepositoryCapability(string Repository, string Capability) : IComparable<RepositoryCapability>
    {
        public int CompareTo(RepositoryCapability? other)
        {
            if (other is null)
            {
                return 1;
            }

            int repositoryComparison = string.Compare(Repository, other.Repository, StringComparison.Ordinal);
            return repositoryComparison != 0
                ? repositoryComparison
                : string.Compare(Capability, other.Capability, StringComparison.Ordinal);
        }
    }
}
