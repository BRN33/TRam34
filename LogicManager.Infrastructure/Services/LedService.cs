using LogicManager.Infrastructure.Interfaces;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Shared;
using RabbitMQ.Client;
using TRAM34_DDU.Core.Application.RabbitMQService;
using LogicManager.Shared.Helpers;
using LogicManager.Shared.DTOs;
using LogicManager.Persistence.Interfaces;
using TRAM34_DDU.Core.Application.Interfaces.Services;

namespace LogicManager.Infrastructure.Services;

public class LedService : ILedService
{
    private readonly LoggerHelper _logService;
    private readonly IMongoDbService _mongoDbService;
    private readonly IRabbitService _rabbitService;

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() }, // Enum'ları string olarak serileştir
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase // JSON'daki property isimlerini küçük harfle başlat
    };

    // Bu, sınıfınızın test edilebilirliğini artırır ve bağımlılığı dışarıdan yönetir.
    public LedService(LoggerHelper logService, IMongoDbService mongoDbService,IRabbitService rabbitService)
    {
        _logService = logService;
        _mongoDbService = mongoDbService;
        _rabbitService = rabbitService;
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


        // Log detaylarını tek bir yerden yönetiyoruz.
        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = messageContent, // Dinamik içerik buraya gelecek
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = ybsPcIp, // Bu IP'ler sabitse, burada kalabilir.
            DestinationIP = ybsPcIp,
            DestinationName = "LedController"
        });

    }


    public async Task UpdateDisplay(LedDisplayType displayType, string stationName, bool isExternal = false)
    {

        // JSON formatında birleştirme
        var message = new
        {
            ledType = "Station", 
            ledCatType = isExternal ? "ExternalLed" : "InternalLed",
            ledMessageType = displayType,
            StationName = stationName
        };

        string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("RabbitMQ ye giden Led : " + jsonMessage);
        Console.ResetColor();

         //RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.LedExchangeName, ExchangeType.Fanout, "", jsonMessage);

        //await RabbitMQHelperAsync.SendMessageToExchangeAsync(RabbitMQConstants.LedExchangeName, jsonMessage);
        await _rabbitService.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.LedExchangeName, ExchangeType.Fanout, "", jsonMessage, ManagementEnum.Live);

        await SendLogAsync($" İstasyon Bilgisi LED tabelalara gönderildi: {stationName}");

    }
}
