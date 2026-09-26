using Template.Domain.DomainEvents;

namespace Template.Domain.Models
{
    public abstract class BaseEntity
    {
        int? _requestedHashCode;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public virtual string Id { get; set; } = Guid.NewGuid().ToString();

        private List<IDomainEvent>? _domainEvents;
        public IReadOnlyCollection<IDomainEvent> DomainEvents =>
            _domainEvents?.AsReadOnly() ?? (IReadOnlyCollection<IDomainEvent>)Array.Empty<IDomainEvent>();

        public void AddDomainEvent(IDomainEvent eventItem)
        {
            _domainEvents = _domainEvents ?? new List<IDomainEvent>();
            _domainEvents.Add(eventItem);
        }

        public void RemoveDomainEvent(IDomainEvent eventItem)
        {
            _domainEvents?.Remove(eventItem);
        }

        public void ClearDomainEvents()
        {
            _domainEvents?.Clear();
        }

        public bool IsTransient()
        {
            return Id == default;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is BaseEntity))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (GetType() != obj.GetType())
                return false;

            var item = (BaseEntity)obj;

            if (item.IsTransient() || IsTransient())
                return false;
            return item.Id == Id;
        }

        public override int GetHashCode()
        {
            if (IsTransient()) return base.GetHashCode();

            _requestedHashCode ??= Id.GetHashCode() ^ 31;

            return _requestedHashCode.Value;
        }

        public static bool operator ==(BaseEntity left, BaseEntity right)
        {
            if (Equals(left, null))
                return (Equals(right, null)) ? true : false;
            return left.Equals(right);
        }

        public static bool operator !=(BaseEntity left, BaseEntity right)
        {
            return !(left == right);
        }
    }
}