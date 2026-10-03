"""Verify E1 independent adoption against the restored project dependency graphs."""

import json
from pathlib import Path


root = Path(__file__).resolve().parents[2]
actor = root / "src/ModulithFoundry.ActorIdentity/ModulithFoundry.ActorIdentity.csproj"
tenancy = root / "src/ModulithFoundry.Tenancy/ModulithFoundry.Tenancy.csproj"
sample = root / "samples/Wholesale/ContextDemo/ContextDemo.csproj"


def read_assets(project):
    assets_path = project.parent / "obj/project.assets.json"
    if not assets_path.is_file():
        raise SystemExit(f"Restore {project.relative_to(root)} before dependency verification.")
    assets = json.loads(assets_path.read_text())
    frameworks = {
        name
        for framework in assets["project"]["frameworks"].values()
        for name in framework.get("frameworkReferences", {})
    }
    if frameworks != {"Microsoft.NETCore.App"}:
        raise SystemExit(f"Unexpected framework references for {project.relative_to(root)}.")
    return assets


def verify(project, expected_libraries, expected_references):
    assets = read_assets(project)
    actual = {
        (name.rsplit("/", 1)[0], details["type"])
        for name, details in assets["libraries"].items()
    }
    if actual != expected_libraries:
        raise SystemExit(
            f"Unexpected dependencies for {project.relative_to(root)}: {sorted(actual)}"
        )
    verify_references(project, assets, expected_references)


def verify_references(project, assets, expected_references):
    references = {
        Path(path).resolve()
        for framework in assets["project"]["restore"]["frameworks"].values()
        for path in framework["projectReferences"]
    }
    if references != expected_references:
        raise SystemExit(f"Unexpected project references for {project.relative_to(root)}.")


def verify_test_consumer(project, library):
    assets = read_assets(project)
    projects = {
        name.rsplit("/", 1)[0]
        for name, details in assets["libraries"].items()
        if details["type"] == "project"
    }
    if projects != {library.stem}:
        raise SystemExit(f"Unexpected transitive project references for {project.relative_to(root)}.")
    verify_references(project, assets, {library.resolve()})


verify(actor, set(), set())
verify(tenancy, set(), set())
verify_test_consumer(root / "tests/ActorIdentityTests/ActorIdentityTests.csproj", actor)
verify_test_consumer(root / "tests/TenantTests/TenantTests.csproj", tenancy)
verify(
    sample,
    {
        ("ModulithFoundry.ActorIdentity", "project"),
        ("ModulithFoundry.Tenancy", "project"),
        ("Microsoft.Extensions.DependencyInjection", "package"),
        ("Microsoft.Extensions.DependencyInjection.Abstractions", "package"),
    },
    {actor.resolve(), tenancy.resolve()},
)
print("Verified independent package-free actor/tenancy libraries, separate test consumers and DI-only sample.")
