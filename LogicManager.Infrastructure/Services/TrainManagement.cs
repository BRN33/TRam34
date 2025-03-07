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

namespace LogicManager.Infrastructure.Services;

public class TrainManagement : ITrainManagement
{

    // Degisken Tanımlamaları

    private readonly IServiceScope _serviceProvider; //Dependency Injection yapma için 

    //private readonly ITcmsService _tcmsService;
    private readonly IAnonsService _anonsService;
    private readonly ILedService _ledService;
    private readonly ILcdService _lcdService;
    private readonly ITakoReaderService _takoReaderService;
    private readonly IRouteService _routeService;
    private readonly IMongoDbService _mongoDbService;
    private readonly LoggerHelper _logService;
    public List<Station> _stations;

    private const int TAKO_DISTANCE_FACTOR = 5; // Her tako pulse için mesafe çarpanı

    public int _currentStationIndex;
    public int _TachoMeterPulse;
    public double ZeroSpeed;
    public bool AllDoorsReleased;
    private bool _isRouteActive;
    public bool IsRouteActive
    {
        get => _isRouteActive;
        private set => _isRouteActive = value;
    }
    private bool _routeCompleted;

    public bool _hasStartAnnouncementPlayed;//Baslangıc Anonsu bir kere göndermek icin
    private bool _approachingAnnouncementMade = false;//Anonsu bir kere göndermek icin
    private bool _arrivalAnnouncementMade = false;//Anonsu bir kere göndermek icin
    private bool _terminalAnnouncementMade = false;//Terminal Anonsu bir kere göndermek icin
    private bool _transferAnnouncementMade = false;//Aktarma Anonsu bir kere göndermek icin
    private bool _privateAnnouncementMade = false;//Özel Anonsu bir kere göndermek icin

    private bool _isFirstStationInitialized = false;
    private bool _isFirstRun = true;
    private bool _nextStationDisplayed = false;
    private int istasyondanCıkısMesafesi;

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
        //_tcmsService = _serviceProvider.ServiceProvider.GetRequiredService<ITcmsService>();
        istasyondanCıkısMesafesi = Convert.ToInt32(configuration["TcmsSettings:istasyondanCıkısMesafesi"]);
        _stations = new List<Station>();

