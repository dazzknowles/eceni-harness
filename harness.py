"""Repository-local entry point for the dependency-free Harness CLI."""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).parent / "src"))

from eceni_harness.cli import main  # noqa: E402


raise SystemExit(main())
