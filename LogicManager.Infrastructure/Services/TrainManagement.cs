using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using LogicManager.Shared.Helpers;
using LogicManager.Shared.DTOs;

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
    private readonly ILogger<TrainManagement> _logger;

    private const int TAKO_DISTANCE_FACTOR = 5; // Her tako pulse için mesafe çarpanı

    private readonly LoggerHelper _logService;

    public List<Station> _stations;
    public int _currentStationIndex;
    public int _TachoMeterPulse;
    private DateTime _lastTakoReadTime = DateTime.Now;
    public double ZeroSpeed;
    public bool AllDoorsReleased;
    public bool _isRouteActive;
    private bool _routeCompleted;

    public bool _hasStartAnnouncementPlayed;//Baslangıc Anonsu bir kere göndermek icin
    private bool _approachingAnnouncementMade = false;//Anonsu bir kere göndermek icin
    private bool _arrivalAnnouncementMade = false;//Anonsu bir kere göndermek icin
    private bool _terminalAnnouncementMade = false;//Terminal Anonsu bir kere göndermek icin
    private bool _transferAnnouncementMade = false;//Aktarma Anonsu bir kere göndermek icin
    private bool _privateAnnouncementMade = false;//Özel Anonsu bir kere göndermek icin



    public TrainManagement(IServiceProvider serviceProvider, ILogger<TrainManagement> logger, LoggerHelper logger1)
    {
        _serviceProvider = serviceProvider.CreateScope();
        _anonsService = _serviceProvider.ServiceProvider.GetRequiredService<IAnonsService>();
        _ledService = _serviceProvider.ServiceProvider.GetRequiredService<ILedService>();
        _lcdService = _serviceProvider.ServiceProvider.GetRequiredService<ILcdService>();
        _takoReaderService = _serviceProvider.ServiceProvider.GetRequiredService<ITakoReaderService>();
        _routeService = _serviceProvider.ServiceProvider.GetRequiredService<IRouteService>();
        _logService = _serviceProvider.ServiceProvider.GetRequiredService<LoggerHelper>();
        //_tcmsService = _serviceProvider.ServiceProvider.GetRequiredService<ITcmsService>();

        _logger = logger;
        _stations = new List<Station>();
        // Route service event'ine abone ol
        //((RouteService)_routeService).OnRouteUpdated += HandleRouteUpdated;
    }



    private async void HandleRouteUpdated(object sender, List<Station> newRoute)
    {
        try
        {
            var filteredStations = FilterAndCalculateSkipStations(newRoute);

            if (filteredStations.Any())
            {
                _stations = filteredStations;
                _isRouteActive = true;
                _currentStationIndex = 0;
                //_accumulatedDistance = 0;
                await InitializeFirstStation();

                _logger.LogInformation("Yeni rota başlatıldı. İlk istasyon: {StationName}",
                    _stations[0].stationName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yeni rota işlenirken hata oluştu");
        }
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

            int takoValue = await _takoReaderService.ReadTakoPulseAsync();  // Tako verisini oku

            if (takoValue == 1)
            {
                _TachoMeterPulse = CalculateDistance(_TachoMeterPulse);

                Console.WriteLine($"TAKO verisi suan : {_TachoMeterPulse} at {DateTime.Now}");
                await _logService.InformationSendLogAsync(new InformationLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = $"TAKO verisi okundu : {_TachoMeterPulse} at {DateTime.Now}",
                    MessageType = LogType.Information.ToString(),
                    DateTime = DateTime.Now,
                });

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
                MessageContent = "Tako verisi gelmedigi icin bekliyor...",
                MessageType = LogType.Error.ToString(),
                DateTime = DateTime.Now,
                ErrorType = LogType.Error.ToString(),
                HardwareIP = "10.3.156.224"
            });
        }
    }

    // ** 1.  Rota kontrolü yapar ve ilk işlemleri gerçekleştirir
    public async Task CheckAndInitializeRouteAsync()
    {
        try
        {
            // Eğer rota zaten başlatılmışsa tekrar başlatma
            if (_isRouteActive) return;

            //// Eğer rota zaten bittiyse yenisini bekle
            //if (_routeCompleted) return;

            var routeStatus = await _routeService.GetAllRouteAsync();// Rota okuma için Api yazılıcak


            // **Eğer yeni rota gelmediyse işlem yapma**
            if (routeStatus == null || !routeStatus.Any())
            {
                Console.WriteLine("🚦 Yeni rota bulunamadı, bekleniyor...");
                return;
            }
            var filteredStations = FilterAndCalculateSkipStations(routeStatus);


            if (!filteredStations.Any())
            {
                _isRouteActive = false;
                Console.WriteLine("Hiçbir istasyon aktif rota için uygun değil.");
                return;
            }

            // Eğer zaten bir istasyon listesi varsa tekrar sıfırlama!
            if (!_stations.Any())
            {
                _stations = filteredStations;
                _isRouteActive = true;
                _currentStationIndex = 0;  // Rota ilk kez başlatıldığında sıfırla
                _hasStartAnnouncementPlayed = false;

                await InitializeFirstStation();
            }

        }
        catch (Exception)
        {
            await _logService.ErrorSendLogAsync(new ErrorLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "Rota bilgisi gelmedi veya baglantı yok...",
                MessageType = LogType.Error.ToString(),
                DateTime = DateTime.Now,
                ErrorType = LogType.Error.ToString(),
                HardwareIP = "10.3.156.55"
            });
            Console.WriteLine("Error Rota Durumu Kontrolü ");
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
                    lastStation.stationName!
                );
                _terminalAnnouncementMade = true;
            }
        }
        _isRouteActive = false;
        _stations.Clear();
        _currentStationIndex = 0;
        Console.WriteLine("Route Tamamlandı");
        _routeCompleted = true;
        await _logService.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "Rota Bitti --- Yeni Rota Bekleniyor ...",
            MessageType = LogType.Information.ToString(),
            DateTime = DateTime.Now,
        });


    }
    //private List<Station> FilterAndCalculateSkipStations(List<Station> stations)
    //{
    //    return stations.Where(s => !s.skipStationState).ToList();
    //}

    //Skipstation durumu kontrolü
    public List<Station> FilterAndCalculateSkipStations(List<Station> stations)
    {

        // 🚨 Eğer `stations` NULL veya boşsa hata almamak için kontrol ekleyelim.
        if (stations == null || !stations.Any())
        {
            Console.WriteLine("🚨 Uyarı: İstasyon listesi boş veya null!");
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
        Console.WriteLine("HESAPLANAN MESAFE =====" + distance);
        //Burada DDU ve Stretch lcd ye bilgi verilicek
        return distance;
    }


    //Tako degeri sıfırlama
    private async Task ResetTakoAsync()
    {
        //await _takoReaderService.ResetTakoPulseAsync();
        _TachoMeterPulse = 0;
        _lastTakoReadTime = DateTime.Now;
        _logger.LogInformation("Tako değeri sıfırlandı ve RabbitMQ ye bilgi gönderildi");
        await _logService.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"TAKO verisi Resetlendi : {_TachoMeterPulse} at {DateTime.Now}",
            MessageType = LogType.Information.ToString(),
            DateTime = DateTime.Now,
        });
    }


    // İstasyona ulastıgında yapması gereken islemler
    public async Task CheckStationArrivalAsync(double ZeroSpeed, bool AllDoorsReleased)
    {

        if (!_isRouteActive || _currentStationIndex >= _stations.Count) return;

        var currentStation = _stations[_currentStationIndex];
        var nextStation = _stations[_currentStationIndex + 1];
        var distanceToStation = GetDistanceToNextStation(nextStation);
        //DDU ve Stretch lcd ye bilgi gönderildi
        await _lcdService.UpdateDisplay(new LcdInfo
        {
            NextStation = nextStation.stationName,
            RemainingDistance = Convert.ToInt32(distanceToStation)
        });
        if (ZeroSpeed == 0 && AllDoorsReleased == true)
        {
            Console.WriteLine($"İstasyona Ulasıldı {nextStation.stationName}");
            await _logService.InformationSendLogAsync(new InformationLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"İstasyona Ulasıldı : {nextStation.stationName} at {DateTime.Now}",
                MessageType = LogType.Information.ToString(),
                DateTime = DateTime.Now,
            });

            // Takometre sıfırlama
            await ResetTakoAsync();
            _currentStationIndex++;  // Bir sonraki istasyona geç

            // Bayrakları sıfırla
            _approachingAnnouncementMade = false;
            _arrivalAnnouncementMade = false;


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
        UpdateDisplays();
        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "Sonraki istasyona geciyor,DDU ve Stretch LCD ye bilgiler gönderildi",
            MessageType = LogType.Event.ToString(),
            DateTime = DateTime.Now,
            SourceIP = "10.3.156.224",
            DestinationIP = "10.3.156.55",
            DestinationName = "LCDService"
        });
    }

    // ** 2.  ilk istasyon ataması
    public async Task InitializeFirstStation()
    {

        // Eğer rota zaten başlatıldıysa tekrar sıfırlama!
        if (_currentStationIndex != 0) return;


        var currentStation = _stations[_currentStationIndex];

        // Başlangıç anonsu kontrolü
        if (currentStation.stationStartAnnounce && !_hasStartAnnouncementPlayed)
        {
            await _anonsService.PlayAnnouncementAsync(
                AnnouncementType.Start,
                currentStation.stationName!
            );
            _hasStartAnnouncementPlayed = true;
        }

        // LED ve LCD güncelleme
        UpdateDisplays();
        await _logService.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = "DDU ve Stretch LCD ye ilk atamalar yapıldı",
            MessageType = LogType.Event.ToString(),
            DateTime = DateTime.Now,
            SourceIP = "10.3.156.224",
            DestinationIP = "10.3.156.55",
            DestinationName = "LCDServiceSend"
        });
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

        // LCD güncelleme
        _lcdService.UpdateDisplay(new LcdInfo
        {
            NextStation = currentStation.stationName,
            RemainingDistance = Convert.ToInt32(distanceToNext),
            TotalDistance = Convert.ToInt32(nextStation.stationDistance)
        });

    }



    //** 4. İstasyon ilerleme . Bütün işleyişin oldugu fonksiyon
    private async Task CheckStationProgress()
    {


        //// 🚨 **Yeni Rota Kontrolü**: Eğer makinist yeni rota kurarsa, eskisini iptal et
        //if (await _routeService.IsRouteEstablishedAsync())
        //{
        //    Console.WriteLine("🚦 Yeni rota algılandı! Mevcut rota iptal ediliyor...");
        //    await RestartRouteAsync();
        //    return;  // Yeni rota başlatıldı, eski işlemleri durdur.
        //}




        //Rota kontrolü yapılıyorrrr
        if (_currentStationIndex >= _stations.Count)
        {
            Console.WriteLine("Rota bitti  veya yeni rota  bekleniyorrr.");
            await _logService.EventSendLogAsync(new EventLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = "Rota bitti  veya yeni rota  bekleniyorrr...",
                MessageType = LogType.Event.ToString(),
                DateTime = DateTime.Now,
                SourceIP = "10.3.156.224",
                DestinationIP = "10.3.156.55",
                DestinationName = "AnonsServisi"
            });

            _isRouteActive = false;
            return;
        }

        var currentStation = _stations[_currentStationIndex];
        var nextStation = _stations[_currentStationIndex + 1];
        int distanceToStation = GetDistanceToNextStation(currentStation);//Metraj hesaplama

        //Burada DDU ekranına kalan mesafe ve toplam mesafe gönderilicek



        // **1️⃣ Yaklaşma Anonsu (Önce Olmalı)**
        if (distanceToStation <= currentStation.stationApproachAnnounceDistance &&
            distanceToStation > currentStation.stationArrivalAnnounceDistance) // 🔹 500m - 150m arası
        {

            //    // Yaklaşma anonsu
            if (!_approachingAnnouncementMade)
            {
                await _anonsService.PlayAnnouncementAsync(AnnouncementType.Approaching,
                nextStation.stationName!);

                //_approachingAnnouncementMade = true;

                //Console.WriteLine("Yaklaşma anonsu yapıldı: {Station}", nextStation.stationName);
                _approachingAnnouncementMade = true;
            }

            await _ledService.UpdateDisplay(LedDisplayType.stationArrivalLed, nextStation.stationName!);

                await _lcdService.UpdateDisplay(new LcdInfo
                {
                    NextStation = nextStation.stationName,
                    RemainingDistance = Convert.ToInt32(distanceToStation)
                });

                //Log servisine gönderildi
                await _logService.EventSendLogAsync(new EventLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = "Anons yapıldı, DDU ve Stretch LCD ye bilgiler gönderildi",
                    MessageType = LogType.Event.ToString(),
                    DateTime = DateTime.Now,
                    SourceIP = "10.3.156.224",
                    DestinationIP = "10.3.156.55",
                    DestinationName = "AnonsServisi"
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
                nextStation.stationName!);
                //_arrivalAnnouncementMade = true;

                //Console.WriteLine("Varış anonsu yapıldı: {Station}", nextStation.stationName);


                ////Log servisine gönderildi
                //await _logService.EventSendLogAsync(new EventLogDto
                //{
                //    MessageSource = "LogicManager",
                //    MessageContent = "Anons yapıldı, Anons Servisine bilgiler gönderildi",
                //    MessageType = LogType.Event.ToString(),
                //    DateTime = DateTime.Now,
                //    SourceIP = "10.3.156.224",
                //    DestinationIP = "10.3.156.55",
                //    DestinationName = "AnonsServisi"
                //});

                _arrivalAnnouncementMade = true;

            }
            await _ledService.UpdateDisplay(LedDisplayType.stationArrivalLed, nextStation.stationName!);

                await _lcdService.UpdateDisplay(new LcdInfo
                {
                    NextStation = nextStation.stationName,
                    RemainingDistance = Convert.ToInt32(distanceToStation)
                });


                //Log servisine gönderildi
                await _logService.EventSendLogAsync(new EventLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = "Anons yapıldı, DDU ve Stretch LCD ye bilgiler gönderildi",
                    MessageType = LogType.Event.ToString(),
                    DateTime = DateTime.Now,
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
            await CheckStationArrivalAsync(ZeroSpeed, AllDoorsReleased);
        }
    }

    // Eğer makinist yeni bir rota kurarsa, mevcut rotayı iptal edip sıfırdan başlatacak.

    private async Task RestartRouteAsync()
    {
        Console.WriteLine("🔄 Yeni rota başlatılıyor...");

        _isRouteActive = false;
        _currentStationIndex = 0;
        _TachoMeterPulse = 0;

        // **Yeni rotayı al**
        var newRoute = await _routeService.GetAllRouteAsync();

        if (newRoute == null || !newRoute.Any())  // 🚨 Eğer yeni rota boşsa işlemi iptal et
        {
            Console.WriteLine("🚫 Yeni rota alınamadı, beklemeye geçiliyor...");
            return;
        }

        _stations = FilterAndCalculateSkipStations(newRoute);

        _isRouteActive = true;
        await InitializeFirstStation();
    }


    ////Rota bitince yeni rota bekliyor modu test edilicek
    //private async Task CompleteRouteAsync()
    //{
    //    _isRouteActive = false;
    //    Console.WriteLine("🚆 Rota tamamlandı! Yeni rota bekleniyor...");

    //    // Yeni rota gelene kadar bekleme moduna geç
    //    while (!await _routeService.IsRouteEstablishedAsync())
    //    {
    //        Console.WriteLine("🚦 Yeni rota bekleniyor...");
    //        await Task.Delay(2000);
    //    }

    //    Console.WriteLine("✅ Yeni rota bulundu! Başlatılıyor...");
    //    await RestartRouteAsync();
    //}




}



