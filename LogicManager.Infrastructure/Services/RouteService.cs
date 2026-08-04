using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Shared;
using System.Collections.Concurrent;
using TRAM34_DDU.Core.Application.Interfaces.Services;
using TRAM34_DDU.Core.Application.RabbitMQService;

namespace LogicManager.Infrastructure.Services;

public class RouteService : IRouteService
{

    public List<Station> _stations = new List<Station>(); // Verileri saklayacağımız liste
    private readonly object _lock = new object();
    private readonly LoggerHelper? _logService;
    private readonly IRabbitService RabbitMQService;

    //private ConcurrentBag<Station> _stations = new ConcurrentBag<Station>();
    public event Action<List<Station>>? OnRouteUpdated; // Rota güncellendiğinde tetiklenecek event

    public RouteService(LoggerHelper logService,IRabbitService rabbitService)
    {
        _logService = logService;
        RabbitMQService = rabbitService;
        InitializeRabbitMQConsumer();
    }

    private async void InitializeRabbitMQConsumer()
    {
        await RabbitMQService.ConsumerAsync<List<Station>>(
                       RabbitMQConstants.RabbitMQHost,
                                  RabbitMQConstants.RotaExchangeName,
                                             ExchangeType.Fanout,
                                                        RabbitMQConstants.RotaQueueName,
                                                                   "",
                                                                   ManagementEnum.LastMessage,
                                                                              HandleNewRoute);

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

        await _logService.InformationSendLogAsync(new InformationLogDto
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


//using LogicManager.Domain.Entities;
//using LogicManager.Infrastructure.Interfaces;
//using LogicManager.Shared.DTOs;
//using LogicManager.Shared.Helpers;
//using Newtonsoft.Json;
//using RabbitMQ.Client;
//using RabbitMQ.Shared;
//using System.Collections.Concurrent;
//using TRAM34_DDU.Core.Application.RabbitMQService;
//using System.IO;

//namespace LogicManager.Infrastructure.Services;

//public class RouteService : IRouteService
//{
//    private List<Station> _stations = new List<Station>(); // Verileri saklayacağımız liste
//    private readonly object _lock = new object();
//    private readonly LoggerHelper? _logService;
//    private const string LastPositionFile = "last_position.json";

//    public event Action<List<Station>>? OnRouteUpdated; // Rota güncellendiğinde tetiklenecek event

//    public RouteService(LoggerHelper logService)
//    {
//        _logService = logService;
//        EnsureJsonFileExists(); // JSON dosyasını kontrol et ve oluştur
//        LoadLastPosition(); // JSON'dan en son istasyonu yükle
//        InitializeRabbitMQConsumer();
//    }

//    private async void InitializeRabbitMQConsumer()
//    {
//        await RabbitMQService.ConsumerAsync<List<Station>>(
//            RabbitMQConstants.RabbitMQHost,
//            RabbitMQConstants.RotaExchangeName,
//            ExchangeType.Fanout,
//            RabbitMQConstants.RotaQueueName,
//            "",
//            ManagementEnum.LastMessage,
//            HandleNewRoute);
//    }

//    private async Task HandleNewRoute(List<Station> stationList)
//    {
//        lock (_lock)
//        {
//            _stations = new List<Station>(stationList);
//        }

//        int lastStationId = GetLastStationFromJson();
//        if (lastStationId != -1)
//        {
//            var lastStationIndex = _stations.FindIndex(s => s.stationSequenceId == lastStationId);
//            if (lastStationIndex != -1)
//            {
//                _stations = _stations.Skip(lastStationIndex).ToList(); // Kaldığı yerden devam et
//            }
//        }

//        OnRouteUpdated?.Invoke(_stations);


//        string logMessage = _stations.Count > 0
//            ? $"Yeni rota alındı, kaldığı yerden devam ediyor: {_stations.Count} istasyon"
//            : "Rota iptal edildi.";

//        await _logService?.InformationSendLogAsync(new InformationLogDto
//        {
//            MessageSource = "LogicManager",
//            MessageContent = logMessage,
//            MessageType = LogType.Information.ToString(),
//            DateTime = DateTime.Now
//        });
//    }

//    public Task<List<Station>> GetAllRouteAsync()
//    {
//        lock (_lock)
//        {
//            return Task.FromResult(new List<Station>(_stations));
//        }
//    }

//    public Task<bool> IsRouteEstablishedAsync()
//    {
//        lock (_lock)
//        {
//            return Task.FromResult(_stations.Count > 0);
//        }
//    }

//    public void SaveLastStationToJson(int stationId)
//    {
//        var data = new { LastStationId = stationId };
//        File.WriteAllText(LastPositionFile, JsonConvert.SerializeObject(data));
//    }

//    public int GetLastStationFromJson()
//    {
//        if (!File.Exists(LastPositionFile)) return -1;

//        var json = File.ReadAllText(LastPositionFile);
//        if (!string.IsNullOrWhiteSpace(json))
//        {
//            try
//            {
//                var data = JsonConvert.DeserializeObject<dynamic>(json);
//                return data?.LastStationId ?? -1;
//            }
//            catch (JsonException)
//            {
//                return -1; // JSON bozuksa -1 döndür
//            }
//        }
//        return -1; // Veri yoksa
//    }

//    private void LoadLastPosition()
//    {
//        int lastStationId = GetLastStationFromJson();
//        if (lastStationId != -1)
//        {
//            Console.WriteLine($"Sistem açıldı, en son {lastStationId} istasyonundaydınız.");
//        }
//    }

//    private void EnsureJsonFileExists()
//    {
//        if (!File.Exists(LastPositionFile))
//        {
//            File.WriteAllText(LastPositionFile, JsonConvert.SerializeObject(new { LastStationId = -1 }));
//        }
//    }
//}




