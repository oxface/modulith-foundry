using System.Diagnostics.CodeAnalysis;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

namespace ArchitectureTests;

public sealed class AssemblyDependencyRulesTests
{
    private static readonly string[] ContractAssemblyNames =
    [
        "ModulithFoundry.Modules.Access.Contracts",
        "ModulithFoundry.Modules.Inventory.Contracts",
        "ModulithFoundry.Modules.Purchasing.Contracts",
        "ModulithFoundry.Modules.Sales.Contracts"
    ];

    private static readonly string[] ImplementationAssemblyNames =
    [
        "ModulithFoundry.Modules.Access",
        "ModulithFoundry.Modules.Inventory",
        "ModulithFoundry.Modules.Purchasing",
        "ModulithFoundry.Modules.Sales"
    ];

    private static readonly ReflectionAssembly[] ModuleAssemblies = ContractAssemblyNames
        .Concat(ImplementationAssemblyNames)
        .Select(ReflectionAssembly.Load)
        .ToArray();

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies([.. ModuleAssemblies, ReflectionAssembly.Load("ModulithFoundry.Api")])
        .Build();

    private static readonly IObjectProvider<IType> Contracts = Types().That()
        .ResideInAssembly(ContractAssemblyNames[0])
        .Or().ResideInAssembly(ContractAssemblyNames[1])
        .Or().ResideInAssembly(ContractAssemblyNames[2])
        .Or().ResideInAssembly(ContractAssemblyNames[3])
        .As("module Contracts");

    private static readonly IObjectProvider<IType> Implementations = Types().That()
        .ResideInAssembly(ImplementationAssemblyNames[0])
        .Or().ResideInAssembly(ImplementationAssemblyNames[1])
        .Or().ResideInAssembly(ImplementationAssemblyNames[2])
        .Or().ResideInAssembly(ImplementationAssemblyNames[3])
        .As("module implementations");

    [Fact]
    public void ModuleContracts_DoNotDependOnImplementations()
    {
        Types().That().Are(Contracts)
            .Should().NotDependOnAny(Implementations)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void ModuleImplementations_DoNotDependOnOtherImplementations()
    {
        for (int index = 0; index < ImplementationAssemblyNames.Length; index++)
        {
            string assemblyName = ImplementationAssemblyNames[index];
            IObjectProvider<IType> otherImplementations = OtherImplementations(index);

            Types().That().ResideInAssembly(assemblyName)
                .Should().NotDependOnAny(otherImplementations)
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
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

        Types().That().Are(Contracts)
            .Should().NotDependOnAny(infrastructureTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void ModuleNamespaces_AreAcyclic()
    {
        Slices().Matching("ModulithFoundry.Modules.(*)")
            .Should().BeFreeOfCycles()
            .Check(Architecture);
    }

    [Fact]
    public void ModuleImplementations_ExposeOnlyCompositionEntryPoints()
    {
        System.Type[] leakedTypes = ImplementationAssemblyNames
            .Select(ReflectionAssembly.Load)
            .SelectMany(assembly => assembly.ExportedTypes)
            .Where(type => type.Namespace?.EndsWith(".Composition", StringComparison.Ordinal) is not true)
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

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "The ArchUnitNET concrete fluent type is an implementation detail; callers need its provider contract.")]
    private static IObjectProvider<IType> OtherImplementations(int excludedIndex)
    {
        string[] names = ImplementationAssemblyNames
            .Where((_, index) => index != excludedIndex)
            .ToArray();

        return Types().That()
            .ResideInAssembly(names[0])
            .Or().ResideInAssembly(names[1])
            .Or().ResideInAssembly(names[2])
            .As("other module implementations");
    }
}
