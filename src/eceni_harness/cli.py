from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
import uuid

from eceni_harness.record import render_record, utc_now, write_new_record
from eceni_harness.validation import validate_definition


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="eceni-harness")
    subparsers = parser.add_subparsers(dest="command", required=True)
    validate = subparsers.add_parser("validate", help="validate a bounded work definition")
    validate.add_argument("definition", type=Path)
    validate.add_argument("--record", required=True, type=Path)
    validate.add_argument("--run-id")
    validate.add_argument("--assessed-at")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if args.command != "validate":
        return 2

    try:
        source_bytes = args.definition.read_bytes()
        document = json.loads(source_bytes)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        print(f"Unable to read work definition: {error}", file=sys.stderr)
        return 2

    findings = validate_definition(document)
    run_id = args.run_id or str(uuid.uuid4())
    assessed_at = args.assessed_at or utc_now()
    definition_id = document.get("id", "<missing>") if isinstance(document, dict) else "<invalid>"
    content = render_record(
        run_id=run_id,
        assessed_at=assessed_at,
        source_path=args.definition.as_posix(),
        source_digest=hashlib.sha256(source_bytes).hexdigest(),
        definition_id=definition_id,
        findings=findings,
    )
    try:
        write_new_record(args.record, content)
    except FileExistsError:
        print(f"Refusing to overwrite existing run record: {args.record}", file=sys.stderr)
        return 2
    except OSError as error:
        print(f"Unable to write run record: {error}", file=sys.stderr)
        return 2

    print(f"{('PASS' if not findings else 'FAIL')}: {args.record}")
    return 0 if not findings else 1


if __name__ == "__main__":
    raise SystemExit(main())
