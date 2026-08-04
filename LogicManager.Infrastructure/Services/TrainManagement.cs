using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using LogicManager.Shared.Helpers;
using LogicManager.Shared.DTOs;
using RabbitMQ.Shared;
using RabbitMQ.Client;
using LogicManager.Persistence.Interfaces;
using Microsoft.Extensions.Configuration;
using TRAM34_DDU.Core.Application.RabbitMQService;
using Newtonsoft.Json;
using LogicManager.Persistence.Services;
using LogicManager.Domain.Services;
using TRAM34_DDU.Core.Application.Interfaces.Services;

namespace LogicManager.Infrastructure.Services;

public class TrainManagement : ITrainManagement
{

    // Degisken Tanımlamaları

    private readonly IServiceScope _serviceProvider; //Dependency Injection yapma için 

    private readonly ITcmsService _tcmsService;
    private readonly ITrainCoupledService _trainCoupledService;
    private readonly IAnonsService _anonsService;
    private readonly ILedService _ledService;
    private readonly ILcdService _lcdService;
    private readonly ITakoReaderService _takoReaderService;
    private readonly IRouteService _routeService;
    private readonly IMongoDbService _mongoDbService;
    private readonly IRabbitService _rabbitService;
    private readonly ISyncManager _syncManager;
    private readonly LoggerHelper _logService;
    public List<Station> _stations;
    private const string LastPositionFile = "last_position.json";

    private const int TAKO_DISTANCE_FACTOR = 5; // Her tako pulse için mesafe çarpanı

    public int _currentStationIndex;
    private object _currentDistance;
    public int _TachoMeterPulse;
    public bool ZeroSpeed;
    public bool AllDoorReleased;
    private bool _isRouteActive;
    public bool IsRouteActive
    {
        get => _isRouteActive;
        private set => _isRouteActive = value;
    }
    private bool _routeCompleted;

    public DateTime currentTime => DateTime.Now; // Güncel zamanı döndüren özellik

    private TrainSyncMessage? _lastSyncData;//Tren kaldıgı yerden devam etmesi icin
    private DateTime _lastSyncedTime = DateTime.MinValue;


    public bool _hasStartAnnouncementPlayed;//Baslangıc Anonsu bir kere göndermek icin
    private bool _approachingAnnouncementMade = false;//Anonsu bir kere göndermek icin
    private bool _arrivalAnnouncementMade = false;//Anonsu bir kere göndermek icin
    private bool _terminalAnnouncementMade = false;//Terminal Anonsu bir kere göndermek icin

    private bool _isFirstStationInitialized = false;
    private bool _nextStationDisplayed = false;
    private int istasyondanCıkısMesafesi;

    private readonly string? _ybsPcIp;//YBS PC IP adresi


    //public event Action<TcmsData>? OnTakoDataUpdated; // Güncellenen veriyi bildirmek için event

    public TrainManagement(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider.CreateScope();
        _mongoDbService = _serviceProvider.ServiceProvider.GetRequiredService<IMongoDbService>();
        _anonsService = _serviceProvider.ServiceProvider.GetRequiredService<IAnonsService>();
        _ledService = _serviceProvider.ServiceProvider.GetRequiredService<ILedService>();
        _lcdService = _serviceProvider.ServiceProvider.GetRequiredService<ILcdService>();
        _takoReaderService = _serviceProvider.ServiceProvider.GetRequiredService<ITakoReaderService>();
        _routeService = _serviceProvider.ServiceProvider.GetRequiredService<IRouteService>();
        _logService = _serviceProvider.ServiceProvider.GetRequiredService<LoggerHelper>();
        _tcmsService = _serviceProvider.ServiceProvider.GetRequiredService<ITcmsService>();
        _trainCoupledService = _serviceProvider.ServiceProvider.GetRequiredService<ITrainCoupledService>();
        _rabbitService = _serviceProvider.ServiceProvider.GetRequiredService<IRabbitService>();
        _syncManager = _serviceProvider.ServiceProvider.GetRequiredService<ISyncManager>();
        istasyondanCıkısMesafesi = Convert.ToInt32(configuration["TcmsSettings:istasyondanCıkısMesafesi"]);
        _stations = new List<Station>();

        _ybsPcIp = _mongoDbService.GetYbsPcIpAsync().GetAwaiter().GetResult();

        //StartSyncListenerAsync(); // 1 . programın kapanıp tekrar acılması durumunda rotanın devam etmesi icin yazıldı .  Acılabilir

        _routeService.OnRouteUpdated += async (routeData) =>
        {  
            await StartTakoProcessing(routeData);
        };

        _tcmsService.OnTakoDataUpdated += (takoData) =>
        {
            ZeroSpeed = takoData.ZeroSpeed;
            AllDoorReleased = takoData.Doors.AllDoorReleased;
            //OnTakoDataUpdated?.Invoke(takoData);
        };
        //_tcmsService.OnTakoDataUpdated += _tcmsService_OnTakoDataUpdated;

        //_takoReaderService.TakoVerisiOkundu +=async (takoValue) => TakoVerisiOkunduHandler(takoValue);


        _ = StartConsuming(_mongoDbService);

        // ✅ Sync listener başlat
        _ = StartSyncListenerAsync();
        // ✅ Sync isteği gönder (program açıldığında diğer trenlerden state ister)
        _ = RequestSyncFromOtherTrainsAsync();

        _ = StartHeartbeatAsync();

    }

