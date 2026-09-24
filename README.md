# Eceni Harness

Eceni Harness begins with one deliberately small control: a deterministic,
fail-closed preflight for a bounded work definition. It checks that a proposed
workload names its authoritative inputs, authority boundary, evidence duties,
and outstanding obligations before work is treated as executable. Every run
produces a durable Markdown record, including failed runs.

This repository consumes Eceni Governance; it does not define or copy it. The
current Harness lineage and applicability decision are recorded in
[`docs/governance.md`](docs/governance.md).

## Try the Solar Optimiser issue #5 proving workload

The maintained baseline is the .NET 10 SDK pinned by `global.json`; the
implementation has no third-party runtime or package dependencies. From a
checkout of this repository:

```powershell
dotnet build Eceni.Harness.slnx
dotnet run --project tests/Eceni.Harness.Tests
dotnet run --project src/Eceni.Harness -- validate `
  data/work-definitions/solar-optimiser-5.json `
  --record runs/solar-optimiser-5-dotnet-1.md
```

The example is a **read-only preflight**, not authority to implement Solar
issue #5. Its product-owner decision approved a boundary but explicitly did
not authorize feature implementation, and the Harness bootstrap task allowed
no Solar changes. The definition records that limit instead of inventing a
grant. A later implementation definition must cite a real implementation
authority and change the prohibited/write capabilities consistently.

The command exits `0` only when the definition passes every structural and
authority check, `1` when it writes a failed validation record, and `2` for a
usage or I/O error. It will not overwrite an existing run record.

See [`docs/work-definition.md`](docs/work-definition.md) for the contract and
the scope intentionally left out of this first slice.
