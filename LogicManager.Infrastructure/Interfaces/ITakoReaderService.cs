namespace LogicManager.Infrastructure.Interfaces;

public interface ITakoReaderService
{

    Task<int> ReadTakoPulseAsync();
    Task<double> ReadTakoValueAsync();
    Task<bool> ReadDoorStatusAsync();
    Task<double> ReadSpeedStatusAsync();
 
}
