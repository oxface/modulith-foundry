using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using ModulithFoundry.ActorIdentity;
using ModulithFoundry.ActorIdentity.AspNetCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;
using ModulithFoundry.Tenancy;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectionAssembly = System.Reflection.Assembly;

namespace ModulithFoundry.ArchitectureTests;

public sealed class AssemblyDependencyTests
{
    private const string Actor = "ModulithFoundry.ActorIdentity";
    private const string ActorHttp = "ModulithFoundry.ActorIdentity.AspNetCore";
    private const string Tenancy = "ModulithFoundry.Tenancy";
    private const string Persistence = "ModulithFoundry.Persistence.EntityFrameworkCore";
    private const string ContextSample = "ContextDemo";
    private const string PersistenceSample = "PersistenceDemo";
    private const string HttpSample = "HttpIdentityDemo";

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(ActorId).Assembly,
            typeof(ActorContextMiddleware).Assembly,
            typeof(TenantId).Assembly,
            typeof(TenantOwnershipExtensions).Assembly,
            typeof(Samples.Wholesale.ContextDemo.DemoComposition).Assembly,
            typeof(Samples.Wholesale.PersistenceDemo.DemoComposition).Assembly,
            typeof(Samples.Wholesale.HttpIdentityDemo.DemoComposition).Assembly
        )
        .Build();

    [Theory]
    [InlineData(Actor, Tenancy)]
    [InlineData(Tenancy, Actor)]
    [InlineData(Persistence, Actor)]
    [InlineData(Persistence, Tenancy)]
    [InlineData(Actor, ContextSample)]
    [InlineData(Actor, PersistenceSample)]
    [InlineData(Tenancy, ContextSample)]
    [InlineData(Tenancy, PersistenceSample)]
    [InlineData(Persistence, ContextSample)]
    [InlineData(Persistence, PersistenceSample)]
    [InlineData(ActorHttp, Tenancy)]
    [InlineData(ActorHttp, Persistence)]
    [InlineData(ActorHttp, ContextSample)]
    [InlineData(ActorHttp, PersistenceSample)]
    [InlineData(ActorHttp, HttpSample)]
    [InlineData(Actor, HttpSample)]
    [InlineData(Tenancy, HttpSample)]
    [InlineData(Persistence, HttpSample)]
    public void LibrariesDoNotUseForbiddenSegmentsOrConsumerTypes(
        string library,
        string forbidden
    ) => NoDependency(library, forbidden).Check(Architecture);

    private static TypesShouldConjunction NoDependency(string source, string forbidden)
    {
        var sourceTypes = Types().That().ResideInAssembly(ReflectionAssembly.Load(source));
        var forbiddenTypes = Types().That().ResideInAssembly(ReflectionAssembly.Load(forbidden));
        Assert.NotEmpty(sourceTypes.GetObjects(Architecture));
        Assert.NotEmpty(forbiddenTypes.GetObjects(Architecture));
        return sourceTypes.Should().NotDependOnAny(forbiddenTypes);
    }
}
