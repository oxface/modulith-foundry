"""Verify E1 independent adoption against the restored project dependency graphs."""

import json
from pathlib import Path


root = Path(__file__).resolve().parents[2]
library = root / "src/ModulithFoundry.ExecutionIdentity/ModulithFoundry.ExecutionIdentity.csproj"
sample = root / "samples/Wholesale/ContextDemo/ContextDemo.csproj"


def verify(project, expected_libraries, expected_references):
    assets_path = project.parent / "obj/project.assets.json"
    if not assets_path.is_file():
        raise SystemExit(f"Restore {project.relative_to(root)} before dependency verification.")
    assets = json.loads(assets_path.read_text())
    actual = {
        (name.rsplit("/", 1)[0], details["type"])
        for name, details in assets["libraries"].items()
    }
    if actual != expected_libraries:
        raise SystemExit(
            f"Unexpected dependencies for {project.relative_to(root)}: {sorted(actual)}"
        )
    references = {
        Path(path).resolve()
        for framework in assets["project"]["restore"]["frameworks"].values()
        for path in framework["projectReferences"]
    }
    if references != expected_references:
        raise SystemExit(f"Unexpected project references for {project.relative_to(root)}.")


verify(library, set(), set())
verify(
    sample,
    {
        ("ModulithFoundry.ExecutionIdentity", "project"),
        ("Microsoft.Extensions.DependencyInjection", "package"),
        ("Microsoft.Extensions.DependencyInjection.Abstractions", "package"),
    },
    {library.resolve()},
)
print("Verified package-free context library and standalone DI-only sample dependencies.")
