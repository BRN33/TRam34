using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using RabbitMQ.Shared;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Client;
using TRAM34_DDU.Core.Application.RabbitMQService;
using LogicManager.Shared.Helpers;
using LogicManager.Shared.DTOs;
using LogicManager.Persistence.Interfaces;
using Microsoft.Extensions.Configuration;
using TRAM34_DDU.Core.Application.Interfaces.Services;

namespace LogicManager.Infrastructure.Services;

public class LcdService : ILcdService
{
    private readonly LoggerHelper _logService;
    private readonly IConfiguration _configuration;
    private readonly IRabbitService _rabbitService;

    private readonly IMongoDbService _mongoDbService;


    private double _lastLoggedRemainingDistance = -1; // Son kalan loglanan mesafe
    private double _lastLoggedTotalDistance = -1;// Son loglanan toplam mesafe
    private DateTime _lastLogTime = DateTime.MinValue; // Son log zamanı
    private int LogIntervalSeconds; // Değişim yoksa 30 saniyede bir logla

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() }, // Enum'ları string olarak serileştir
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase // JSON'daki property isimlerini küçük harfle başlat
    };

    // Bu, sınıfınızın test edilebilirliğini artırır ve bağımlılığı dışarıdan yönetir.
    public LcdService(LoggerHelper logService, IMongoDbService mongoDbService,IRabbitService rabbitService)
    {
        _logService = logService;
        _mongoDbService = mongoDbService;
       
        _rabbitService = rabbitService;

        //var intervalValue = _configuration["LogAliveStatus:LogIntervalSecondsDistance"];
        //LogIntervalSeconds = !string.IsNullOrEmpty(intervalValue) ? Convert.ToInt32(intervalValue) : 30;
        
    }

    private async Task SendLogAsync(string messageContent)
    {
        // currentTime sorununu aşmak için DateTime.Now'ı doğrudan burada kullanabilirsiniz.
        var currentTime = DateTime.Now;

        // Loglama işlemini sadece tek bir yerden yapıyoruz.

        //var trainConfig = await _mongoDbService.GetTrainConfigurationAsync();

        //var sourceIp = trainConfig.Hardware?.FirstOrDefault(h => h.Name == "YBS PC")?.ip;
        //var destinationIp = trainConfig.Hardware?.FirstOrDefault(s => s.Name == "YBS PC")?.ip;


        string? ybsPcIp = await _mongoDbService.GetYbsPcIpAsync();

        // Log detaylarını StretchLCD ye gönderiyoruz.
        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = messageContent, // Dinamik içerik buraya gelecek
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = ybsPcIp, // Bu IP'ler sabitse, burada kalabilir.
            DestinationIP = ybsPcIp,
            DestinationName = "StretchController"
        });

        // Log detaylarını DDU ya gönderiyoruz.
        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = messageContent, // Dinamik içerik buraya gelecek
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = ybsPcIp, // Bu IP'ler sabitse, burada kalabilir.
            DestinationIP = ybsPcIp,
            DestinationName = "HMIController"
        });

    }


    public async Task UpdateDisplay(LcdInfo displayInfo)
    {
        //string stationName = displayInfo.NextStation!;
        // JSON formatında birleştirme
        var message = new
        {
            stationName = displayInfo.NextStation,
            //remainingDistance = displayInfo.RemainingDistance
        };

        string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("RabbitMQ ye giden DDU ve Stretch_LCD_Bilgisi : " + message);
        Console.ResetColor();
        //RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);
        //await RabbitMQHelperAsync.PublishMessageAsync(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);
        await _rabbitService.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage, ManagementEnum.Live);

        await SendLogAsync($"440 - Sonraki İstasyon Bilgisi HMIController ve StretchController'a gönderildi: {displayInfo.NextStation}");
    }



    public async Task UpdateDistance(LcdInfo displayInfo)
    {
        //var remainingDistance = displayInfo.RemainingDistance;

        // Değişim kontrolü: Mevcut veri son loglanandan farklı mı?
        bool isDistanceChanged = displayInfo.RemainingDistance != _lastLoggedRemainingDistance ||
                                 displayInfo.TotalDistance != _lastLoggedTotalDistance;

        // Zaman kontrolü: Son log üzerinden belirlenen süre geçti mi? (Heartbeat)
        bool isTimeElapsed = (DateTime.Now - _lastLogTime).TotalSeconds >= LogIntervalSeconds;




        // JSON formatında birleştirme
        var message = new
        {

            remainingDistance = displayInfo.RemainingDistance,
            totalDistance = displayInfo.TotalDistance

        };

        string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);


        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("RabbitMQ ye giden Kalan Mesafe Bilgisi------------ : " + jsonMessage);
        Console.ResetColor();
        //RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.DistanceInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);
        //await RabbitMQHelperAsync.PublishMessageAsync(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);
        await _rabbitService.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.DistanceInfoExchangeName, ExchangeType.Fanout, "", jsonMessage, ManagementEnum.Live);

        string guid8 = Guid.NewGuid().ToString("N").Substring(0, 8);


        await SendLogAsync($"[{guid8}]"+ $" : 438 - Kalan Mesafe ve Toplam Mesafe HMIController ve StretchController'a gönderildi :  {jsonMessage}");

        //// LOGLAMA SADECE DEĞİŞİM VARSA VEYA ZAMAN DOLDUYSA YAPILIR
        //if (isDistanceChanged || isTimeElapsed)
        //{

        //    await SendLogAsync($"Kalan Mesafe ve Toplam Mesafe DDU ve Stretch LCD'ye gönderildi +{jsonMessage}");

        //    // Durumu güncelle
        //    _lastLoggedRemainingDistance = displayInfo.RemainingDistance;
        //    _lastLoggedTotalDistance = displayInfo.TotalDistance;
        //    _lastLogTime = DateTime.Now;
        //}

    }
}
