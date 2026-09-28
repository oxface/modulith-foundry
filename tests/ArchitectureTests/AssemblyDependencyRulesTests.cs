using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using ModulithFoundry.Testing.Architecture;

using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

namespace ArchitectureTests;

public sealed class AssemblyDependencyRulesTests
{
    private static readonly ModuleDefinition[] Modules = [.. RepositoryTopology.Modules()];

    private static readonly string[] ContractAssemblyNames = [.. Modules
        .Select(module => module.ContractsProject.AssemblyName)];

    private static readonly string[] ImplementationAssemblyNames = [.. Modules
        .Select(module => module.ImplementationProject.AssemblyName)];

    private static readonly ReflectionAssembly[] ModuleAssemblies = ContractAssemblyNames
        .Concat(ImplementationAssemblyNames)
        .Select(ReflectionAssembly.Load)
        .ToArray();

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies([.. ModuleAssemblies, ReflectionAssembly.Load("ModulithFoundry.Api")])
        .Build();

    [Fact]
    public void ModuleContracts_DoNotDependOnImplementations()
    {
        foreach (string contracts in ContractAssemblyNames)
        {
            foreach (string implementation in ImplementationAssemblyNames)
            {
                Types().That().ResideInAssembly(contracts)
                    .Should().NotDependOnAny(Types().That().ResideInAssembly(implementation))
                    .WithoutRequiringPositiveResults()
                    .Check(Architecture);
            }
        }
    }

    [Fact]
    public void ModuleImplementations_DoNotDependOnOtherImplementations()
    {
        foreach (string implementation in ImplementationAssemblyNames)
        {
            foreach (string otherImplementation in ImplementationAssemblyNames.Where(name =>
                name != implementation))
            {
                Types().That().ResideInAssembly(implementation)
                    .Should().NotDependOnAny(Types().That().ResideInAssembly(otherImplementation))
                    .WithoutRequiringPositiveResults()
                    .Check(Architecture);
            }
        }
    }

    [Fact]
    public void ModuleContracts_DoNotDependOnInfrastructureTypes()
    {
        IObjectProvider<IType> infrastructureTypes = Types().That()
            .HaveFullNameStartingWith("Microsoft.AspNetCore.")
            .Or().HaveFullNameStartingWith("Microsoft.EntityFrameworkCore.")
            .Or().HaveFullNameStartingWith("Npgsql.")
            .Or().HaveFullNameStartingWith("Rebus.")
            .Or().HaveFullNameStartingWith("System.Linq.IQueryable")
            .As("ASP.NET Core, EF Core, Npgsql, Rebus, or IQueryable types");

        foreach (string contracts in ContractAssemblyNames)
        {
            Types().That().ResideInAssembly(contracts)
                .Should().NotDependOnAny(infrastructureTypes)
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }
    }

    [Fact]
    public void ModuleImplementations_ExposeOnlyCompositionAndExplicitExtensionPoints()
    {
        System.Type[] leakedTypes = ImplementationAssemblyNames
            .Select(ReflectionAssembly.Load)
            .SelectMany(assembly => assembly.ExportedTypes)
            .Where(type => type.Namespace?.EndsWith(".Composition", StringComparison.Ordinal) is not true)
            .Where(type => type.Namespace?.EndsWith(".ExtensionPoints", StringComparison.Ordinal) is not true)
            .ToArray();

        Assert.Empty(leakedTypes);
    }

    [Fact]
    public void Api_DoesNotDependOnModuleFeatureTypes()
    {
        IObjectProvider<IType> moduleFeatureTypes = Types().That()
            .HaveFullNameContaining(".Domain.")
            .Or().HaveFullNameContaining(".Features.")
            .Or().HaveFullNameContaining(".Messaging.")
            .Or().HaveFullNameContaining(".Persistence.")
            .As("module feature, domain, messaging, or persistence types");

        Types().That().ResideInAssembly("ModulithFoundry.Api")
            .Should().NotDependOnAny(moduleFeatureTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
}
