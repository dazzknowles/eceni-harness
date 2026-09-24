from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable

from eceni_harness import __version__
from eceni_harness.validation import Finding


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def render_record(
    *,
    run_id: str,
    assessed_at: str,
    source_path: str,
    source_digest: str,
    definition_id: str,
    findings: Iterable[Finding],
) -> str:
    ordered = list(findings)
    result = "Pass" if not ordered else "Fail"
    lines = [
        f"# Eceni Harness run: {run_id}",
        "",
        f"- Result: **{result}**",
        f"- Criterion assessed: `{definition_id}` conforms structurally to `eceni-harness.work-definition/1`",
        f"- Assessed at: `{assessed_at}`",
        f"- Responsible assessment: `eceni-harness {__version__}` deterministic validator",
        f"- Input: `{source_path}`",
        f"- Input SHA-256: `{source_digest}`",
        "",
        "## Evidence",
        "",
    ]
    if ordered:
        lines.append(f"Validation produced {len(ordered)} finding(s):")
        lines.append("")
        for finding in ordered:
            lines.append(f"- **{finding.code}** `{finding.path}` — {finding.message}")
    else:
        lines.extend(
            [
                "The input passed every implemented structural, source-pinning, authority,",
                "acceptance, independent-verification, and obligation-completeness check.",
                "This is preflight evidence only; it is not implementation or product acceptance evidence.",
            ]
        )
    lines.extend(
        [
            "",
            "## Result boundary",
            "",
            "`Pass` means only that the supplied definition satisfied this validator. It does not",
            "establish that cited authority is authentic, that evidence is substantively adequate,",
            "or that the target work has been implemented or accepted.",
            "",
        ]
    )
    return "\n".join(lines)


def write_new_record(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        stream.write(content)
