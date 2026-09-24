from __future__ import annotations

from dataclasses import dataclass
import re
from typing import Any


SCHEMA_VERSION = "eceni-harness.work-definition/1"
GIT_SHA = re.compile(r"^[0-9a-f]{40}$")
RESULT_STATES = {"open", "satisfied", "superseded"}


@dataclass(frozen=True, order=True)
class Finding:
    code: str
    path: str
    message: str


def validate_definition(document: Any) -> list[Finding]:
    findings: list[Finding] = []

    if not isinstance(document, dict):
        return [Finding("WD001", "$", "work definition must be a JSON object")]

    required_top = {
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
    }
    _require_keys(document, required_top, "$", findings)
    unknown = set(document) - required_top
    for key in sorted(unknown):
        findings.append(Finding("WD002", f"$.{key}", "unknown top-level field"))

    if document.get("schema_version") != SCHEMA_VERSION:
        findings.append(
            Finding("WD003", "$.schema_version", f"must equal {SCHEMA_VERSION!r}")
        )
    _non_empty_string(document.get("id"), "$.id", findings)
    _non_empty_string(document.get("purpose"), "$.purpose", findings)

    target = _object(document.get("target"), "$.target", findings)
    if target is not None:
        _require_keys(target, {"repository", "ref", "work_item"}, "$.target", findings)
        target_repository = _url(target.get("repository"), "$.target.repository", findings)
        _git_sha(target.get("ref"), "$.target.ref", findings)
        work_item = _object(target.get("work_item"), "$.target.work_item", findings)
        if work_item is not None:
            _require_keys(work_item, {"id", "url"}, "$.target.work_item", findings)
            _non_empty_string(work_item.get("id"), "$.target.work_item.id", findings)
            _url(work_item.get("url"), "$.target.work_item.url", findings)
    else:
        target_repository = None

    inputs = _list(document.get("authoritative_inputs"), "$.authoritative_inputs", findings)
    input_roles: set[str] = set()
    input_identities: dict[str, tuple[str, str]] = {}
    if inputs is not None:
        if not inputs:
            findings.append(Finding("WD004", "$.authoritative_inputs", "must not be empty"))
        identities: set[tuple[str, str]] = set()
        for index, value in enumerate(inputs):
            path = f"$.authoritative_inputs[{index}]"
            item = _object(value, path, findings)
            if item is None:
                continue
            _require_keys(item, {"role", "url", "revision_kind", "revision"}, path, findings)
            role = _non_empty_string(item.get("role"), f"{path}.role", findings)
            url = _url(item.get("url"), f"{path}.url", findings)
            revision_kind = item.get("revision_kind")
            if revision_kind == "git-commit":
                revision = _git_sha(item.get("revision"), f"{path}.revision", findings)
            elif revision_kind == "provider-id":
                revision = _non_empty_string(item.get("revision"), f"{path}.revision", findings)
            else:
                findings.append(
                    Finding(
                        "WD015",
                        f"{path}.revision_kind",
                        "must be 'git-commit' or 'provider-id'",
                    )
                )
                revision = None
            if role:
                if role in input_roles:
                    findings.append(Finding("WD005", f"{path}.role", "role must be unique"))
                input_roles.add(role)
                if url and revision:
                    input_identities[role] = (url, revision)
            if url and revision:
                identity = (url, revision)
                if identity in identities:
                    findings.append(Finding("WD006", path, "duplicate authoritative input"))
                identities.add(identity)
        for role in ("governance-baseline", "project-applicability"):
            if role not in input_roles:
                findings.append(
                    Finding("WD007", "$.authoritative_inputs", f"missing required role {role!r}")
                )

    governance = _object(document.get("governance"), "$.governance", findings)
    if governance is not None:
        _require_keys(governance, {"baseline", "applicability_record"}, "$.governance", findings)
        baseline = _object(governance.get("baseline"), "$.governance.baseline", findings)
        if baseline is not None:
            _require_keys(
                baseline,
                {"version", "repository", "commit"},
                "$.governance.baseline",
                findings,
            )
            _non_empty_string(baseline.get("version"), "$.governance.baseline.version", findings)
            _url(baseline.get("repository"), "$.governance.baseline.repository", findings)
            _git_sha(baseline.get("commit"), "$.governance.baseline.commit", findings)
            expected = input_identities.get("governance-baseline")
            actual = (baseline.get("repository"), baseline.get("commit"))
            if expected is not None and actual != expected:
                findings.append(
                    Finding(
                        "WD016",
                        "$.governance.baseline",
                        "must match the governance-baseline authoritative input",
                    )
                )
        applicability = _object(
            governance.get("applicability_record"),
            "$.governance.applicability_record",
            findings,
        )
        if applicability is not None:
            _require_keys(
                applicability,
                {"url", "revision"},
                "$.governance.applicability_record",
                findings,
            )
            _url(applicability.get("url"), "$.governance.applicability_record.url", findings)
            _git_sha(
                applicability.get("revision"),
                "$.governance.applicability_record.revision",
                findings,
            )
            expected = input_identities.get("project-applicability")
            actual = (applicability.get("url"), applicability.get("revision"))
            if expected is not None and actual != expected:
                findings.append(
                    Finding(
                        "WD017",
                        "$.governance.applicability_record",
                        "must match the project-applicability authoritative input",
                    )
                )

    authority = _object(document.get("authority"), "$.authority", findings)
    grants: set[tuple[str, str]] = set()
    prohibitions: set[tuple[str, str]] = set()
    if authority is not None:
        _require_keys(authority, {"grants", "prohibitions"}, "$.authority", findings)
        grants = _validate_capabilities(authority.get("grants"), "$.authority.grants", findings)
        prohibitions = _validate_capabilities(
            authority.get("prohibitions"), "$.authority.prohibitions", findings
        )
        for repository, capability in sorted(grants & prohibitions):
            findings.append(
                Finding(
                    "AUTH001",
                    "$.authority",
                    f"{capability!r} for {repository!r} is both granted and prohibited",
                )
            )

    execution = _object(document.get("execution"), "$.execution", findings)
    if execution is not None:
        _require_keys(
            execution,
            {"change_repositories", "reference_only_repositories"},
            "$.execution",
            findings,
        )
        change_repositories = _repository_list(
            execution.get("change_repositories"), "$.execution.change_repositories", findings
        )
        reference_repositories = _repository_list(
            execution.get("reference_only_repositories"),
            "$.execution.reference_only_repositories",
            findings,
        )
        for repository in sorted(change_repositories & reference_repositories):
            findings.append(
                Finding(
                    "AUTH002",
                    "$.execution",
                    f"repository {repository!r} cannot be both writable and reference-only",
                )
            )
        for repository in sorted(change_repositories):
            if (repository, "repository.write") not in grants:
                findings.append(
                    Finding(
                        "AUTH003",
                        "$.execution.change_repositories",
                        f"repository {repository!r} lacks an explicit repository.write grant",
                    )
                )
            if (repository, "repository.write") in prohibitions:
                findings.append(
                    Finding(
                        "AUTH004",
                        "$.execution.change_repositories",
                        f"repository {repository!r} has a repository.write prohibition",
                    )
                )
        for repository in sorted(reference_repositories):
            if (repository, "repository.read") not in grants:
                findings.append(
                    Finding(
                        "AUTH005",
                        "$.execution.reference_only_repositories",
                        f"repository {repository!r} lacks an explicit repository.read grant",
                    )
                )
            if (repository, "repository.write") in grants:
                findings.append(
                    Finding(
                        "AUTH006",
                        "$.execution.reference_only_repositories",
                        f"reference-only repository {repository!r} has a repository.write grant",
                    )
                )
        if target_repository and target_repository not in change_repositories | reference_repositories:
            findings.append(
                Finding(
                    "AUTH007",
                    "$.target.repository",
                    "target repository must be declared writable or reference-only",
                )
            )

    acceptance = _object(document.get("acceptance"), "$.acceptance", findings)
    if acceptance is not None:
        _validate_acceptance(acceptance, findings)

    obligations = _object(document.get("obligations"), "$.obligations", findings)
    if obligations is not None:
        _require_keys(
            obligations,
            {"decisions", "deferrals", "follow_ups"},
            "$.obligations",
            findings,
        )
        _validate_decisions(obligations.get("decisions"), findings)
        _validate_tracked(obligations.get("deferrals"), "deferrals", findings)
        _validate_tracked(obligations.get("follow_ups"), "follow_ups", findings)

    return sorted(set(findings))


