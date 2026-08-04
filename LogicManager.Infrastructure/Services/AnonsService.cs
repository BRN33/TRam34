//using LogicManager.Infrastructure.Interfaces;
//using RabbitMQ.Shared;
//using System.Text.Json.Serialization;
//using System.Text.Json;
//using RabbitMQ.Client;
//using TRAM34_DDU.Core.Application.RabbitMQService;
//using LogicManager.Shared.Helpers;
//using LogicManager.Shared.DTOs;
//using LogicManager.Persistence.Interfaces;
//using TRAM34_DDU.Core.Application.Interfaces.Services;


//namespace LogicManager.Infrastructure.Services;

//public class AnonsService : IAnonsService
//{

//    private readonly LoggerHelper _logService;

//    private readonly IMongoDbService _mongoDbService;
//    private readonly IRabbitService _rabbitService;


//    private readonly string? _sipServerIp; // Sınıf düzeyinde önbellek

//    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
//    {
//        Converters = { new JsonStringEnumConverter() }, // Enum'ları string olarak serileştir
//        PropertyNamingPolicy = JsonNamingPolicy.CamelCase // JSON'daki property isimlerini küçük harfle başlat
//    };

//    // Constructor Injection: ILogService bağımlılığını enjekte edin
//    public AnonsService(LoggerHelper logService, IMongoDbService mongoDbService,IRabbitService rabbitService)
//    {
//        _logService = logService;
//        _mongoDbService = mongoDbService;
//        _rabbitService = rabbitService;

//        var sipConfig = mongoDbService.GetSoftwareByNameAsync("SIPServer").GetAwaiter().GetResult();
//        _sipServerIp = sipConfig?.ip; // Null kontrolü yapıldı
//    }


//    // *** Yardımcı Log Metodu ***
//    private async Task SendEventLogAsync(string messageContent)
//    {
//        // currentTime sorununu aşmak için DateTime.Now'ı doğrudan burada kullanıyoruz.
//        var currentTime = DateTime.Now;

//        //var trainConfig = await _mongoDbService.GetTrainConfigurationAsync();

//        //var sourceIp = trainConfig.Software?.FirstOrDefault(h => h.Name == "SIP Server")?.ip;
//        //var destinationIp = trainConfig.Software?.FirstOrDefault(s => s.Name == "SIP Server")?.ip;
//        //// ✅ IP adresini burada kontrol ediyoruz. Yoksa asenkron çekiyoruz.
//        //if (string.IsNullOrEmpty(_sipServerIp))
//        //{
//        //    var sipConfig = await _mongoDbService.GetSoftwareByNameAsync("SIPServer");
//        //    _sipServerIp = sipConfig?.ip ?? "0.0.0.0"; // Bulamazsa varsayılan ata
//        //}

//        // Log detaylarını tek bir yerden yönetiyoruz.
//        await _logService.EventSendLogAsync(new EventLogDto
//        {
//            MessageSource = "LogicManager",
//            MessageContent = messageContent, // Dinamik içerik buraya gelecek
//            MessageType = LogType.Event.ToString(),
//            DateTime = currentTime,
//            SourceIP = _sipServerIp, // Bu IP'ler sabitse, burada kalabilir.
//            DestinationIP = _sipServerIp,
//            DestinationName = "AnnouncementController"
//        });
//    }

//    // *** AnnouncementType'a Göre Log Mesajını Oluşturan Metot ***
//    private string GetAnnouncementLogMessage(AnnouncementType type, string stationName, string destinationName)
//    {
//        // Anons tipine göre log mesaj içeriğini belirliyoruz (Pattern Matching)
//        return type switch
//        {
//            AnnouncementType.Start => $"442 - Başlangıç Anonsu yapıldı. Hedef: {destinationName}",
//            AnnouncementType.Arrival => $"437 - Varış Anonsu yapıldı. İstasyon: {stationName}",
//            AnnouncementType.Approaching => $"439 - Yaklaşım Anonsu yapıldı. İstasyon: {stationName}",
//            AnnouncementType.Special => $"Özel Anons yapıldı. İçerik bilgisi: '{stationName}'",
//            AnnouncementType.Terminal => $"443 - Terminal Anonsu yapıldı. Terminal: {destinationName}",
//            AnnouncementType.Transfer => $"444 - Transfer Anonsu yapıldı. Transfer İstasyonu: {stationName}",
//            _ => $"Bilinmeyen Anons Tipi ({type}) yapıldı."
//        };
//    }


//    public async Task PlayAnnouncementAsync(AnnouncementType type, string stationName, string destinationName)
//    {
//        try
//        {

//            // JSON formatında birleştirme
//            var message = new
//            {
//                Type = type,
//                StationName = stationName,
//                Destination = destinationName
//            };


//            string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);

//            Console.WriteLine("RabbitMQ ye giden Anons : " + jsonMessage);
//            //RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.AnnounceExchangeName, ExchangeType.Fanout, "", jsonMessage);

//            //await  RabbitMQHelperAsync.SendMessageToExchangeAsync(RabbitMQConstants.AnnounceExchangeName, jsonMessage);

