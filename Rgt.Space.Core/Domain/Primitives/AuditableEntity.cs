using System;

namespace Rgt.Space.Core.Domain.Primitives
{
    public abstract class AuditableEntity : Entity
    {
        public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; protected set; }
        
        public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; protected set; }

        public bool IsDeleted { get; protected set; }
        public DateTime? DeletedAt { get; protected set; }
        public Guid? DeletedBy { get; protected set; }

        protected AuditableEntity() { }

        protected AuditableEntity(Guid id) : base(id) { }

        // Methods for audit updates can be added here or handled via EF Core Interceptors
        public void UpdateAudit(Guid userId)
        {
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = userId;
        }

        public void Delete(Guid userId)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            DeletedBy = userId;
        }
    }
}
