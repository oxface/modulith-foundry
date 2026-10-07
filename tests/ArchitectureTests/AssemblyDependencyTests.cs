using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using ModulithFoundry.ActorIdentity;
using ModulithFoundry.ActorIdentity.AspNetCore;
using ModulithFoundry.Events.History;
using ModulithFoundry.Events.Serialization;
using ModulithFoundry.EventSourcing;
using ModulithFoundry.EventSourcing.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;
using ModulithFoundry.Tenancy;
using ModulithFoundry.Tenancy.AspNetCore;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectionAssembly = System.Reflection.Assembly;

namespace ModulithFoundry.ArchitectureTests;

public sealed class AssemblyDependencyTests
{
    private const string Actor = "ModulithFoundry.ActorIdentity";
    private const string ActorHttp = "ModulithFoundry.ActorIdentity.AspNetCore";
    private const string Tenancy = "ModulithFoundry.Tenancy";
    private const string TenancyHttp = "ModulithFoundry.Tenancy.AspNetCore";
    private const string Persistence = "ModulithFoundry.Persistence.EntityFrameworkCore";
    private const string ContextSample = "ContextDemo";
    private const string PersistenceSample = "PersistenceDemo";
    private const string HttpSample = "HttpIdentityDemo";
    private const string EventSerialization = "ModulithFoundry.Events.Serialization";
    private const string History = "ModulithFoundry.Events.History";
    private const string EventPersistenceSample = "EventPersistenceDemo";
    private const string AggregateCore = "ModulithFoundry.EventSourcing";
    private const string Storage = "ModulithFoundry.EventSourcing.EntityFrameworkCore";
    private const string StorageSample = "EventStorageDemo";
    private const string CodecSample = "EventCodecDemo";

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(ActorId).Assembly,
            typeof(ActorContextMiddleware).Assembly,
            typeof(TenantId).Assembly,
            typeof(TenantContextMiddleware).Assembly,
            typeof(TenantOwnershipExtensions).Assembly,
            typeof(SerializedEvent).Assembly,
            typeof(EventHistory).Assembly,
            typeof(IEventStreamRecord).Assembly,
            typeof(IEventSourcedAggregate<>).Assembly,
            typeof(Samples.EventStorageDemo.DemoJourneys).Assembly,
            typeof(Samples.Wholesale.EventCodecDemo.DemoJourneys).Assembly,
            typeof(Samples.Wholesale.EventPersistenceDemo.DemoJourneys).Assembly,
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
    [InlineData(TenancyHttp, Actor)]
    [InlineData(TenancyHttp, ActorHttp)]
    [InlineData(TenancyHttp, Persistence)]
    [InlineData(TenancyHttp, ContextSample)]
    [InlineData(TenancyHttp, PersistenceSample)]
    [InlineData(TenancyHttp, HttpSample)]
    [InlineData(ActorHttp, TenancyHttp)]
    [InlineData(Actor, TenancyHttp)]
    [InlineData(Tenancy, TenancyHttp)]
    [InlineData(Persistence, TenancyHttp)]
    public void LibrariesDoNotUseForbiddenSegmentsOrConsumerTypes(
        string library,
        string forbidden
    ) => NoDependency(library, forbidden).Check(Architecture);

    [Fact]
    public void EventSerializationUsesNoOtherSegmentsOrConsumerTypes()
    {
        foreach (
            string forbidden in new[]
            {
                Actor,
                ActorHttp,
                Tenancy,
                TenancyHttp,
                Persistence,
                ContextSample,
                PersistenceSample,
                HttpSample,
                CodecSample,
                EventPersistenceSample,
                AggregateCore,
                Storage,
                StorageSample,
                History,
            }
        )
            NoDependency(EventSerialization, forbidden).Check(Architecture);
    }

    [Fact]
    public void EventHistoryUsesNoOtherSegmentsOrConsumerTypes()
    {
        foreach (
            string forbidden in new[]
            {
                Actor,
                ActorHttp,
                Tenancy,
                TenancyHttp,
                Persistence,
                EventSerialization,
                ContextSample,
                PersistenceSample,
                HttpSample,
                CodecSample,
                EventPersistenceSample,
                AggregateCore,
                Storage,
                StorageSample,
            }
        )
            NoDependency(History, forbidden).Check(Architecture);
    }

    [Fact]
    public void AggregateCoreUsesNoOtherSegmentsOrConsumerTypes()
    {
        foreach (
            string forbidden in new[]
            {
                Actor,
                ActorHttp,
                Tenancy,
                TenancyHttp,
                Persistence,
                EventSerialization,
                History,
                Storage,
                ContextSample,
                PersistenceSample,
                HttpSample,
                CodecSample,
                EventPersistenceSample,
                StorageSample,
            }
        )
            NoDependency(AggregateCore, forbidden).Check(Architecture);
        Assert.DoesNotContain(
            typeof(IEventSourcedAggregate<>).Assembly.GetReferencedAssemblies(),
            assembly =>
                assembly.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || assembly.Name == "System.Text.Json"
        );
    }

    [Fact]
    public void EventStorageUsesNoOtherSegmentsOrConsumerTypes()
    {
        foreach (
            string forbidden in new[]
            {
                Actor,
                ActorHttp,
                Tenancy,
                TenancyHttp,
                Persistence,
                EventSerialization,
                History,
                ContextSample,
                PersistenceSample,
                HttpSample,
                CodecSample,
                EventPersistenceSample,
                StorageSample,
            }
        )
            NoDependency(Storage, forbidden).Check(Architecture);
    }

    [Fact]
    public void IndependentStorageConsumerUsesNoOtherSegmentsOrSampleApplications()
    {
        foreach (
            string forbidden in new[]
            {
                Actor,
                ActorHttp,
                Tenancy,
                TenancyHttp,
                Persistence,
                EventSerialization,
                History,
                ContextSample,
                PersistenceSample,
                HttpSample,
                CodecSample,
                EventPersistenceSample,
            }
        )
            NoDependency(StorageSample, forbidden).Check(Architecture);
    }

    private static TypesShouldConjunction NoDependency(string source, string forbidden)
    {
        var sourceTypes = Types().That().ResideInAssembly(ReflectionAssembly.Load(source));
        var forbiddenTypes = Types().That().ResideInAssembly(ReflectionAssembly.Load(forbidden));
        Assert.NotEmpty(sourceTypes.GetObjects(Architecture));
        Assert.NotEmpty(forbiddenTypes.GetObjects(Architecture));
        return sourceTypes.Should().NotDependOnAny(forbiddenTypes);
    }
}
