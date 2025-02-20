using LogicManager.Persistence.Interfaces;
using LogicManager.Persistence.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;


namespace LogicManager.Persistence.Services;

public class MongoDbService : IMongoDbService
{

    private readonly IMongoCollection<TrainConfiguration> _trainConfigs;
    private readonly ILogger<MongoDbService> _logger;

    public MongoDbService(
        IOptions<MongoDbSettings> mongoSettings,
        ILogger<MongoDbService> logger)
    {
        var client = new MongoClient(mongoSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoSettings.Value.DatabaseName);
        _trainConfigs = database.GetCollection<TrainConfiguration>(mongoSettings.Value.CollectionName);
        _logger = logger;
    }
    public async Task<Hardware> GetHardwareByNameAsync(string name)
    {
        try
        {

            var filter = Builders<TrainConfiguration>.Filter.ElemMatch(x => x.Hardware,
                Builders<Hardware>.Filter.Eq(h => h.Name, name));

            var config = await _trainConfigs.Find(filter).FirstOrDefaultAsync();
            return config?.Hardware?.FirstOrDefault(h => h.Name == name)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware bilgisi alınırken hata oluştu");
            throw;
        }
    }

    public async Task<Software> GetSoftwareByNameAsync(string name)
    {
        try
        {
            var filter = Builders<TrainConfiguration>.Filter.ElemMatch(x => x.Software,
                Builders<Software>.Filter.Eq(s => s.Name, name));

            var config = await _trainConfigs.Find(filter).FirstOrDefaultAsync();
            return config?.Software?.FirstOrDefault(s => s.Name == name)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Software bilgisi alınırken hata oluştu");
            throw;
        }
    }

    public async Task<TrainConfiguration> GetTrainConfigurationAsync(string trainId)
    {
        try
        {
            var filter = Builders<TrainConfiguration>.Filter.Eq(x => x.TrainId, trainId);
            return await _trainConfigs.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB'den tren konfigürasyonu alınırken hata oluştu");
            throw;
        }
    }
}
