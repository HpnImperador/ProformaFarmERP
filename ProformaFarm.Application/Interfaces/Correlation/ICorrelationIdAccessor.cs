using System;

namespace ProformaFarm.Application.Interfaces.Correlation;

public interface ICorrelationIdAccessor
{
    Guid? GetCurrentCorrelationId();
}
