namespace LogicManager.Infrastructure.Interfaces;

public interface ITakoReaderService
{
    event Action<int> TakoVerisiOkundu;//tako verisi geldiğinde tetiklenecek event
    Task<int> ReadTakoPulseAsync();
    Task<double> ReadTakoValueAsync();
    Task<bool> ReadDoorStatusAsync();
    Task<double> ReadSpeedStatusAsync();

}
