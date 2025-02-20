using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Shared;

namespace LogicManager.Infrastructure.Services;

public class RouteService : IRouteService
{
    private bool _isRabbitConsumerInitialized = false;  // Consumer'ın zaten başlatıldığını kontrol etmek için
    public List<Station> _stations = new List<Station>(); // Verileri saklayacağımız liste
    private readonly object _lock = new object();
    private readonly LoggerHelper? _logService;

    // 📢 **Event Tanımı**: Rota değiştiğinde tetiklenecek
    public event Action<List<Station>>? OnRouteUpdated;

    public RouteService(LoggerHelper logService)
    {
        _logService = logService;
        InitializeRabbitMQConsumer();
    }

    private void InitializeRabbitMQConsumer()
    {
        Task.Run(async () =>
        {
            await RabbitMQHelperAsync.ConsumeMessageAsync<List<Station>>(
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
        if (stationList != null && stationList.Any())
        {
            lock (_lock)
            {
                _stations.Clear();
                _stations.AddRange(stationList);
            }

            // Event'i tetikle
            OnRouteUpdated?.Invoke(stationList);

            await _logService.InformationSendLogAsync(new InformationLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"Yeni rota alındı: {stationList.Count} istasyon",
                MessageType = LogType.Information.ToString(),
                DateTime = DateTime.Now
            });
        }
    }


    public async Task<List<Station>> GetAllRouteAsync()
    {
        lock (_lock)
        {
            return new List<Station>(_stations);
        }
    }

    public async Task<bool> IsRouteEstablishedAsync()
    {
        lock (_lock)
        {
            return _stations != null && _stations.Any();
        }
    }

    //public async Task<List<Station>> GetAllRouteAsync()
    //{
    //    lock (_lock)
    //    {

    //        // Eğer liste daha önce dolduysa tekrar okumaya gerek yok
    //        if (_stations.Any())
    //        {
    //            return _stations;
    //        }
    //    }

    //    _stations.Clear();  // Önce eski verileri temizle
    //    await RabbitMQHelperAsync.ConsumeMessageAsync<List<Station>>(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.RotaExchangeName, ExchangeType.Fanout, RabbitMQConstants.RotaQueueName, "", async (stationList) =>
    //    {
    //        if (stationList != null && stationList.Any())
    //        {
    //            _stations.Clear();  // Önce eski verileri temizle
    //            _stations.AddRange(stationList);
    //            Console.WriteLine("Geliyorrrr listeeee: " + _stations.First());
    //            Console.WriteLine($"Rota Bilgisi Eklendi: {_stations.Count} istasyon yüklendi.");

    //            //**Event'i tetikle**: Yeni rota geldiğinde dinleyicilere haber ver
    //            OnRouteUpdated?.Invoke(_stations);
    //            //// **Eski veriyi silmeden önce yeni verinin dolmasını bekle**
    //            //var tempStations = new List<Station>(stationList);  // Yeni gelen veriyi geçici listede tut
    //            //if (tempStations.Any())
    //            //{
    //            //    _stations.Clear();  // Eski veriyi temizle
    //            //    _stations.AddRange(tempStations);  // Yeni veriyi ekle
    //            //}

    //        }
    //    });

    //    return _stations;

    //}


    //// Rota dolu mu boş mu kontrolü
    //public async Task<bool> IsRouteEstablishedAsync()
    //{
    //    try
    //    {
            
    //        var response = await GetAllRouteAsync();

    //        // 🚨 Eğer yeni rota listesi boşsa, rota kurulmamıştır.
    //        if (response == null || !response.Any())
    //        {
    //            Console.WriteLine("--- > > > Henüz yeni rota gelmedi. Bekleniyor >>>...");
    //            return false;
    //        }

    //        //// ✅ Eğer daha önce bir rota varsa, eski rotayı temizleyelim.
    //        //if (_stations.Any())
    //        //{
    //        //    Console.WriteLine("🔄 Yeni rota bulundu! Önceki rota temizleniyor...");
    //        //    _stations.Clear();
    //        //}


    //        //Console.WriteLine("✅ Yeni rota bulundu ve yüklendi!");
    //        //_stations = response;  // Yeni rotayı yükle

    //        return true;






    //        //if (stationList != null && stationList.Any())
    //        //{
    //        //    Console.WriteLine($"🚆 Yeni rota alındı! {stationList.Count} istasyon yüklendi.");

    //        //    // **Eski veriyi temizlemeden önce yeni rotanın gerçekten farklı olup olmadığını kontrol et**
    //        //    if (!_stations.SequenceEqual(stationList, new StationComparer()))
    //        //    {
    //        //        _stations = new List<Station>(stationList); // 🔄 Yeni referans ata
    //        //        Console.WriteLine("🔄 Rota güncellendi.");
    //        //    }
    //        //    else
    //        //    {
    //        //        Console.WriteLine("✅ Rota zaten aynı, güncelleme yapılmadı.");
    //        //    }
    //        //}







    //    }
    //    catch (Exception)
    //    {
            
    //        Console.WriteLine($"🚨 Rota kontrol hatası:");
    //        await _logService.ErrorSendLogAsync(new ErrorLogDto
    //        {
    //            MessageSource = "LogicManager",
    //            MessageContent = "Rota bilgisi gelmedi veya baglantı yok...",
    //            MessageType = LogType.Error.ToString(),
    //            DateTime = DateTime.Now,
    //            ErrorType = LogType.Error.ToString(),
    //            HardwareIP = "10.3.156.55"
    //        });
    //        return false;
    //    }
    //}

}
