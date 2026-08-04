using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;

namespace LogicManager.Infrastructure.Services;

public class SyncManager : ISyncManager
{

    private TrainSyncMessage? _syncMessage;
    private readonly object _lock = new object();


    public void SetSyncMessage(TrainSyncMessage syncMessage)
    {
        lock (_lock)
        {
            _syncMessage = syncMessage;
            Console.WriteLine($"[SyncManager] Sync verisi alındı: {syncMessage.NextStation}. istasyon, {syncMessage.RemainingDistance} m");
        }
    }

    public TrainSyncMessage? GetSyncMessage()
    {
        lock (_lock)
        {
            return _syncMessage;
        }
    }

    public void ClearSyncMessage()
    {
        lock (_lock)
        {
            _syncMessage = null;
        }
    }
}
