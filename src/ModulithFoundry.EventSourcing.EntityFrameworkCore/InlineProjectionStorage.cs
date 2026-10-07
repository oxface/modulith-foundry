using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>Native loading and metadata validation for an explicitly mapped inline row.</summary>
public sealed class InlineProjectionStorage<TStream, TRow>
    where TStream : class, IEventStreamRecord
    where TRow : class, IInlineStateRecord
{
    private readonly DbContext database;
    private readonly IForeignKey reference;

    public InlineProjectionStorage(DbContext database)
    {
        ArgumentNullException.ThrowIfNull(database);
        this.database = database;
        reference = InlineProjectionModel.Reference(database.Model, typeof(TStream), typeof(TRow));
    }

    public async Task<TRow> LoadAsync(TStream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (
            stream.Version < 1
            || stream.CreatedAt.Offset != TimeSpan.Zero
            || stream.UpdatedAt.Offset != TimeSpan.Zero
            || stream.CreatedAt > stream.UpdatedAt
        )
            throw new InvalidDataException("A valid existing stream header is required.");
        var parameter = Expression.Parameter(typeof(TRow), "row");
        Expression predicate = Expression.Constant(true);
        for (int index = 0; index < reference.Properties.Count; index++)
        {
            IProperty property = reference.Properties[index];
            object? value = reference.PrincipalKey.Properties[index].PropertyInfo!.GetValue(stream);
            predicate = Expression.AndAlso(
                predicate,
                Expression.Equal(
                    Expression.Property(parameter, property.PropertyInfo!),
                    Expression.Constant(value, property.ClrType)
                )
            );
        }
        var row = await database
            .Set<TRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                Expression.Lambda<Func<TRow, bool>>(predicate, parameter),
                cancellationToken
            );
        if (row is null || row.Version < stream.Version)
            throw new InvalidDataException(
                "A required inline state is missing or behind its stream."
            );
        Validate(stream, row);
        return row;
    }

    /// <summary>Validates an already fetched native row without another database query.</summary>
    public void Validate(TStream stream, TRow row)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(row);
        if (
            stream.Version < 1
            || stream.CreatedAt.Offset != TimeSpan.Zero
            || stream.UpdatedAt.Offset != TimeSpan.Zero
            || stream.CreatedAt > stream.UpdatedAt
            || row.Version < stream.Version
        )
            throw new InvalidDataException("Inline state is behind an invalid or advanced header.");
        if (
            !reference
                .Properties.Select(property => property.PropertyInfo!.GetValue(row))
                .SequenceEqual(
                    reference.PrincipalKey.Properties.Select(property =>
                        property.PropertyInfo!.GetValue(stream)
                    )
                )
        )
            throw new InvalidDataException("Inline state must match the complete stream key.");
        if (row.Version > stream.Version)
            throw new DbUpdateConcurrencyException("Inline state advanced after the header read.");
        if (!row.RecordedAt.EqualsExact(stream.UpdatedAt))
            throw new InvalidDataException("Inline state and stream timestamps disagree.");
    }

    internal PreparedInlineProjection Prepare(
        TStream stream,
        TRow? committed,
        TRow candidate,
        long nextVersion,
        DateTimeOffset recordedAt
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (
            ReferenceEquals(committed, candidate)
            || database.Entry(candidate).State != EntityState.Detached
        )
            throw new InvalidOperationException(
                "An inline candidate must be a fresh detached row."
            );
        for (int index = 0; index < reference.Properties.Count; index++)
        {
            object? value = reference.PrincipalKey.Properties[index].PropertyInfo!.GetValue(stream);
            reference.Properties[index].PropertyInfo!.SetValue(candidate, value);
        }
        candidate.Version = nextVersion;
        candidate.RecordedAt = recordedAt;
        if (committed is not null && database.Entry(committed).State != EntityState.Detached)
            throw new InvalidOperationException("Use an untracked inline observation.");
        object?[] key = reference
            .Properties.Select(property => property.PropertyInfo!.GetValue(candidate))
            .ToArray();
        if (
            database
                .ChangeTracker.Entries<TRow>()
                .Any(entry =>
                    key.SequenceEqual(InlineProjectionModel.Values(entry, reference.Properties))
                )
        )
            throw new InvalidOperationException(
                "The observed inline row must not already be tracked."
            );
        return new PreparedRow(database, committed, candidate);
    }

    private sealed class PreparedRow(DbContext database, TRow? committed, TRow candidate)
        : PreparedInlineProjection
    {
        internal override void Stage()
        {
            if (committed is null)
                database.Set<TRow>().Add(candidate);
            else
            {
                database.Set<TRow>().Attach(committed);
                database.Entry(committed).CurrentValues.SetValues(candidate);
            }
        }
    }
}

internal abstract class PreparedInlineProjection
{
    internal abstract void Stage();
}

internal static class InlineProjectionModel
{
    internal static IForeignKey Reference(IReadOnlyModel model, Type streamType, Type rowType)
    {
        var stream =
            model.FindEntityType(streamType)
            ?? throw new InvalidOperationException(
                "Map the stream before configuring inline state."
            );
        var row =
            model.FindEntityType(rowType)
            ?? throw new InvalidOperationException("Map the inline row before configuring it.");
        if (
            stream.BaseType is not null
            || row.BaseType is not null
            || row.IsOwned()
            || row.GetDerivedTypes().Any()
            || row.GetMappingFragments().Any()
            || row.GetViewName() is not null
        )
            throw new InvalidOperationException("Use ordinary independent inline row types.");
        var key = stream.FindPrimaryKey();
        var reference =
            row.GetForeignKeys().SingleOrDefault(foreign => foreign.PrincipalKey == key)
            ?? throw new InvalidOperationException(
                "Inline state must reference the complete stream primary key."
            );
        if (!reference.Properties.SequenceEqual(row.FindPrimaryKey()?.Properties ?? []))
            throw new InvalidOperationException("Use one inline row per complete stream key.");
        if (
            reference.Properties.Any(property =>
                property.IsShadowProperty()
                || property.PropertyInfo is null
                || property.GetValueConverter() is not null
                || property.GetProviderClrType() is not null
            )
        )
            throw new InvalidOperationException("Use mapped unconverted inline keys.");
        if (
            row.FindProperty(nameof(IInlineStateRecord.Version))
                is not {
                    IsConcurrencyToken: true,
                    ValueGenerated: ValueGenerated.Never,
                    ClrType: var versionType
                }
            || versionType != typeof(long)
            || row.FindProperty(nameof(IInlineStateRecord.RecordedAt))?.ClrType
                != typeof(DateTimeOffset)
        )
            throw new InvalidOperationException(
                "Map inline version as a native concurrency token and recorded time."
            );
        return (IForeignKey)reference;
    }

    internal static object?[] Values(
        EntityEntry entry,
        IReadOnlyList<IProperty> properties,
        bool original = false
    ) =>
        properties
            .Select(property =>
                original
                    ? entry.Property(property.Name).OriginalValue
                    : entry.Property(property.Name).CurrentValue
            )
            .ToArray();
}
