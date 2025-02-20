using LogicManager.Domain.Entities;

namespace LogicManager.Infrastructure.Interfaces;

public interface IRouteService
{

    Task<List<Station>> GetAllRouteAsync();

    Task<bool> IsRouteEstablishedAsync();
}
