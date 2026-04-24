namespace OrderFlow.Domain.Common;

/// <summary>
/// Base class for all domain entities. Equality is identity-based.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    /// <summary>Unique identifier of the entity.</summary>
    public Guid Id { get; protected set; }

    /// <summary>Protected constructor for ORM materialisation.</summary>
    protected Entity()
    {
    }

    /// <summary>Creates an entity with a caller-supplied identifier.</summary>
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity Id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    /// <inheritdoc />
    public bool Equals(Entity? other) =>
        other is not null && GetType() == other.GetType() && Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>Reference equality by identity.</summary>
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    /// <summary>Reference inequality by identity.</summary>
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
