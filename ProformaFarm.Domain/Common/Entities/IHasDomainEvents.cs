using System.Collections.Generic;
using ProformaFarm.Domain.Common.Events;

namespace ProformaFarm.Domain.Common.Entities;

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
