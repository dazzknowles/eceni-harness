from __future__ import annotations

import json
from pathlib import Path
import unittest

from eceni_harness.cli import main


ROOT = Path(__file__).parents[1]
EXAMPLE = ROOT / "data" / "work-definitions" / "solar-optimiser-5.json"


class CliTests(unittest.TestCase):
    def temporary_path(self, name: str) -> Path:
        path = ROOT / "tests" / name
        self.addCleanup(path.unlink, missing_ok=True)
        return path

    def test_valid_definition_writes_reproducible_pass_record(self) -> None:
        record = self.temporary_path(".valid-run.md")
        result = main(
            [
                "validate",
                str(EXAMPLE),
                "--record",
                str(record),
                "--run-id",
                "test-run",
                "--assessed-at",
                "2026-09-24T18:00:00Z",
            ]
        )

        self.assertEqual(0, result)
        content = record.read_text(encoding="utf-8")
        self.assertIn("Result: **Pass**", content)
        self.assertIn("test-run", content)
        self.assertIn("preflight evidence only", content)

    def test_invalid_definition_still_writes_fail_record(self) -> None:
        definition = json.loads(EXAMPLE.read_text(encoding="utf-8"))
        definition["authority"]["grants"] = []
        invalid = self.temporary_path(".invalid-definition.json")
        invalid.write_text(json.dumps(definition), encoding="utf-8")
        record = self.temporary_path(".failed-run.md")

        result = main(
            [
                "validate",
                str(invalid),
                "--record",
                str(record),
                "--run-id",
                "failed-run",
                "--assessed-at",
                "2026-09-24T18:01:00Z",
            ]
        )

        self.assertEqual(1, result)
        self.assertIn("Result: **Fail**", record.read_text(encoding="utf-8"))

    def test_existing_record_is_not_overwritten(self) -> None:
        record = self.temporary_path(".existing-run.md")
        record.write_text("existing", encoding="utf-8")

        result = main(["validate", str(EXAMPLE), "--record", str(record)])

        self.assertEqual(2, result)
        self.assertEqual("existing", record.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
