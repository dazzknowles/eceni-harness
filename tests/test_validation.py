from __future__ import annotations

from copy import deepcopy
import json
from pathlib import Path
import unittest

from eceni_harness.validation import validate_definition


EXAMPLE = Path(__file__).parents[1] / "data" / "work-definitions" / "solar-optimiser-5.json"


class WorkDefinitionValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.definition = json.loads(EXAMPLE.read_text(encoding="utf-8"))

    def codes(self, document: dict) -> set[str]:
        return {finding.code for finding in validate_definition(document)}

    def test_solar_issue_5_preflight_is_valid(self) -> None:
        self.assertEqual([], validate_definition(self.definition))

    def test_missing_authority_fails_closed(self) -> None:
        document = deepcopy(self.definition)
        del document["authority"]["grants"]

        self.assertIn("WD009", self.codes(document))

    def test_grant_and_prohibition_conflict_fails_closed(self) -> None:
        document = deepcopy(self.definition)
        document["authority"]["grants"].append(
            {
                "id": "BAD-GRANT",
                "repository": "https://github.com/dazzknowles/solar-optimiser",
                "capability": "repository.write",
                "scope": "Contradicts the explicit prohibition.",
                "source": "test:contradiction",
            }
        )

        self.assertIn("AUTH001", self.codes(document))

    def test_change_repository_requires_explicit_write_grant(self) -> None:
        document = deepcopy(self.definition)
        document["execution"]["reference_only_repositories"].remove(
            "https://github.com/dazzknowles/solar-optimiser"
        )
        document["execution"]["change_repositories"].append(
            "https://github.com/dazzknowles/solar-optimiser"
        )

        codes = self.codes(document)
        self.assertIn("AUTH003", codes)
        self.assertIn("AUTH004", codes)

    def test_unpinned_git_input_is_rejected(self) -> None:
        document = deepcopy(self.definition)
        document["authoritative_inputs"][0]["revision"] = "main"

        self.assertIn("WD014", self.codes(document))

    def test_adopted_baseline_must_match_authoritative_input(self) -> None:
        document = deepcopy(self.definition)
        document["governance"]["baseline"]["commit"] = "0" * 40

        self.assertIn("WD016", self.codes(document))

    def test_independent_verifier_must_differ_from_implementer(self) -> None:
        document = deepcopy(self.definition)
        document["acceptance"]["independent_verification"]["verifier_role"] = document[
            "acceptance"
        ]["implementer_role"]

        self.assertIn("ACC007", self.codes(document))

    def test_open_follow_up_requires_acceptance_conditions(self) -> None:
        document = deepcopy(self.definition)
        document["obligations"]["follow_ups"][0]["acceptance_conditions"] = []

        self.assertIn("OBL003", self.codes(document))


if __name__ == "__main__":
    unittest.main()
