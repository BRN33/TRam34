using LogicManager.Persistence.Interfaces;
using LogicManager.Persistence.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Concurrent;


namespace LogicManager.Persistence.Services;

public class MongoDbService : IMongoDbService
{

    private readonly IMongoCollection<TrainConfiguration> _trainConfigs;
    private readonly ILogger<MongoDbService> _logger;
    private readonly IConfiguration _configuration;
    private string _trainId;

    // 1. Thread-safe ve performanslı cache yapısı
    private readonly ConcurrentDictionary<string, Hardware> _hardwareCache = new();
    private readonly ConcurrentDictionary<string, Software> _softwareCache = new();
    private readonly ConcurrentDictionary<string, TrainConfiguration> _trainConfigCache = new();

    //// 2. Eski ... 18.12.2025 Basit Dictionary tabanlı cache yapısı
    //private readonly Dictionary<string, Hardware> _hardwareCache = new Dictionary<string, Hardware>();//Öbellekte tutmak icin
    //private readonly Dictionary<string, Software> _softwareCache = new Dictionary<string, Software>();
    //private readonly Dictionary<string, TrainConfiguration> _trainConfigCache = new Dictionary<string, TrainConfiguration>();



    public MongoDbService(
        IConfiguration configuration,
        IOptions<MongoDbSettings> mongoSettings,
        ILogger<MongoDbService> logger)
    {
        var client = new MongoClient(mongoSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoSettings.Value.DatabaseName);
        _trainConfigs = database.GetCollection<TrainConfiguration>(mongoSettings.Value.CollectionName);
        _logger = logger;
        _configuration = configuration;
        //_trainId = _configuration["MongoDb:TrainId"]!;
        _trainId = "";

    }


