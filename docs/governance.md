# Harness governance adoption

Eceni Harness was created before Eceni had a versioned Governance baseline.
It therefore has no retrospective birth baseline. Its first formal adoption
is Eceni Governance **1.1.0**, commit
[`90e8b0a`](https://github.com/dazzknowles/eceni-governance/tree/90e8b0a7e051df4e40e058223c7d3aad6b88c166),
on 24 September 2026. That repository remains authoritative; this file records
Harness applicability and does not reproduce or amend Governance.

On 24 September 2026 Harness adopted Eceni Governance **1.2.0**, commit
[`fba2357`](https://github.com/dazzknowles/eceni-governance/tree/fba2357d6876efa051eddac42f7592e4e5dc66a5).
The adoption decision is the design-authority instruction recorded in Codex
task `01a0ced1-dc8c-7fe2-8c2c-502f543f911e`: replace the first executable
implementation with C# on .NET 10 and treat that stack as the authorised
Harness project default. The migration retires the former implementation and
its runtime requirements while preserving the work-definition contract,
validation behaviour, tests, example and historical evidence records.

This is a durable technology-foundation decision under QUA-002. Its scope is
maintained Harness application and test code. The rationale is one explicit,
reviewable project baseline with a supported SDK, typed models and standard
.NET facilities; it adds the .NET 10 build/runtime requirement and the C#
implementation guide, but no application framework or third-party package.
Reassessment is required if the design authority changes the project default,
.NET 10 leaves the supported lifecycle, or deployment or interoperability
constraints make the baseline materially unsuitable.

## Applicability for this vertical slice

All six Commandments apply. The following Laws apply directly:

- EVI-001 to every validation result and run record;
- GOV-001 to decisions, deferrals, and follow-up obligations;
- GOV-002 to decisions with recorded reassessment triggers;
- GOV-003 to this lineage record and future transitions;
- VER-001 where a work definition requires independent verification;
- SEC-001 to executable capabilities and repository authority;
- DATA-001 to source material and evidence retained in a run record;
- DATA-002 to the versioned example definition and reproducible validation;
- QUA-001 and the C# implementation guide to the implementation and tests;
- QUA-002 to the recorded C#/.NET 10 technology baseline.

EVI-002 is applicable when a work definition records deferred evidence. The
Solar #5 example records live verification as a deferred obligation whose
trigger is implementation against a real FoxESS connection.

ECO-001 is not triggered by this slice: it creates no material recurring
infrastructure, service, inference, transfer, or operational cost decision.

The MariaDB guide is not applicable because this slice contains no database.
None of the baseline 1.2.0 Checks applies to this slice: it has no governed
parent-issue marker or MariaDB definitions/data bootstrap. This does not make
their underlying Laws optional.

Future Governance transitions must append their baseline, adoption decision,
applicability changes, and migration obligations here rather than replacing
this history.
