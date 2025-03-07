using LogicManager.Infrastructure.Interfaces;
using LogicManager.Infrastructure.Services;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LogicManager.Application.Features.Tako;

public class TakoDataCommand : BackgroundService
{
    private readonly IServiceScope _serviceProvider;//DI entegrasyonu için ServiceProvider 
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly LoggerHelper _logService;
    private readonly ILedService _ledService;
    private IRouteService _routeService;

    private readonly ITrainManagement _trainManagement;
    private readonly ITakoReaderService _takoReaderService;
    private readonly LeadershipManager _leadershipManager;

    private DateTime _lastTcmsUpdateTime = DateTime.Now;
    private int _updateInterval = 100;  // Varsayılan 100 ms
    private double _lastZeroSpeed = 0;
    private bool _lastAllDoorsReleased = false;



    public TakoDataCommand(LoggerHelper logService, LeadershipManager leadershipManager, IServiceScopeFactory serviceScopeFactory)
    {
        _leadershipManager = leadershipManager;
        _logService = logService;
        //_serviceProvider = serviceProvider.CreateScope();
        _serviceScopeFactory = serviceScopeFactory;

        //_trainManagement = _serviceProvider.ServiceProvider.GetRequiredService<ITrainManagement>();
        //_takoReaderService = _serviceProvider.ServiceProvider.GetRequiredService<ITakoReaderService>();
        //_ledService = _serviceProvider.ServiceProvider.GetRequiredService<ILedService>();
        //_routeService = _serviceProvider.ServiceProvider.GetRequiredService<IRouteService>();


    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceScopeFactory.CreateScope()) // Yeni scope oluştur
            {
                var trainManagement = scope.ServiceProvider.GetRequiredService<ITrainManagement>();
                var takoReaderService = scope.ServiceProvider.GetRequiredService<ITakoReaderService>();
                var routeService = scope.ServiceProvider.GetRequiredService<IRouteService>();

                try
                {

                    ////// **1. Lider tren mi kontrol et**
                    ////bool isLeader = await _leadershipManager.CheckIfLeaderAsync();
                    ////if (!isLeader)
                    ////{
                    ////    Console.ForegroundColor = ConsoleColor.Green;
                    ////    Console.WriteLine("Bu tren lider değil, beklemeye geçiyorum...");
                    ////    Console.ResetColor();
                    ////    await _logService.InformationSendLogAsync(new InformationLogDto
                    ////    {
                    ////        MessageSource = "LogicManager",
                    ////        MessageContent = $"TCMS den Master-Slave verisi okundu : {isLeader} at {DateTime.Now}",
                    ////        MessageType = LogType.Information.ToString(),
                    ////        DateTime = DateTime.Now,
                    ////    });

                    ////    await Task.Delay(2000, stoppingToken);
                    ////    continue;
                    ////}



                    //////// **1. Rota bilgisi alınıncaya kadar bekle**
                    ////while (!await _routeService.IsRouteEstablishedAsync())
                    ////{
                    ////    Console.WriteLine(">...>...>...Rota kurulması bekleniyor >...>...>...");
                    ////    await Task.Delay(2000, stoppingToken);
                    ////}

                    //// **2. Rota bilgisi al**
                    //await _trainManagement.CheckAndInitializeRouteAsync();



                    //// **3. Tako verisini oku**
                    //await _trainManagement.ReadAndProcessTakoAsync();




                    // ✅ Artık sürekli sorgulamak yerine, rota gelmesini bekleyeceğiz
                    if (!trainManagement.IsRouteActive)
                    {
                        var currentTime = DateTime.Now;
                        var consoleText = ">>> Kara Tren Rotası Bekleniyor ...: ";
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(consoleText + currentTime);
                        Console.ResetColor();

                        await _logService.InformationSendLogAsync(new InformationLogDto
                        {
                            MessageSource = "LogicManager",
                            MessageContent = $"Rota kurulması bekleniyor {currentTime}",
                            MessageType = LogType.Information.ToString(),
                            DateTime = currentTime
                        });
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    // ✅ Tako başladığında işlemi ilerlet
                    await trainManagement.ReadAndProcessTakoAsync();
                    // **4. Küçük bir bekleme ekleyerek işlem döngüsünü stabilize et**
                    await Task.Delay(100, stoppingToken);

                    //// **TCMS ile senkron hale gelmek için dinamik bekleme süresi kullan**
                    //await Task.Delay(_updateInterval, stoppingToken);


                }
                catch (Exception)
                {
                    Console.WriteLine("Rota işleme hatası....................");
                    await Task.Delay(5000, stoppingToken);
                }

            }
        }
    }


    public void UpdateTcmsData(double zeroSpeed, bool allDoorsReleased)
    {
        _lastZeroSpeed = zeroSpeed;
        _lastAllDoorsReleased = allDoorsReleased;

        // En son TCMS güncelleme zamanını kaydet
        var now = DateTime.Now;
        _updateInterval = (int)(now - _lastTcmsUpdateTime).TotalMilliseconds;
        _lastTcmsUpdateTime = now;

        // Eğer TCMS'den veri gelmiyorsa minimum gecikmeyi koru
        if (_updateInterval < 100) _updateInterval = 100;
        if (_updateInterval > 500) _updateInterval = 500;
    }


}