//            await _rabbitService.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.AnnounceExchangeName, ExchangeType.Fanout, "", jsonMessage, ManagementEnum.Live);


//            // 3. Log mesajını dinamik olarak oluşturma
//            string logContent = GetAnnouncementLogMessage(type, stationName, destinationName);

//            // 4. Log servisine gönderme
//            await SendEventLogAsync(logContent);
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine("Anons servisine veri gönderilemedi.");
//            var currentTime = DateTime.Now;
//            await _logService.ErrorSendLogAsync(new ErrorLogDto
//            {
//                MessageSource = "LogicManager",
//                MessageContent = $"460 - AnnouncementController servisine veri gönderilemedi: {ex.Message}",
//                MessageType = LogType.Error.ToString(),
//                DateTime = currentTime,
//                MessageSourceType = "Software",
//                HardwareIP = _sipServerIp ?? "Unknown"
//            });

//        }

//    }
//}




using LogicManager.Infrastructure.Interfaces;
using RabbitMQ.Shared;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Client;
using TRAM34_DDU.Core.Application.RabbitMQService;
using LogicManager.Shared.Helpers;
using LogicManager.Shared.DTOs;
using LogicManager.Persistence.Interfaces;
using TRAM34_DDU.Core.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace LogicManager.Infrastructure.Services;

public class AnonsService : IAnonsService
{
    private readonly LoggerHelper _logService;
    private readonly IMongoDbService _mongoDbService;
    private readonly IRabbitService _rabbitService;

    private string? _sipServerIp;
    // Aynı anda birden fazla anons gelirse DB'ye hücum etmemesi için kilit mekanizması
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AnonsService(LoggerHelper logService, IMongoDbService mongoDbService, IRabbitService rabbitService)
    {
        _logService = logService;
        _mongoDbService = mongoDbService;
        _rabbitService = rabbitService;

        // Constructor içinde GetResult() KALDIRILDI. 
        // Uygulama artık açılışta MongoDB'yi beklemez, Docker crash yemez.
    }

    // IP adresini güvenli ve asenkron şekilde getiren yardımcı metot
    private async Task<string> GetSipIpAsync()
    {
        if (!string.IsNullOrEmpty(_sipServerIp))
            return _sipServerIp;

        await _semaphore.WaitAsync();
        try
        {
            // Kilidi aldıktan sonra tekrar kontrol et (Double-check locking)
            if (string.IsNullOrEmpty(_sipServerIp))
            {
                var sipConfig = await _mongoDbService.GetSoftwareByNameAsync("SIPServer");
                _sipServerIp = sipConfig?.ip ?? "0.0.0.0"; // Bulamazsa default ata ama uygulama çökmesin
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return _sipServerIp;
    }

    private async Task SendEventLogAsync(string messageContent)
    {
        var currentTime = DateTime.Now;
        // IP'yi lazım olduğu an (on-demand) alıyoruz
        var currentIp = await GetSipIpAsync();

        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = messageContent,
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = currentIp,
            DestinationIP = currentIp,
            DestinationName = "AnnouncementController"
        });
    }

    private string GetAnnouncementLogMessage(AnnouncementType type, string stationName, string destinationName)
    {
        return type switch
        {
            AnnouncementType.Start => $"442 - Başlangıç Anonsu yapıldı. Hedef: {destinationName}",
            AnnouncementType.Arrival => $"437 - Varış Anonsu yapıldı. İstasyon: {stationName}",
            AnnouncementType.Approaching => $"439 - Yaklaşım Anonsu yapıldı. İstasyon: {stationName}",
            AnnouncementType.Special => $"Özel Anons yapıldı. İçerik bilgisi: '{stationName}'",
            AnnouncementType.Terminal => $"443 - Terminal Anonsu yapıldı. Terminal: {destinationName}",
            AnnouncementType.Transfer => $"444 - Transfer Anonsu yapıldı. Transfer İstasyonu: {stationName}",
            _ => $"Bilinmeyen Anons Tipi ({type}) yapıldı."
        };
    }

    public async Task PlayAnnouncementAsync(AnnouncementType type, string stationName, string destinationName)
    {
        try
        {
            var message = new
            {
                Type = type,
                StationName = stationName,
                Destination = destinationName
            };

            string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);

            await _rabbitService.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.AnnounceExchangeName, ExchangeType.Fanout, "", jsonMessage, ManagementEnum.Live);

            string logContent = GetAnnouncementLogMessage(type, stationName, destinationName);
            await SendEventLogAsync(logContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Anons hatası: {ex.Message}");
            var currentIp = await GetSipIpAsync(); // Hata logu için de güvenli IP alımı

            await _logService.ErrorSendLogAsync(new ErrorLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"460 - AnnouncementController servisine veri gönderilemedi: {ex.Message}",
                MessageType = LogType.Error.ToString(),
                DateTime = DateTime.Now,
                MessageSourceType = "Software",
                HardwareIP = currentIp
            });
        }
    }
}
