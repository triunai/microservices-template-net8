using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Domain.Primitives
{
    public abstract class Entity
    {
        public Guid Id { get; protected set; } = Guid.NewGuid();

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
