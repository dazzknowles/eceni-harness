# Bounded work-definition preflight

## Purpose

The first Harness slice answers one question before execution: **is this work
definition complete and internally consistent enough to act on without
inventing authority or evidence?**

The command validates a plain JSON file and writes a human-readable Markdown
run record. It does not clone repositories, execute agents, edit products,
interpret Governance, accept a product result, or publish anything.

## Required content

A definition uses `eceni-harness.work-definition/1` and contains:

- a stable identity and purpose;
- a target repository, immutable 40-character Git commit, and authoritative
  work-item identity/URL;
- versioned authoritative inputs, including one Governance baseline and one
  project applicability record (Git content uses a full commit; provider
  records such as issue comments use their durable provider identity);
- the adopted Governance version/commit and project applicability record;
- repository/capability grants, explicit prohibitions, and the authoritative
  source of each;
- the repositories that may be changed and those that are reference-only;
- acceptance criteria, evidence required, assessment ownership, and an
  independent-verification plan where any criterion requires one;
- explicitly tracked decisions, deferrals, and follow-up obligations.

Repository capabilities are intentionally exact strings. The initial
vocabulary is `repository.read`, `repository.write`, `issue.read`,
`pull_request.create`, `pull_request.merge`, `release.deploy`, and
`provider.control`. A definition may name another capability, but it gains no
special meaning or implicit authority from doing so.

## Fail-closed rules

Validation fails when required content is absent, a Git source is not pinned,
a change repository lacks an explicit `repository.write` grant, a reference-
only repository is writable, or the same repository/capability pair is both
granted and prohibited. An empty change-repository list is valid and means no
repository may be changed.

Every acceptance criterion must name its evidence and responsible assessor.
If any criterion requires independent verification, the common verification
plan must be enabled, derive its work from named requirements/criteria, retain
provenance, and use a verifier role distinct from the implementer role.

Tracked decisions require authority, basis, source, and reassessment triggers.
Open deferrals and follow-ups require an owner, objective trigger, and
acceptance conditions. These checks establish record completeness only; they
do not claim that rationale or evidence is substantively adequate.

## Run records

The record contains the run identity/time, validator version, input path and
SHA-256 digest, overall EVI-001 result (`Pass` or `Fail`), responsible
assessment, and every individual check/finding. Failed definitions therefore
leave evidence rather than disappearing into terminal output.

The output path is caller-selected and never overwritten. A run ID and
assessment time can be supplied explicitly for reproducible tests or an
externally assigned execution identity.

## Conscious non-reuse of Rio Harness

Rio Harness was inspected for lessons, especially explicit authority,
fail-closed evidence states, durable identities, and human-readable querying.
No Rio code, schema, authority model, deployment, or dependency is reused.
Its SQLite event store, content-addressed artefact store, lifecycle engine,
executor adapters, and service-like orchestration are disproportionate to
this first Eceni need. The only consciously retained ideas are general design
lessons independently justified here: immutable source refs, explicit grants
and prohibitions, non-overwritten evidence, and no conversion of missing
evidence into success.
