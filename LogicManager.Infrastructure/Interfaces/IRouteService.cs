using LogicManager.Domain.Entities;

namespace LogicManager.Infrastructure.Interfaces;

public interface IRouteService
{
    event Action<List<Station>> OnRouteUpdated; // Olayı tanımlıyoruz
    Task<List<Station>> GetAllRouteAsync();

    Task<bool> IsRouteEstablishedAsync();
}
