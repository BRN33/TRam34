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
         RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);
        //await RabbitMQHelperAsync.PublishMessageAsync(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);

    }

    public async Task UpdateDistance(LcdInfo displayInfo)
    {
        //var remainingDistance = displayInfo.RemainingDistance;

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
        RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.DistanceInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);
        //await RabbitMQHelperAsync.PublishMessageAsync(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", jsonMessage);
    }
}