    public async Task StartSyncListenerAsync()
    {
        await _rabbitService.ConsumerAsync<TrainSyncMessage>(
            RabbitMQConstants.RabbitMQHost,
            RabbitMQConstants.ContiniueSyncRotaExchangeName,
            ExchangeType.Fanout,
            RabbitMQConstants.ContiniueSyncRotaQueueName,
            "",
            ManagementEnum.LastMessage,
            HandleSyncMessage);
        Console.WriteLine("Sync mesajı dinleme başlatıldı."/* + _lastSyncData!.NextStation, _lastSyncData.RemainingDistance*/);
    }


    // Örnek kullanım (RabbitMQService'i kullanan sınıfınızda)
    public async Task StartConsuming(IMongoDbService mongoDbService)
    {
        await _rabbitService.ConsumerAsync<TrainCouplingData>(
            RabbitMQConstants.RabbitMQHost,
            RabbitMQConstants.CoupledTrainsExchangeName,
            "fanout",
            RabbitMQConstants.CoupledTrainsQueueName,
            "",
            ManagementEnum.LastMessage,
            async (message) =>
            {
                mongoDbService?.SetTrainId(message.CurrentTrain.ID);
                // Gerekli diğer işlemler...
                // ✅ Bağlantı kopup tekrar geldi → Sync iste
                await RequestSyncFromOtherTrainsAsync();
            });
    }

    // 🔄 Sync request gönder
    public async Task RequestSyncFromOtherTrainsAsync()
    {
        var tren = await _trainCoupledService.GetLastTrainData();
        if (tren == null) return;


        var request = new TrainSyncMessage
        {
            Type = "StateSyncRequest",
            TrainId = tren.CurrentTrain.ID,
            Ip = tren.CurrentTrain.IP,
            StationIndex = -1, // özel işaret → state isteği
            UpdatedAt = DateTime.Now
        };


        await _rabbitService.PublishMessage(
        RabbitMQConstants.RabbitMQHost,
        RabbitMQConstants.ContiniueSyncRotaExchangeName,
        ExchangeType.Fanout,
        "",
        request,
        ManagementEnum.Live);


        Console.WriteLine($"[{tren.CurrentTrain.ID}] diğer trenlerden state sync isteği gönderildi.");
    }

