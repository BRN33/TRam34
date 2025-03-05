using LogicManager.Infrastructure.Interfaces;
using RabbitMQ.Shared;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Client;


namespace LogicManager.Infrastructure.Services;

public class AnonsService : IAnonsService
{


    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() }, // Enum'ları string olarak serileştir
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase // JSON'daki property isimlerini küçük harfle başlat
    };
    public async Task PlayAnnouncementAsync(AnnouncementType type, string stationName, string destinationName)
    {


        // JSON formatında birleştirme
        var message = new
        {
            Type = type,
            StationName = stationName,
            Destination = destinationName
        };


        string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);

        Console.WriteLine("RabbitMQ ye giden Anons : " + jsonMessage);
        RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.AnnounceExchangeName, ExchangeType.Fanout, "", jsonMessage);

        //await  RabbitMQHelperAsync.SendMessageToExchangeAsync(RabbitMQConstants.AnnounceExchangeName, jsonMessage);

    }
}
