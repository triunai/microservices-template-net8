using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Primitives
{
    public abstract class Entity
    {
        public Guid Id { get; protected set; } = Uuid7.NewUuid7();

        protected Entity() { }
        
        protected Entity(Guid id)
        {
            Id = id;
        }

        public override bool Equals(object? obj)
            => obj is Entity other && other.GetType() == GetType() && other.Id == Id;

        public static bool operator ==(Entity a, Entity b) => a.Equals(b);
        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);

        public override int GetHashCode() => HashCode.Combine(GetType(), Id);
    }
}
