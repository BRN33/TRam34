using LogicManager.Infrastructure.Interfaces;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Shared;
using RabbitMQ.Client;

namespace LogicManager.Infrastructure.Services;

public class LedService : ILedService
{

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() }, // Enum'ları string olarak serileştir
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase // JSON'daki property isimlerini küçük harfle başlat
    };
    public async Task UpdateDisplay(LedDisplayType displayType, string stationName)
    {

        // JSON formatında birleştirme
        var message = new
        {
            ledType = "Station",
            ledMessageType = displayType,
            StationName = stationName
        };

        string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("RabbitMQ ye giden Led : " + jsonMessage);
        Console.ResetColor();

         RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.LedExchangeName, ExchangeType.Fanout, "", jsonMessage);

        //await RabbitMQHelperAsync.SendMessageToExchangeAsync(RabbitMQConstants.LedExchangeName, jsonMessage);

    }
}
