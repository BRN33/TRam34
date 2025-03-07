using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Shared;
using System.Collections.Concurrent;

namespace LogicManager.Infrastructure.Services;

public class RouteService : IRouteService
{
    
    public List<Station> _stations = new List<Station>(); // Verileri saklayacağımız liste
    private readonly object _lock = new object();
    private readonly LoggerHelper? _logService;

    //private ConcurrentBag<Station> _stations = new ConcurrentBag<Station>();
    public event Action<List<Station>>? OnRouteUpdated; // Rota güncellendiğinde tetiklenecek event

    public RouteService(LoggerHelper logService)
    {
        _logService = logService;
        InitializeRabbitMQConsumer();
    }

    private void InitializeRabbitMQConsumer()
    {
        Task.Run(() =>
        {
            RabbitMQHelper.ConsumeMessage<List<Station>>(
                RabbitMQConstants.RabbitMQHost,
                RabbitMQConstants.RotaExchangeName,
                ExchangeType.Fanout,
                RabbitMQConstants.RotaQueueName,
                "",
                HandleNewRoute);
        });
    }

    private async Task HandleNewRoute(List<Station> stationList)
    {
        lock (_lock)
        {
            _stations = new List<Station>(stationList); // Gelen liste neyse onu al
        }

        OnRouteUpdated?.Invoke(stationList); // Güncellenmiş rotayı bildirim olarak gönder

        string logMessage = stationList.Count > 0
            ? $"Yeni rota alındı: {stationList.Count} istasyon"
            : "Rota iptal edildi."; // Eğer liste boşsa rota iptal edilmiş demektir.

        await _logService?.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = logMessage,
            MessageType = LogType.Information.ToString(),
            DateTime = DateTime.Now
        });
    }


    public Task<List<Station>> GetAllRouteAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(new List<Station>(_stations));
        }
    }

    public Task<bool> IsRouteEstablishedAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_stations.Count > 0);
        }
    }

}