    //5 Sn de bir gönderiliyor
    private async Task StartHeartbeatAsync()
    {
        while (true)
        {
            await PublishHeartbeatAsync();
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    // 🔄 Sync mesajlarını işleme
    private async Task HandleSyncMessage(TrainSyncMessage message)
    {
        var myTrain = await _trainCoupledService.GetLastTrainData();
        if (myTrain == null) return;


        if (message.Type == "StateSyncRequest" && message.TrainId != myTrain.CurrentTrain.ID)
        {
            var response = new TrainSyncMessage
            {
                Type = "StateSyncResponse",
                TrainId = myTrain.CurrentTrain.ID,
                Ip = myTrain.CurrentTrain.IP,
                StationIndex = _currentStationIndex,
                RemainingDistance = (int?)_currentDistance ?? 0,
                NextStation = _stations.ElementAtOrDefault(_currentStationIndex + 1)?.stationName,
                TotalDistance = _stations.ElementAtOrDefault(_currentStationIndex)?.stationDistance ?? 0,
                UpdatedAt = DateTime.Now
            };


            //await PublishDataToAllCoupledTrainsAsync(response);
            Console.WriteLine($"[SYNC] {myTrain.CurrentTrain.ID} → {message.TrainId} SyncResponse gönderildi.");
            return;
        }


        if (message.Type == "Heartbeat" || message.Type == "StateSyncResponse")
        {
            if (message.UpdatedAt > _lastSyncedTime)
            {
                _lastSyncData = message;
                _lastSyncedTime = message.UpdatedAt;
                _syncManager.SetSyncMessage(message);


                Console.WriteLine($"[SYNC] Güncel state alındı: Station={message.StationIndex}");
            }
        }
    }
    // 🔥 ÖNEMLİ: ROTA DEVAM KARARI BURADA VERİLİYOR. Kapılar açılınca çağrılıyor
    public void CheckStationConfirmation()
    {
        var currentTime = DateTime.Now;
        if (ZeroSpeed == true && AllDoorReleased == true)
        {
            if (!_isRouteActive && _lastSyncData != null)
            {
                Console.WriteLine("[SYNC] Tren durdu + kapılar açıldı → sync datası uygulanıyor...");
                ActivateRouteFromSync(_lastSyncData);
                _isRouteActive = true;

                _logService?.InformationSendLogAsync(new InformationLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = $"Rota Kaldıgı yerden devam etmeye basladı : {_lastSyncData.NextStation} at {currentTime}",
                    MessageType = LogType.Information.ToString(),
                    DateTime = currentTime,
                });

            }
        }
    }

    private async Task StartTakoProcessing(List<Station> routeData)
    {
        _stations.Clear();

        var filteredStations = FilterAndCalculateSkipStations(routeData);

        //if (filteredStations.Any())
        //{

        //    _stations = filteredStations;
        //    ResetRoute();

        //    await InitializeFirstStation();

        //    Console.WriteLine("Yeni rota başlatıldı.....");
        //}

        if (!filteredStations.Any())
            return;

        _stations = filteredStations;



        // Eğer senkronizasyon datası varsa, o istasyondan başla
        if (_lastSyncData != null && _lastSyncData.StationIndex < _stations.Count)
        {
            Console.WriteLine($"Sync verisi bulundu: {_lastSyncData.StationIndex}. istasyon");

            // Durumları set et
            _currentStationIndex = _lastSyncData.StationIndex;
            _currentDistance = _lastSyncData.RemainingDistance!;

            //_stations = _stations.Skip(_currentStationIndex).ToList();


            // Gerekirse özel bir başlatma yap
            //await InitializeFromSyncedStation(_currentStationIndex, _currentDistance);

            _lastSyncData = null; // bir daha kullanmamak için temizle
        }
        else
        {
            _lastSyncData = null; // Sync verisi yoksa temizle
            ResetRoute();
            await InitializeFirstStation();
        }

        Console.WriteLine("Yeni rota başlatıldı.....");



    }

    public void ActivateRouteFromSync(TrainSyncMessage message)
    {
        _currentStationIndex = message.StationIndex;
        _currentDistance = message.DistanceFromStart ?? 0;
        _isRouteActive = true;
        //_nextStation = message.NextStation;


        Console.WriteLine($"Rota senkron veriye göre tekrar başlatıldı. {message.StationIndex}. istasyondan devam ediliyor.");
    }


    //Yeni rota gelince değerlerş sıfırlayan metot
    private void ResetRoute()
    {

        _lastSyncData = null;
        //PublishDataToAllCoupledTrainsAsync(_lastSyncData!); //2 . programın kapanıp tekrar acılması durumunda rotanın devam etmesi icin yazıldı .  1. acılırsa buda heryerde Acılabilir
        _isRouteActive = true;
        _currentStationIndex = 0;
        _TachoMeterPulse = 0;
        _hasStartAnnouncementPlayed = false;
        _approachingAnnouncementMade = false;
        _arrivalAnnouncementMade = false;
        _nextStationDisplayed = false;
        _terminalAnnouncementMade = false;



        _logService?.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"407 - Rota bitti ve bütün degerler resetlendi. {currentTime}",
            MessageType = LogType.Information.ToString(),
            DateTime = currentTime,
        });

    }


    //Tako hesaplama fonksiyonu
    public int CalculateDistance(int tako)
    {
        // Tako değerinden mesafe hesaplama mantığı
        tako = tako + TAKO_DISTANCE_FACTOR;  // Mevcut mesafeye ekleme
        return tako;

    }

    // **3. Tako Verisini Okuma ve İşleme fonksiyonu
    public async Task ReadAndProcessTakoAsync()
    {
        //int takoValue = -1; // Varsayılan bir değer
        try
        {
            // İlk istasyon kontrolü
            if (_currentStationIndex == 0 && !_isFirstStationInitialized)
            {
                await InitializeFirstStation();
            }
            //try
            //{
            //    takoValue = await _takoReaderService.ReadTakoPulseAsync();
            //}
            //catch (Exception)
            //{

            //    //Console.WriteLine(" - - - MOXA dan Tako verisi alınamadı......");
            //    takoValue = -1; // Hata durumunda varsayılan değeri koruyun
            //}

            //takoValue = await _takoReaderService.ReadTakoPulseAsync();  // Tako verisini oku

            var tcmsData = await _tcmsService.GetLatestTakoDataAsync();

            //if ((tcmsData != null && tcmsData?.TachoMeterPulse == true) || takoValue == 1)
            if ((tcmsData != null && tcmsData?.TachoMeterPulse == true))
            {
                _TachoMeterPulse = CalculateDistance(_TachoMeterPulse);

                Console.WriteLine($"TAKO verisi suan : {_TachoMeterPulse} at {currentTime}");
                _logService?.InformationSendLogAsync(new InformationLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = $"401 - TAKO verisi okundu : {_TachoMeterPulse} at {currentTime}",
                    MessageType = LogType.Information.ToString(),
                    DateTime = currentTime,
                });
                ////Tako gelince başlayacak
                //await CheckStationProgress(); // Rota kurulması bekleniyor , Kontrol ediliyor
            }

            await CheckStationProgress(); // Rota kurulması bekleniyor , Kontrol ediliyor



        }
        catch (Exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(" - - - Tako verisi gelmedigi icin bekliyor......");
            Console.ResetColor();

            await _logService.ErrorSendLogAsync(new ErrorLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "461 - Tako verisi gelmedigi icin bekliyor...",
                MessageType = LogType.Error.ToString(),
                DateTime = currentTime,
                MessageSourceType = "Software",
                HardwareIP = _ybsPcIp ?? "127.0.0.1",
            });
        }
    }




    //Rota tamamlandı
    public async Task CompleteRouteAsync()
    {


        //Burada DDU ekranına rota bitti bilgisi verilecek

        var lastStation = _stations[_currentStationIndex];

        if (lastStation.terminalAnnounce)
        {
            if (!_terminalAnnouncementMade)
            {

                await _anonsService.PlayAnnouncementAsync(
                    AnnouncementType.Terminal,
                    lastStation.stationName!, lastStation.stationName!
                );

                await _ledService.UpdateDisplay(LedDisplayType.stationTerminalLed, lastStation.stationName!);//Sonradan eklendi

                _terminalAnnouncementMade = true;
            }
        }
        _isRouteActive = false;
        _stations.Clear();
        _currentStationIndex = 0;
        Console.WriteLine("Route Tamamlandı");
        _routeCompleted = true;
        var message = new
        {
            RouteCompleted = _routeCompleted
        };
        //Burada DDU ekranına rota bitti bilgisi verilecek
        await _rabbitService.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.RouteCompletedExchangeName, ExchangeType.Fanout, "", message, ManagementEnum.Live);


        await _logService.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "405 - Rota Tamamlandı.",
            MessageType = LogType.Information.ToString(),
            DateTime = currentTime,
        });

    }


    //Skipstation durumu kontrolü
    public List<Station> FilterAndCalculateSkipStations(List<Station> stations)
    {

        // 🚨 Eğer `stations` NULL veya boşsa hata almamak için kontrol ekleyelim.
        if (stations == null || !stations.Any())
        {
            Console.WriteLine("Uyarı: İstasyon listesi boş veya null!!!");
            _lastSyncData = null; // Sync verisi yok
            //PublishDataToAllCoupledTrainsAsync(_lastSyncData!);
            return new List<Station>();  // ✅ Boş bir liste döndürerek hatayı önleriz.
        }



        List<Station> processedStations = new List<Station>();
        int cumulativeT1Distance = 0;

        Station lastValidStation = null; // Son geçerli istasyonu takip etmek için

        for (int i = 0; i < stations.Count; i++)
        {
            var currentStation = stations[i];

            if (currentStation.skipStationState)
            {
                //// Skip edilen istasyonların mesafe ve boy değerlerini topla

                //// Eğer bu ilk istasyon ise önceki istasyon yoktur, hata olmaması için kontrol et
                //if (i > 0)
                //{
                //    var previousStation = stations[i - 1]; // Bir önceki istasyonu al
                //    previousStation.stationDistance += currentStation.stationDistance; // Mesafeyi önceki istasyona ekle
                //}

                cumulativeT1Distance += currentStation.stationDistance;



            }
            else
            {
                //// Mesafe ve boy değerlerini birleştir
                //currentStation.stationDistance += cumulativeT1Distance;

                //// Toplamları sıfırla
                //cumulativeT1Distance = 0;

                //processedStations.Add(currentStation);

                ////Son skip edilmeyen istasyonu takip etmek için kullanıyoruz.
                ////previousValidStation = currentStation; // Yeni referans noktası olarak belirle
                ///

                // Eğer daha önce biriken mesafe varsa, son geçerli istasyona ekle
                if (lastValidStation != null)
                {
                    lastValidStation.stationDistance += cumulativeT1Distance;
                }

                // Şu anki istasyonu listeye ekle ve referans olarak güncelle
                processedStations.Add(currentStation);
                lastValidStation = currentStation; // Yeni referans noktası belirle

                // Toplamı sıfırla
                cumulativeT1Distance = 0;
            }
        }

        return processedStations;
    }




    //Sonraki istasyona kalan mesafe
    public int GetDistanceToNextStation(Station nextStation)
    {
        if (_currentStationIndex >= _stations.Count - 1) return 0;

        //var nextStation = _stations[_currentStationIndex + 1];
        var distance = nextStation.stationDistance - _TachoMeterPulse;
        var distanceToStation = Math.Max(distance, 0);//eksiye düşmesini engellemek için istege baglı yapılıcak

        var consoleText = $"HESAPLANAN MESAFE ===== {distance}";
        Console.WriteLine(consoleText);
        return distance;
    }


    //Tako degeri sıfırlama
    private async Task ResetTakoAsync()
    {
        //await _takoReaderService.ResetTakoPulseAsync();
        _TachoMeterPulse = 0;

        Console.WriteLine("Tako değeri sıfırlandı ve RabbitMQ ye bilgi gönderildi");

        await _logService.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"406 - TAKO verisi resetlendi : {_TachoMeterPulse} at {currentTime}",
            MessageType = LogType.Information.ToString(),
            DateTime = currentTime,
        });
    }


    // İstasyona ulastıgında yapması gereken islemler
    public async Task CheckStationArrivalAsync(bool ZeroSpeed, bool AllDoorReleased)
    {

        if (!_isRouteActive || _currentStationIndex >= _stations.Count) return;

        var currentStation = _stations[_currentStationIndex];

        //if (_currentStationIndex + 1 < _stations.Count)
        //{

        var nextStation = _stations[_currentStationIndex + 1];
        //var distanceToStation = GetDistanceToNextStation(nextStation);
        //DDU ve Stretch lcd ye bilgi gönderildi
        await _lcdService.UpdateDistance(new LcdInfo
        {
            RemainingDistance = 0// İstasyona varıldığında kalan mesafe 0 olmalı
        });
        await _lcdService.UpdateDisplay(new LcdInfo
        {
            NextStation = nextStation.stationName,
            //RemainingDistance = Convert.ToInt32(distanceToStation)
        });


        if (ZeroSpeed == true && AllDoorReleased == true)
        {
            Console.WriteLine($"İstasyona Ulasıldı {nextStation.stationName}");

            await _logService.InformationSendLogAsync(new InformationLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"403 - İstasyona Ulasıldı : {nextStation.stationName}",
                MessageType = LogType.Information.ToString(),
                DateTime = currentTime,
            });

            // 🔄 İstasyon senkronizasyonu burada  tren kapanıp açılma senaryosuna göre çalışıcak burası
            var tren = await _trainCoupledService.GetLastTrainData();
            var syncDto = new TrainSyncMessage
            {
                TrainId = tren.CurrentTrain.ID,
                NextStation = nextStation.stationName,
                RemainingDistance = 0, // İstasyona ulaşıldığında kalan mesafe 0 olmalı
                TotalDistance = nextStation.stationDistance,
                UpdatedAt = DateTime.Now
            };

            CheckStationConfirmation();  // Yarıda kapanıp açılma durumunda Rota aktif etme kontrolü


            // Takometre sıfırlama
            await ResetTakoAsync();
            _currentStationIndex++;  // Bir sonraki istasyona geç

            // Bayrakları sıfırla
            _approachingAnnouncementMade = false;
            _arrivalAnnouncementMade = false;
            _nextStationDisplayed = false; // Yeni istasyona geçtiğinde bayrağı sıfırla



            if (IsLastStation())
            {
                await CompleteRouteAsync();
            }
            else
            {
                //_currentStationIndex++;  // Bir sonraki istasyona geç
                await MoveToNextStationAsync();
                //await InitializeFirstStation();  // Yeni istasyonu başlat
            }
        }
    }


    //Son istasyon mu
    public bool IsLastStation()
    {
        return _currentStationIndex >= _stations.Count - 1;
    }


    // Sonraki istasyona geçiş fonksiyonu
    public async Task MoveToNextStationAsync()
    {
        _TachoMeterPulse = 0;
        // Bayrakları sıfırla
        _approachingAnnouncementMade = false;
        _arrivalAnnouncementMade = false;
        _nextStationDisplayed = false; // Yeni istasyona geçtiğinde bayrağı sıfırla
        var currentStation = _stations[_currentStationIndex];
        //UpdateDisplays();

        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "440 - Sonraki istasyona geciyor,HMIController ve StretchController'a bilgiler gönderildi.",
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = _ybsPcIp ?? "127.0.0.1",
            DestinationIP = _ybsPcIp ?? "127.0.0.1",
            DestinationName = "StretchController"
        });
    }

    // ** 2.  ilk istasyon ataması
    public async Task InitializeFirstStation()
    {

        //// Eğer rota zaten başlatıldıysa tekrar sıfırlama!
        //if (_currentStationIndex != 0) return;

        // Eğer rota zaten başlatıldıysa veya ilk istasyon zaten başlatıldıysa, geri dön
        if (_currentStationIndex != 0 && _isFirstStationInitialized) return;

        var currentStation = _stations[_currentStationIndex];
        var lastItem = _stations.LastOrDefault();

        // LED ve LCD güncelleme
        UpdateDisplays();


        Console.WriteLine($"Baslangıc Anons Durumu:== {currentStation.stationStartAnnounce}");
        // Başlangıç anonsu kontrolü
        if (currentStation.stationStartAnnounce && !_hasStartAnnouncementPlayed)
        {
            await _anonsService.PlayAnnouncementAsync(
                AnnouncementType.Start,
                currentStation.stationName!, lastItem!.stationName!
            );
            _hasStartAnnouncementPlayed = true;

        }


        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "434 - Yeni Rota Kuruldu, HMIController ve StretchController'a ilk atamalar yapıldı.",
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = _ybsPcIp ?? "127.0.0.1",
            DestinationIP = _ybsPcIp ?? "127.0.0.1",
            DestinationName = "StretchController"
        });
        _isFirstStationInitialized = true; // İlk istasyon başlatıldı olarak işaretle
    }


    // İlk Led ve LCD    tanımlamaları
    public void UpdateDisplays()
    {
        if (_currentStationIndex >= _stations.Count - 1) return;

        var currentStation = _stations[_currentStationIndex];
        var nextStation = _stations[_currentStationIndex + 1];
        var lastItem = _stations.LastOrDefault()!;
        var distanceToNext = GetDistanceToNextStation(currentStation);

        // LED güncelleme
        _ledService.UpdateDisplay(
            LedDisplayType.stationStartLed,
            currentStation.stationName!
        );
        _ledService.UpdateDisplay(LedDisplayType.stationStartLed, lastItem.stationName!, true);//Hedef LED
        // LCD stationName güncelleme
        _lcdService.UpdateDisplay(new LcdInfo
        {
            NextStation = currentStation.stationName,
            //RemainingDistance = Convert.ToInt32(distanceToNext),
            //TotalDistance = Convert.ToInt32(nextStation.stationDistance)
        });

        // LCD mesafe güncelleme
        _lcdService.UpdateDistance(new LcdInfo
        {

            RemainingDistance = currentStation.stationDistance,
            TotalDistance = currentStation.stationDistance

        });

    }



    //** 4. İstasyon ilerleme . Bütün işleyişin oldugu fonksiyon
    private async Task CheckStationProgress()
    {

        //// Kaynak IP'yi MongoDB'den oku
        //var trainConfig = await _mongoDbService.GetTrainConfigurationAsync();

        //var sourceIp = trainConfig.Software?.FirstOrDefault(h => h.Name == "Central Maintenance Server")?.ip;
        //var destinationIp = trainConfig.Software?.FirstOrDefault(s => s.Name == "Central Maintenance Client")?.ip;


        //Rota kontrolü yapılıyorrrr
        if (_currentStationIndex >= _stations.Count)
        {
            Console.WriteLine("Rota bitti  veya yeni rota  bekleniyorrr.");

            _lastSyncData = null;
            //PublishDataToAllCoupledTrainsAsync(_lastSyncData!);

            await _logService.EventSendLogAsync(new EventLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "431 - Rota Bitti veya Yeni Rota  Bekleniyor.",
                MessageType = LogType.Event.ToString(),
                DateTime = currentTime,
                SourceIP = _ybsPcIp ?? "127.0.0.1",
                DestinationIP = _ybsPcIp ?? "127.0.0.1",
                DestinationName = "HMIController"
            });

            _isRouteActive = false;
            return;
        }

        var currentStation = _stations[_currentStationIndex];
        var nextStation = _stations[_currentStationIndex + 1];
        var lastItem = _stations.LastOrDefault()!;
        var distanceToStation = GetDistanceToNextStation(currentStation);//Metraj hesaplama


        //// 🔄 İstasyon senkronizasyonu burada  tren kapanıp açılma senaryosuna göre çalışıcak burası
        //var tren = await _trainCoupledService.GetLastTrainData();
        //var syncDto = new TrainSyncMessage
        //{
        //    TrainId = tren.CurrentTrain.ID,
        //    Ip = tren.CurrentTrain.IP,
        //    StationIndex = _currentStationIndex,
        //    RemainingDistance = distanceToStation,
        //    NextStation = currentStation.stationName,
        //    DistanceFromStart = distanceToStation, // İstasyona ulaşıldığında kalan mesafe 0 olmalı
        //    TotalDistance = currentStation.stationDistance,
        //    UpdatedAt = DateTime.Now
        //};

        ////Burada DDU ekranına kalan mesafe ve toplam mesafe gönderilicek
        await _lcdService.UpdateDistance(new LcdInfo
        {
            RemainingDistance = distanceToStation,
            TotalDistance = currentStation.stationDistance

        });


        // Eğer tren istasyondan çıktıktan sonra 20 metre ilerlediyse VE daha önce güncellenmediyse
        if (distanceToStation <= currentStation.stationDistance - istasyondanCıkısMesafesi && !_nextStationDisplayed)
        {
            await _lcdService.UpdateDisplay(new LcdInfo
            {
                NextStation = nextStation.stationName
            });

            _nextStationDisplayed = true; // Bir daha girmemesi için bayrağı true yap
        }


        // **1️⃣ Yaklaşma Anonsu (Önce Olmalı)**
        else if (distanceToStation <= currentStation.stationApproachAnnounceDistance &&
            distanceToStation > currentStation.stationArrivalAnnounceDistance) // 🔹 500m - 150m arası
        {

            //    // Yaklaşma anonsu
            if (!_approachingAnnouncementMade)
            {
                await _anonsService.PlayAnnouncementAsync(AnnouncementType.Approaching,
                nextStation.stationName!, lastItem.stationName!);


                //Console.WriteLine("Yaklaşma anonsu yapıldı: {Station}", nextStation.stationName);
                await _ledService.UpdateDisplay(LedDisplayType.stationApproachLed, nextStation.stationName!);

                await _lcdService.UpdateDisplay(new LcdInfo
                {
                    NextStation = nextStation.stationName,
                    //RemainingDistance = Convert.ToInt32(distanceToStation)
                });

                _approachingAnnouncementMade = true;
            }


            await _lcdService.UpdateDistance(new LcdInfo
            {
                RemainingDistance = distanceToStation,
                TotalDistance = currentStation.stationDistance
            });

        }


        // İstasyon anonsu

        else if (distanceToStation <= currentStation.stationArrivalAnnounceDistance && distanceToStation > 0)
        {

            if (!_arrivalAnnouncementMade)
            {

                await _anonsService.PlayAnnouncementAsync(AnnouncementType.Arrival,
                nextStation.stationName!, lastItem.stationName!);

                await _ledService.UpdateDisplay(LedDisplayType.stationArrivalLed, nextStation.stationName!);

                await _lcdService.UpdateDisplay(new LcdInfo
                {
                    NextStation = nextStation.stationName,
                    //RemainingDistance = Convert.ToInt32(distanceToStation)
                });

                _arrivalAnnouncementMade = true;


            }
            //Kalan mesafe kuyruga iletildi
            await _lcdService.UpdateDistance(new LcdInfo
            {
                RemainingDistance = distanceToStation,
                TotalDistance = currentStation.stationDistance
            });

        }

        // İstasyona varış
        else if (distanceToStation <= 0)
        {
            //// Burada ZeroSpeed ve AllDoorsReleased TCMS den alındıgında islenecektir
            //// TCMS'den güncel verileri al

            var tcmsData = await _tcmsService.GetLatestTakoDataAsync();
            //ZeroSpeed = 0;
            //AllDoorsReleased = true;
            if (tcmsData.ZeroSpeed == true && tcmsData.Doors.AllDoorReleased == true)
            {
                // İstasyona ulaşıldı
                await CheckStationArrivalAsync(tcmsData.ZeroSpeed, tcmsData.Doors.AllDoorReleased);

                await _logService.InformationSendLogAsync(new InformationLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = $"404 - Kapılar Acıldı: {tcmsData.Doors.AllDoorReleased} ve Hız Sıfır(0) {tcmsData.ZeroSpeed}",
                    MessageType = LogType.Information.ToString(),
                    DateTime = currentTime,
                });

            }
            else
            {
                Console.WriteLine("İstasyona henüz ulaşılmadı.");
            }
            //Console.WriteLine($"{nextStation.stationName} istasyonuna ulaşıldı.");

            //RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", AllDoorsReleased);
            //await CheckStationArrivalAsync(ZeroSpeed, AllDoorsReleased);
        }
        //else if (distanceToStation <= -50)
        //{
        //    await _logService.WarningSendLogAsync(new WarningLogDto
        //    {
        //        MessageSource = "LogicManager",
        //        MessageContent = "İstasyona ulaşıldı fakat TCMS verisi alınamıyor. ZeroSpeed/AllDoorsReleased kontrol edin.",
        //        MessageType = LogType.Warning.ToString(),
        //        DateTime = DateTime.Now
        //    });
        //}
    }



    //Kuplajdaki trenlerin ip sini almak icin
    public async Task<string> GetSourceIpAsync()
    {
        var tren = await _trainCoupledService.GetLastTrainData();
        if (tren == null || tren.CouplingTrainsIds == null)
        {
            return null;
        }
        var trenId = tren.CouplingTrainsIds;

        var trainConfig = await _mongoDbService.GetTrainConfigurationAsync();
        return trainConfig?.Hardware?.FirstOrDefault(h => h.Name == "YBS PC")?.ip!;
    }


    private async Task PublishHeartbeatAsync()
    {
        var tren = await _trainCoupledService.GetLastTrainData();
        if (tren == null) return;


        var heartbeat = new TrainSyncMessage
        {
            Type = "Heartbeat",
            TrainId = tren.CurrentTrain.ID,
            Ip = tren.CurrentTrain.IP,
            StationIndex = _currentStationIndex,
            RemainingDistance = _TachoMeterPulse,//(int?)_currentDistance ?? 0,  //veya distanceToStation gelebilir test edilicek
            NextStation = _stations.ElementAtOrDefault(_currentStationIndex + 1)?.stationName,
            TotalDistance = _stations.ElementAtOrDefault(_currentStationIndex)?.stationDistance ?? 0,
            UpdatedAt = DateTime.Now
        };


        await _rabbitService.PublishMessage(
        RabbitMQConstants.RabbitMQHost,
        RabbitMQConstants.ContiniueSyncRotaExchangeName,
        ExchangeType.Fanout,
        "",
        heartbeat,
        ManagementEnum.Live);


        Console.WriteLine($"[HB] {tren.CurrentTrain.ID} → Heartbeat gönderildi.");
    }


}



