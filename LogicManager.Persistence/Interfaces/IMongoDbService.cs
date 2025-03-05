using LogicManager.Persistence.Models;

namespace LogicManager.Persistence.Interfaces;

public interface IMongoDbService
{
    Task<TrainConfiguration> GetTrainConfigurationAsync();
    Task<Hardware> GetHardwareByNameAsync(string name);
    Task<Software> GetSoftwareByNameAsync(string name);
}