def _validate_capabilities(value: Any, path: str, findings: list[Finding]) -> set[tuple[str, str]]:
    items = _list(value, path, findings)
    result: set[tuple[str, str]] = set()
    if items is None:
        return result
    if not items:
        findings.append(Finding("AUTH008", path, "must not be empty"))
    for index, value in enumerate(items):
        item_path = f"{path}[{index}]"
        item = _object(value, item_path, findings)
        if item is None:
            continue
        _require_keys(item, {"id", "repository", "capability", "scope", "source"}, item_path, findings)
        _non_empty_string(item.get("id"), f"{item_path}.id", findings)
        repository = _url(item.get("repository"), f"{item_path}.repository", findings)
        capability = _non_empty_string(item.get("capability"), f"{item_path}.capability", findings)
        _non_empty_string(item.get("scope"), f"{item_path}.scope", findings)
        _non_empty_string(item.get("source"), f"{item_path}.source", findings)
        if repository and capability:
            pair = (repository, capability)
            if pair in result:
                findings.append(Finding("AUTH009", item_path, "duplicate repository capability"))
            result.add(pair)
    return result


def _validate_acceptance(value: dict[str, Any], findings: list[Finding]) -> None:
    path = "$.acceptance"
    _require_keys(value, {"implementer_role", "criteria", "independent_verification"}, path, findings)
    implementer = _non_empty_string(value.get("implementer_role"), f"{path}.implementer_role", findings)
    criteria = _list(value.get("criteria"), f"{path}.criteria", findings)
    independence_required = False
    if criteria is not None:
        if not criteria:
            findings.append(Finding("ACC001", f"{path}.criteria", "must not be empty"))
        identifiers: set[str] = set()
        for index, raw in enumerate(criteria):
            item_path = f"{path}.criteria[{index}]"
            item = _object(raw, item_path, findings)
            if item is None:
                continue
            _require_keys(
                item,
                {"id", "criterion", "evidence_required", "assessor", "independent_verification_required"},
                item_path,
                findings,
            )
            identifier = _non_empty_string(item.get("id"), f"{item_path}.id", findings)
            if identifier:
                if identifier in identifiers:
                    findings.append(Finding("ACC002", f"{item_path}.id", "criterion id must be unique"))
                identifiers.add(identifier)
            _non_empty_string(item.get("criterion"), f"{item_path}.criterion", findings)
            evidence = _list(item.get("evidence_required"), f"{item_path}.evidence_required", findings)
            if evidence is not None and not evidence:
                findings.append(Finding("ACC003", f"{item_path}.evidence_required", "must not be empty"))
            elif evidence is not None:
                for evidence_index, entry in enumerate(evidence):
                    _non_empty_string(entry, f"{item_path}.evidence_required[{evidence_index}]", findings)
            _non_empty_string(item.get("assessor"), f"{item_path}.assessor", findings)
            required = item.get("independent_verification_required")
            if not isinstance(required, bool):
                findings.append(Finding("ACC004", f"{item_path}.independent_verification_required", "must be a boolean"))
            independence_required = independence_required or required is True

    verification = _object(value.get("independent_verification"), f"{path}.independent_verification", findings)
    if verification is not None:
        _require_keys(
            verification,
            {"required", "verifier_role", "derived_from", "provenance_required"},
            f"{path}.independent_verification",
            findings,
        )
        required = verification.get("required")
        if not isinstance(required, bool):
            findings.append(Finding("ACC005", f"{path}.independent_verification.required", "must be a boolean"))
        if independence_required and required is not True:
            findings.append(Finding("ACC006", f"{path}.independent_verification.required", "must be true when any criterion requires independent verification"))
        verifier = _non_empty_string(
            verification.get("verifier_role"),
            f"{path}.independent_verification.verifier_role",
            findings,
        )
        if required is True and implementer and verifier == implementer:
            findings.append(Finding("ACC007", f"{path}.independent_verification.verifier_role", "must differ from implementer_role"))
        derived_from = _list(
            verification.get("derived_from"),
            f"{path}.independent_verification.derived_from",
            findings,
        )
        if required is True and derived_from is not None and not derived_from:
            findings.append(Finding("ACC008", f"{path}.independent_verification.derived_from", "must identify governing requirements or criteria"))
        elif derived_from is not None:
            for index, entry in enumerate(derived_from):
                _non_empty_string(entry, f"{path}.independent_verification.derived_from[{index}]", findings)
        provenance = verification.get("provenance_required")
        if not isinstance(provenance, bool):
            findings.append(Finding("ACC009", f"{path}.independent_verification.provenance_required", "must be a boolean"))
        elif required is True and not provenance:
            findings.append(Finding("ACC010", f"{path}.independent_verification.provenance_required", "must be true for independent verification"))


