using LogicManager.Persistence.Interfaces;
using LogicManager.Persistence.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;


namespace LogicManager.Persistence.Services;

public class MongoDbService : IMongoDbService
{

    private readonly IMongoCollection<TrainConfiguration> _trainConfigs;
    private readonly ILogger<MongoDbService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _trainId;
    private readonly Dictionary<string, Hardware> _hardwareCache = new Dictionary<string, Hardware>();//Öbellekte tutmak icin
    private readonly Dictionary<string, Software> _softwareCache = new Dictionary<string, Software>();
    private readonly Dictionary<string, TrainConfiguration> _trainConfigCache = new Dictionary<string, TrainConfiguration>();



    public MongoDbService(IConfiguration configuration,
        IOptions<MongoDbSettings> mongoSettings,
        ILogger<MongoDbService> logger)
    {
        var client = new MongoClient(mongoSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoSettings.Value.DatabaseName);
        _trainConfigs = database.GetCollection<TrainConfiguration>(mongoSettings.Value.CollectionName);
        _logger = logger;
         _configuration = configuration;
        _trainId = _configuration["MongoDb:TrainId"]!;
    }
    public async Task<Hardware> GetHardwareByNameAsync(string name)
    {
        if (_hardwareCache.TryGetValue(name, out var cachedHardware))
        {
            return cachedHardware;
        }
        try
        {

            var filter = Builders<TrainConfiguration>.Filter.ElemMatch(x => x.Hardware,
                Builders<Hardware>.Filter.Eq(h => h.Name, name));

            var config = await _trainConfigs.Find(filter).FirstOrDefaultAsync();
            return config?.Hardware?.FirstOrDefault(h => h.Name == name)!;


        }
        catch (Exception)
        {
            Console.WriteLine("Hardware bilgisi alınırken hata oluştu");
            throw;
        }
    }

    public async Task<Software> GetSoftwareByNameAsync(string name)
    {

        if (_softwareCache.TryGetValue(name, out var cachedSoftware))
        {
            return cachedSoftware;
        }
        try
        {
            var filter = Builders<TrainConfiguration>.Filter.ElemMatch(x => x.Software,
                Builders<Software>.Filter.Eq(s => s.Name, name));

            var config = await _trainConfigs.Find(filter).FirstOrDefaultAsync();
            return config?.Software?.FirstOrDefault(s => s.Name == name)!;
        }
        catch (Exception)
        {
            Console.WriteLine("Software bilgisi alınırken hata oluştu");
            throw;
        }
    }

    public async Task<TrainConfiguration> GetTrainConfigurationAsync()
    {
        
        if (_trainConfigCache.TryGetValue(_trainId, out var cachedConfig))
        {
            return cachedConfig;
        }
        try
        {
            var filter = Builders<TrainConfiguration>.Filter.Eq(x => x.TrainId, _trainId);
            return await _trainConfigs.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception)
        {
            Console.WriteLine("MongoDB'den tren konfigürasyonu alınırken hata oluştu");
            //_logService?.ErrorSendLogAsync(new ErrorLogDto
            //{
            //    MessageSource = "LogicManager",
            //    MessageContent = "RabbitMQ Bağlantı hatası: 5 saniye sonra tekrar denenecek...",
            //    MessageType = LogType.Error.ToString(),
            //    DateTime = DateTime.Now,
            //    ErrorType = LogType.Error.ToString(),
            //    HardwareIP = "100.10.107.20"
            //});
            throw;
        }
    }
}
