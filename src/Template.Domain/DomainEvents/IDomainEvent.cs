namespace Template.Domain.DomainEvents;

/// <summary>
/// Marks a type as a domain event raised by an entity via <c>BaseEntity.AddDomainEvent</c>.
/// Wolverine discovers handlers by convention and needs no marker of its own, so this
/// interface exists purely so the Domain layer can describe its own events without
/// referencing a messaging library — the dependency rule this template is built on.
/// </summary>
public interface IDomainEvent
{
}