def _validate_decisions(value: Any, findings: list[Finding]) -> None:
    path = "$.obligations.decisions"
    items = _list(value, path, findings)
    if items is None:
        return
    for index, raw in enumerate(items):
        item_path = f"{path}[{index}]"
        item = _object(raw, item_path, findings)
        if item is None:
            continue
        _require_keys(item, {"id", "summary", "authority", "basis", "source", "reassessment_triggers"}, item_path, findings)
        for key in ("id", "summary", "authority", "basis"):
            _non_empty_string(item.get(key), f"{item_path}.{key}", findings)
        _url(item.get("source"), f"{item_path}.source", findings)
        triggers = _list(item.get("reassessment_triggers"), f"{item_path}.reassessment_triggers", findings)
        if triggers is not None and not triggers:
            findings.append(Finding("OBL001", f"{item_path}.reassessment_triggers", "must not be empty"))
        elif triggers is not None:
            for trigger_index, entry in enumerate(triggers):
                _non_empty_string(entry, f"{item_path}.reassessment_triggers[{trigger_index}]", findings)


def _validate_tracked(value: Any, kind: str, findings: list[Finding]) -> None:
    path = f"$.obligations.{kind}"
    items = _list(value, path, findings)
    if items is None:
        return
    for index, raw in enumerate(items):
        item_path = f"{path}[{index}]"
        item = _object(raw, item_path, findings)
        if item is None:
            continue
        required = {"id", "summary", "owner", "status", "trigger", "acceptance_conditions", "source"}
        if kind == "deferrals":
            required |= {"affected_source", "rationale", "may_proceed", "may_not_proceed"}
        _require_keys(item, required, item_path, findings)
        for key in required - {"acceptance_conditions"}:
            if key == "source":
                _url(item.get(key), f"{item_path}.{key}", findings)
            else:
                _non_empty_string(item.get(key), f"{item_path}.{key}", findings)
        if item.get("status") not in RESULT_STATES:
            findings.append(Finding("OBL002", f"{item_path}.status", f"must be one of {sorted(RESULT_STATES)}"))
        conditions = _list(item.get("acceptance_conditions"), f"{item_path}.acceptance_conditions", findings)
        if conditions is not None and not conditions:
            findings.append(Finding("OBL003", f"{item_path}.acceptance_conditions", "must not be empty"))
        elif conditions is not None:
            for condition_index, entry in enumerate(conditions):
                _non_empty_string(entry, f"{item_path}.acceptance_conditions[{condition_index}]", findings)


