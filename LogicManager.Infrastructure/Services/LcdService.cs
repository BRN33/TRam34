using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using RabbitMQ.Shared;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Client;

namespace LogicManager.Infrastructure.Services;

public class LcdService : ILcdService
{

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() }, // Enum'ları string olarak serileştir
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase // JSON'daki property isimlerini küçük harfle başlat
    };
    public async Task UpdateDisplay(LcdInfo displayInfo)
    {

        // JSON formatında birleştirme
        var message = new
        {
            stationName = displayInfo.NextStation,
            remainingDistance = displayInfo.RemainingDistance
        };

        string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("RabbitMQ ye giden DDU ve Stretch_LCD_Bilgisi : " + jsonMessage);
        Console.ResetColor();
        await RabbitMQHelperAsync.PublishMessageAsync(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);

    }
}
