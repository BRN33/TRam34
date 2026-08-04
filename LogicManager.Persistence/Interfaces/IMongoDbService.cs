using LogicManager.Persistence.Models;

namespace LogicManager.Persistence.Interfaces;

public interface IMongoDbService
{
    Task<TrainConfiguration?> GetTrainConfigurationAsync();

    Task<TrainConfiguration> GetTrainConfigurationByTrainIdAsync(string trainId);
    Task<Hardware> GetHardwareByNameAsync(string name);
    Task<Software?> GetSoftwareByNameAsync(string name);

    Task SetTrainId(string masterTrainId);
    Task<string?> GetYbsPcIpAsync(); // Sonradan eklenmiş metod 12.12.2025  YPS_PC IP adresini almak için
}
