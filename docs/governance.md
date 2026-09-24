# Harness governance adoption

Eceni Harness was created before Eceni had a versioned Governance baseline.
It therefore has no retrospective birth baseline. Its first formal adoption
is Eceni Governance **1.1.0**, commit
[`90e8b0a`](https://github.com/dazzknowles/eceni-governance/tree/90e8b0a7e051df4e40e058223c7d3aad6b88c166),
on 24 September 2026. That repository remains authoritative; this file records
Harness applicability and does not reproduce or amend Governance.

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
- QUA-001 to the implementation and tests.

EVI-002 is applicable when a work definition records deferred evidence. The
Solar #5 example records live verification as a deferred obligation whose
trigger is implementation against a real FoxESS connection.

ECO-001 is not triggered by this slice: it creates no material recurring
infrastructure, service, inference, transfer, or operational cost decision.

No versioned Eceni Python implementation guide exists in baseline 1.1.0. Under
QUA-001, the implementation therefore follows the Python standard library's
conventional module, CLI, JSON, hashing, and `unittest` facilities. The C# and
MariaDB guides are not applicable because this repository contains neither
C# nor database code.

None of the baseline 1.1.0 Checks applies to this slice: it has no governed
parent-issue marker or MariaDB definitions/data bootstrap. This does not make
their underlying Laws optional.

Future Governance transitions must append their baseline, adoption decision,
applicability changes, and migration obligations here rather than replacing
this history.