def _repository_list(value: Any, path: str, findings: list[Finding]) -> set[str]:
    items = _list(value, path, findings)
    result: set[str] = set()
    if items is None:
        return result
    for index, item in enumerate(items):
        repository = _url(item, f"{path}[{index}]", findings)
        if repository:
            if repository in result:
                findings.append(Finding("WD008", f"{path}[{index}]", "duplicate repository"))
            result.add(repository)
    return result


def _require_keys(value: dict[str, Any], keys: set[str], path: str, findings: list[Finding]) -> None:
    for key in sorted(keys - set(value)):
        findings.append(Finding("WD009", f"{path}.{key}", "required field is missing"))


def _object(value: Any, path: str, findings: list[Finding]) -> dict[str, Any] | None:
    if not isinstance(value, dict):
        findings.append(Finding("WD010", path, "must be an object"))
        return None
    return value


def _list(value: Any, path: str, findings: list[Finding]) -> list[Any] | None:
    if not isinstance(value, list):
        findings.append(Finding("WD011", path, "must be an array"))
        return None
    return value


def _non_empty_string(value: Any, path: str, findings: list[Finding]) -> str | None:
    if not isinstance(value, str) or not value.strip():
        findings.append(Finding("WD012", path, "must be a non-empty string"))
        return None
    return value


def _url(value: Any, path: str, findings: list[Finding]) -> str | None:
    result = _non_empty_string(value, path, findings)
    if result is not None and not result.startswith("https://"):
        findings.append(Finding("WD013", path, "must be an https URL"))
        return None
    return result


def _git_sha(value: Any, path: str, findings: list[Finding]) -> str | None:
    result = _non_empty_string(value, path, findings)
    if result is not None and GIT_SHA.fullmatch(result) is None:
        findings.append(Finding("WD014", path, "must be a full lowercase 40-character Git commit"))
        return None
    return result