    public async Task<Hardware> GetHardwareByNameAsync(string name)
    {
        if (_hardwareCache.TryGetValue(name, out var cachedHardware))
        {
            return cachedHardware;
        }
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)); // 100ms timeout
        try
        {

            var filter = Builders<TrainConfiguration>.Filter.ElemMatch(x => x.Hardware,
                Builders<Hardware>.Filter.Eq(h => h.Name, name));

            var config = await _trainConfigs.Find(filter).FirstOrDefaultAsync(cts.Token);
            return config?.Hardware?.FirstOrDefault(h => h.Name == name)!;


        }
        catch (Exception)
        {
            Console.WriteLine("Hardware bilgisi alınırken hata oluştu");
            throw;
        }
    }

    // Yazılım bilgisi çekme metodu, cache'li ve retry mekanizmalı. 06.04.2026 Güncellendi
    public async Task<Software?> GetSoftwareByNameAsync(string name)
    {
        // 1. CACHE
        if (_softwareCache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        int retryCount = 3;

        for (int i = 0; i < retryCount; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            try
            {
                var filter = Builders<TrainConfiguration>.Filter.ElemMatch(x => x.Software,
                    Builders<Software>.Filter.Eq(s => s.Name, name));

                var config = await _trainConfigs
                    .Find(filter)
                    .FirstOrDefaultAsync(cts.Token);

                var result = config?.Software?.FirstOrDefault(s => s.Name == name);

                if (result != null)
                {
                    // ✅ CACHE'E YAZ
                    _softwareCache[name] = result;
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Mongo hata (deneme {i + 1})");

                await Task.Delay(500); // küçük bekleme
            }
        }

        // ❗ Mongo tamamen fail → fallback
        if (_softwareCache.TryGetValue(name, out var fallback))
        {
            _logger.LogWarning("Mongo down → cache kullanıldı");
            return fallback;
        }

        _logger.LogError("Mongo ve cache başarısız");
        return null;
    }




    // TrainConfiguration'ı TrainId'ye göre çek, cache'le ve timeout ekle. 06.04.2026 Güncellendi
    public async Task<TrainConfiguration?> GetTrainConfigurationAsync()
    {
        // 1. Önce Cache kontrolü (Thread-safe çekim için TryGetValue iyidir)
        if (_trainConfigCache.TryGetValue(_trainId, out var cachedConfig))
        {
            return cachedConfig;
        }

        // 2. TrainId boşsa (RabbitMQ'dan henüz gelmediyse) hiç DB'ye gitme
        if (string.IsNullOrEmpty(_trainId))
        {
            _logger.LogWarning("GetTrainConfigurationAsync: TrainId henüz setlenmedi.");
            return null;
        }

        // 3. Makul bir timeout (5-10 saniye başlangıç için idealdir)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            var filter = Builders<TrainConfiguration>.Filter.Eq(x => x.TrainId, _trainId);

            // CRITICAL: cts.Token mutlaka buraya eklenmeli!
            var trainConfig = await _trainConfigs.Find(filter).FirstOrDefaultAsync(cts.Token);

            if (trainConfig != null)
            {
                _trainConfigCache[_trainId] = trainConfig;
                _logger.LogInformation("Konfigürasyon MongoDB'den başarıyla yüklendi: {TrainId}", _trainId);
            }
            else
            {
                _logger.LogWarning("MongoDB'de {TrainId} için kayıt bulunamadı.", _trainId);
            }

            return trainConfig;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("MongoDB sorgusu zaman aşımına uğradı (Timeout).");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB bağlantı hatası oluştu.");
            return null;
        }
    }

    // Sadece "YBS PC" donanımının IP'sini döndürür. 06.04.2026 Güncellendi

    public async Task<string?> GetYbsPcIpAsync()
    {
        var trainConfig = await GetTrainConfigurationAsync();

        if (trainConfig == null)
        {
            // DB kapalıysa veya konfig yoksa log kirliliğini önlemek için Debug basabiliriz
            _logger.LogError("GetYbsPcIpAsync: Tren konfigürasyonu alınamadı, default IP dönülüyor.");
            return "127.0.0.1";
        }

        var ybsPcHardware = trainConfig.Hardware?.FirstOrDefault(h => h.Name == "YBS PC");

        if (ybsPcHardware == null)
        {
            _logger.LogWarning("GetYbsPcIpAsync: 'YBS PC' donanımı konfigürasyonda bulunamadı.");
            return "127.0.0.1"; // Null yerine en azından local dönmek güvenlidir
        }

        return ybsPcHardware.ip;
    }


    //public async Task<Software> GetSoftwareByNameAsync(string name)
    //{

    //    if (_softwareCache.TryGetValue(name, out var cachedSoftware))
    //    {
    //        return cachedSoftware;
    //    }
    //    // ✅ 'using' ekleyerek CTS nesnesinin işi bittiğinde bellekten silinmesini sağladık.
    //    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)); // 500ms timeout
    //    try
    //    {
    //        var filter = Builders<TrainConfiguration>.Filter.ElemMatch(x => x.Software,
    //            Builders<Software>.Filter.Eq(s => s.Name, name));

    //        var config = await _trainConfigs.Find(filter).FirstOrDefaultAsync(cts.Token);
    //        return config?.Software?.FirstOrDefault(s => s.Name == name)!;
    //    }
    //    catch (Exception)
    //    {
    //        Console.WriteLine("Software bilgisi alınırken hata oluştu");
    //        throw;
    //    }
    //}

    //public async Task<TrainConfiguration> GetTrainConfigurationAsync()
    //{

    //    if (_trainConfigCache.TryGetValue(_trainId, out var cachedConfig))
    //    {
    //        return cachedConfig;
    //    }
    //    //  Varsayılan IP kullanılacaksa, hata durumunda uygulama çökmesin.
    //    TrainConfiguration? trainConfig = null;
    //    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)); // 100ms timeout
    //    try
    //    {
    //        var filter = Builders<TrainConfiguration>.Filter.Eq(x => x.TrainId, _trainId);
    //        trainConfig = await _trainConfigs.Find(filter).FirstOrDefaultAsync();
    //        if (trainConfig != null)
    //        {
    //            _trainConfigCache[_trainId] = trainConfig; // 📌 Cache'e ekliyoruz
    //        }

    //        return trainConfig!;
    //    }
    //    catch (Exception)
    //    {
    //        Console.ForegroundColor = ConsoleColor.Cyan;
    //        Console.WriteLine(">>> MongoDB'de bu Train ID için kayıt bulunamadı. !!! Baglantıları veya appsetting dosyasındaki TrainId kontrol ediniz");
    //        Console.ResetColor();
    //        //_logService?.ErrorSendLogAsync(new ErrorLogDto
    //        //{
    //        //    MessageSource = "LogicManager",
    //        //    MessageContent = "RabbitMQ Bağlantı hatası: 5 saniye sonra tekrar denenecek...",
    //        //    MessageType = LogType.Error.ToString(),
    //        //    DateTime = DateTime.Now,
    //        //    ErrorType = LogType.Error.ToString(),
    //        //    HardwareIP = "100.10.107.20"
    //        //});
    //       _logger.LogError("MongoDB'de bu Train ID için kayıt bulunamadı. !!! Baglantıları veya appsetting dosyasındaki TrainId kontrol ediniz");
    //    }
    //    return trainConfig!;
    //}


    //// Sadece "YBS PC" donanımının IP'sini döndürür.
    //public async Task<string?> GetYbsPcIpAsync()
    //{
    //    // 1. Tüm TrainConfiguration nesnesini önbellekten (cache) veya MongoDb'den çek
    //    var trainConfig = await GetTrainConfigurationAsync();

    //    if (trainConfig == null)
    //    {
    //        _logger.LogWarning("GetYbsPcIpAsync: Tren konfigürasyonu bulunamadı.");
    //        return "127.0.0.1"; // Veya bir varsayılan IP döndürün.
    //    }

    //    // 2. LINQ sorgusunu burada bir kez çalıştır
    //    var ybsPcHardware = trainConfig.Hardware?
    //                                   .FirstOrDefault(h => h.Name == "YBS PC");

    //    if (ybsPcHardware == null)
    //    {
    //        _logger.LogWarning("GetYbsPcIpAsync: 'YBS PC' donanımı konfigürasyonda bulunamadı.");
    //        return null;
    //    }

    //    // 3. Sonucu döndür
    //    return ybsPcHardware.ip;
    //}



    public async Task<TrainConfiguration> GetTrainConfigurationByTrainIdAsync(string trainId)
    {
        var filter = Builders<TrainConfiguration>.Filter.Eq(t => t.TrainId, trainId);
        return await _trainConfigs.Find(filter).FirstOrDefaultAsync();
    }

    public Task SetTrainId(string masterTrainId)
    {

        _trainId = masterTrainId;
        //Console.WriteLine("MongoDB Bağlantısı Başarılı" + _trainId);
        _logger.LogInformation("MongoDB Bağlantısı Başarılı" + _trainId);
        _trainConfigCache.Clear(); // Önbelleği temizle veya ilgili TrainId'yi kaldır.
        return Task.CompletedTask;

    }

}