        // Route service event'ine abone ol
        //((RouteService)_routeService).OnRouteUpdated += HandleRouteUpdated;
        _routeService.OnRouteUpdated += async (routeData) => await StartTakoProcessing(routeData);
    }

    private async Task StartTakoProcessing(List<Station> routeData)
    {
        _stations.Clear();
        var filteredStations = FilterAndCalculateSkipStations(routeData);

        if (filteredStations.Any())
        {

            _stations = filteredStations;
            ResetRoute();
            //_isRouteActive = true;
            //_currentStationIndex = 0;
            //_approachingAnnouncementMade = false;
            //_arrivalAnnouncementMade = false;
            //_nextStationDisplayed = false;

            await InitializeFirstStation();

            Console.WriteLine("Yeni rota başlatıldı.....");
        }

    }
    //Yeni rota gelince değerlerş sıfırlayan metot
    private void ResetRoute()
    {
        _isRouteActive = true;
        _currentStationIndex = 0;
        _TachoMeterPulse = 0;
        _hasStartAnnouncementPlayed = false;
        _approachingAnnouncementMade = false;
        _arrivalAnnouncementMade = false;
        _nextStationDisplayed = false;

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
        try
        {
            // İlk istasyon kontrolü
            if (_currentStationIndex == 0 && !_isFirstStationInitialized)
            {
                await InitializeFirstStation();
            }

            int takoValue = await _takoReaderService.ReadTakoPulseAsync();  // Tako verisini oku

            if (takoValue == 1)
            {
                _TachoMeterPulse = CalculateDistance(_TachoMeterPulse);
                var currentTime = DateTime.Now;
                Console.WriteLine($"TAKO verisi suan : {_TachoMeterPulse} at {currentTime}");
                await _logService.InformationSendLogAsync(new InformationLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = $"TAKO verisi okundu : {_TachoMeterPulse} at {currentTime}",
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
            var currentTime = DateTime.Now;
            await _logService.ErrorSendLogAsync(new ErrorLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "Tako verisi gelmedigi icin bekliyor...",
                MessageType = LogType.Error.ToString(),
                DateTime = currentTime,
                ErrorType = LogType.Error.ToString(),
                HardwareIP = "10.3.156.224"
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
                _terminalAnnouncementMade = true;
            }
        }
        _isRouteActive = false;
        _stations.Clear();
        _currentStationIndex = 0;
        Console.WriteLine("Route Tamamlandı");
        _routeCompleted = true;
        //Burada DDU ekranına rota bitti bilgisi verilecek

        var currentTime = DateTime.Now;
        await _logService.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "Rota Bitti --- Yeni Rota Bekleniyor ...",
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
        var consoleText= $"HESAPLANAN MESAFE ===== {distance}";
        Console.WriteLine(consoleText);
        return distance;
    }


    //Tako degeri sıfırlama
    private async Task ResetTakoAsync()
    {
        //await _takoReaderService.ResetTakoPulseAsync();
        _TachoMeterPulse = 0;

        Console.WriteLine("Tako değeri sıfırlandı ve RabbitMQ ye bilgi gönderildi");
        var currentTime = DateTime.Now;
        await _logService.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"TAKO verisi Resetlendi : {_TachoMeterPulse} at {currentTime}",
            MessageType = LogType.Information.ToString(),
            DateTime = currentTime,
        });
    }


    // İstasyona ulastıgında yapması gereken islemler
    public async Task CheckStationArrivalAsync(double ZeroSpeed, bool AllDoorsReleased)
    {

        if (!_isRouteActive || _currentStationIndex >= _stations.Count) return;

        var currentStation = _stations[_currentStationIndex];
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
        if (ZeroSpeed == 0 && AllDoorsReleased == true)
        {
            Console.WriteLine($"İstasyona Ulasıldı {nextStation.stationName}");
            var currentTime = DateTime.Now;
            await _logService.InformationSendLogAsync(new InformationLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"İstasyona Ulasıldı : {nextStation.stationName} at {currentTime}",
                MessageType = LogType.Information.ToString(),
                DateTime = currentTime,
            });

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
        var currentStation = _stations[_currentStationIndex];
        //UpdateDisplays();
        var currentTime = DateTime.Now;
        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "Sonraki istasyona geciyor,DDU ve Stretch LCD ye bilgiler gönderildi",
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = "10.3.156.224",
            DestinationIP = "10.3.156.55",
            DestinationName = "LCDService"
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

        // Başlangıç anonsu kontrolü
        if (currentStation.stationStartAnnounce && !_hasStartAnnouncementPlayed)
        {
            await _anonsService.PlayAnnouncementAsync(
                AnnouncementType.Start,
                currentStation.stationName!, lastItem.stationName!
            );
            _hasStartAnnouncementPlayed = true;
        }

        var currentTime = DateTime.Now;
        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "DDU ve Stretch LCD ye ilk atamalar yapıldı",
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = "10.3.156.224",
            DestinationIP = "10.3.156.55",
            DestinationName = "LCDServiceSend"
        });
        _isFirstStationInitialized = true; // İlk istasyon başlatıldı olarak işaretle
    }


    // İlk Led ve LCD    tanımlamaları
    public void UpdateDisplays()
    {
        if (_currentStationIndex >= _stations.Count - 1) return;

        var currentStation = _stations[_currentStationIndex];
        var nextStation = _stations[_currentStationIndex + 1];
        var distanceToNext = GetDistanceToNextStation(currentStation);

        // LED güncelleme
        _ledService.UpdateDisplay(
            LedDisplayType.stationStartLed,
            currentStation.stationName!
        );
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
            TotalDistance = Convert.ToInt32(currentStation.stationDistance)

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
            var currentTime = DateTime.Now;
            await _logService.EventSendLogAsync(new EventLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "Rota bitti  veya yeni rota  bekleniyorrr...",
                MessageType = LogType.Event.ToString(),
                DateTime = currentTime,
                SourceIP = "100.10.100.100",
                DestinationIP = "100.10.100.100",
                DestinationName = "DDU_Servisi"
            });

            _isRouteActive = false;
            return;
        }

        var currentStation = _stations[_currentStationIndex];
        var nextStation = _stations[_currentStationIndex + 1];
        var lastItem = _stations.LastOrDefault()!;
        int distanceToStation = GetDistanceToNextStation(currentStation);//Metraj hesaplama

        ////Burada DDU ekranına kalan mesafe ve toplam mesafe gönderilicek
        await _lcdService.UpdateDistance(new LcdInfo
        {
            RemainingDistance = Convert.ToInt32(distanceToStation),
            TotalDistance = Convert.ToInt32(currentStation.stationDistance)

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

                //Log servisine gönderildi
                var currentTime = DateTime.Now;
                await _logService.EventSendLogAsync(new EventLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = "Anons yapıldı, DDU ve Stretch LCD ye bilgiler gönderildi",
                    MessageType = LogType.Event.ToString(),
                    DateTime = currentTime,
                    SourceIP = "10.3.156.224",
                    DestinationIP = "10.3.156.55",
                    DestinationName = "AnonsServisi"
                });
                _approachingAnnouncementMade = true;
            }


            await _lcdService.UpdateDistance(new LcdInfo
            {
                RemainingDistance = Convert.ToInt32(distanceToStation),
                TotalDistance = Convert.ToInt32(currentStation.stationDistance)
            });

        }

        //// Transfer anonsu kontrolü (450m)
        //if (distanceToNext <= currentStation.stationTransferAnnounceT1 && !_transferAnnouncementMade)
        //{
        //    await _announcementService.PlayAnnouncementAsync(
        //        AnnouncementType.Transfer,
        //        nextStation.stationName);

        //    _transferAnnouncementMade = true;
        //    _logger.LogInformation("Transfer anonsu yapıldı: {Station}, Mesafe: {Distance}m",
        //        nextStation.stationName, distanceToNext);
        //}

        //// Özel anons kontrolü (250m)
        //if (distanceToNext <= currentStation.stationPrivateAnnounceT1 && !_privateAnnouncementMade)
        //{
        //    await _announcementService.PlayAnnouncementAsync(
        //        AnnouncementType.Private,
        //        nextStation.stationName);

        //    _privateAnnouncementMade = true;
        //    _logger.LogInformation("Özel anons yapıldı: {Station}, Mesafe: {Distance}m",
        //        nextStation.stationName, distanceToNext);
        //}





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
                RemainingDistance = Convert.ToInt32(distanceToStation),
                TotalDistance = Convert.ToInt32(currentStation.stationDistance)
            });
            //Log servisine gönderildi
            var currentTime = DateTime.Now;
            await _logService.EventSendLogAsync(new EventLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "Anons yapıldı, DDU ve Stretch LCD ye bilgiler gönderildi",
                MessageType = LogType.Event.ToString(),
                DateTime = currentTime,
                SourceIP = "10.3.156.224",
                DestinationIP = "10.3.156.55",
                DestinationName = "AnonsServisi"
            });


        }

        // İstasyona varış
        else if (distanceToStation <= 0)
        {
            //// Burada ZeroSpeed ve AllDoorsReleased TCMS den alındıgında islenecektir
            //// TCMS'den güncel verileri al
            ////var tcmsData = await _tcmsService.GetTcmsDataAsync();
            ////if (tcmsData.IsZeroSpeed && tcmsData.AllDoorsReleased)
            ////{

            ZeroSpeed = 0;
            AllDoorsReleased = true;
            //Console.WriteLine($"{nextStation.stationName} istasyonuna ulaşıldı.");

            //RabbitMQHelper.PublishMessage(RabbitMQConstants.RabbitMQHost, RabbitMQConstants.NextStationInfoExchangeName, ExchangeType.Fanout, "", AllDoorsReleased);
            await CheckStationArrivalAsync(ZeroSpeed, AllDoorsReleased);
        }
    }

    // Arka plan servisinde veya Tako verisi geldiğinde çağırabilirsiniz.
    private async Task CheckRouteStatus()
    {
        if (_currentStationIndex >= _stations.Count)
        {
            Console.WriteLine("Rota bitti veya yeni rota bekleniyor.");
            var currentTime = DateTime.Now;
            await _logService.EventSendLogAsync(new EventLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "Rota bitti veya yeni rota bekleniyor...",
                MessageType = LogType.Event.ToString(),
                DateTime = currentTime,
                SourceIP = "100.10.100.100",
                DestinationIP = "100.10.100.100",
                DestinationName = "AnonsServisi"
            });

            _isRouteActive = false;
            return;
        }
    }
}



