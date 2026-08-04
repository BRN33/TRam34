using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Infrastructure.Services;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Shared;
using System.Text.Json;

namespace LogicManager.Application.Features.Tako;

public class TakoDataCommand : BackgroundService
{
    private readonly IServiceScope _serviceProvider;//DI entegrasyonu için ServiceProvider 
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly IConfiguration _configuration;

    private readonly LoggerHelper _logService;
    private readonly ILedService _ledService;
    private IRouteService _routeService;

    private readonly ITrainManagement _trainManagement;
    private readonly ITakoReaderService _takoReaderService;

    public bool isWaitingLogged = false;




    public TakoDataCommand(IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
    {
        //_logService = logService;
        //_serviceProvider = serviceProvider.CreateScope();
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;

        //_trainManagement = _serviceProvider.ServiceProvider.GetRequiredService<ITrainManagement>();
        //_takoReaderService = _serviceProvider.ServiceProvider.GetRequiredService<ITakoReaderService>();
        //_ledService = _serviceProvider.ServiceProvider.GetRequiredService<ILedService>();
        //_routeService = _serviceProvider.ServiceProvider.GetRequiredService<IRouteService>();


    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = _serviceScopeFactory.CreateScope()) // Yeni scope oluştur
        {


            var imAliveIntervalSeconds = _configuration.GetValue<int>("LogAliveStatus:ImAliveIntervalSeconds");

            // Geçerli bir değer okunamadıysa veya 0'dan küçükse varsayılan bir değer kullan
            if (imAliveIntervalSeconds <= 0)
            {
                imAliveIntervalSeconds = 10; // Varsayılan 10 saniye
                Console.WriteLine("Uyarı: appsettings.json'da geçerli ImAliveIntervalSeconds bulunamadı. Varsayılan 10 saniye kullanılıyor.");
            }

            var imAliveInterval = TimeSpan.FromSeconds(imAliveIntervalSeconds);
            DateTime lastImAliveLogTime = DateTime.MinValue;



            var trainManagement = scope.ServiceProvider.GetRequiredService<ITrainManagement>();
            var routeService = scope.ServiceProvider.GetRequiredService<IRouteService>();
            var logService = scope.ServiceProvider.GetRequiredService<LoggerHelper>(); // LogService'i al
            var _tcmsService = scope.ServiceProvider.GetRequiredService<ITcmsService>(); // LeadershipManager'i al
            var _trainCoupledService = scope.ServiceProvider.GetRequiredService<ITrainCoupledService>(); // LeadershipManager'i al
            while (!stoppingToken.IsCancellationRequested)
            {

                try
                {

                    var currentTime = DateTime.Now;

                    // 1. Dışarıdan okunan ayara göre IMALIVE LOGU kontrolü
                    if ((currentTime - lastImAliveLogTime) >= imAliveInterval)
                    {
                        await logService.InformationSendLogAsync(new InformationLogDto
                        {
                            MessageSource = "LogicManager",
                            MessageContent = "IMALIVE",
                            MessageType = LogType.Information.ToString(),
                            DateTime = currentTime
                        });
                        lastImAliveLogTime = currentTime;
                    }


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

                    // ✅ Artık sürekli sorgulamak yerine, rota gelmesini bekleyeceğiz
                    if (!trainManagement.IsRouteActive)
                    {
                        //var currentTime = DateTime.Now;
                        var consoleText = ">>> Kara Tren Rotası Bekleniyor";
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(consoleText + currentTime);
                        Console.ResetColor();
                        if (!isWaitingLogged)
                        {
                            await logService.InformationSendLogAsync(new InformationLogDto
                            {
                                MessageSource = "LogicManager",
                                MessageContent = $"400 - Rota Kurulması İçin Bekleniyor.",
                                MessageType = LogType.Information.ToString(),
                                DateTime = currentTime
                            });
                            isWaitingLogged = true; // Log atıldı olarak işaretle
                        };


                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    // --- EĞER KOD BURAYA GELMİŞSE ROTA VAR DEMEKTİR ---
                    // Burada değişkeni sıfırlıyoruz ki rota tekrar bozulursa yukarıdaki IF bloğu tekrar log atabilsin.
                    if (isWaitingLogged)
                    {
                        // İsteğe bağlı: Rota tekrar kuruldu logu da atabilirsiniz
                        isWaitingLogged = false;
                    }

                    // ✅ Tako başladığında işlemi ilerlet
                    await trainManagement.ReadAndProcessTakoAsync();
                    // **4. Küçük bir bekleme ekleyerek işlem döngüsünü stabilize et**
                    await Task.Delay(100, stoppingToken);

                    //// **TCMS ile senkron hale gelmek için dinamik bekleme süresi kullan**
                    //await Task.Delay(_updateInterval, stoppingToken);


                }
                catch (Exception ex)
                {
                    Console.WriteLine("Rota işleme hatası....................");

                    var currentTime = DateTime.Now;
                    await _logService.ErrorSendLogAsync(new ErrorLogDto
                    {
                        MessageSource = "LogicManager",
                        MessageContent = $"462 - RabbitMQ Baglantısında veya Diğer Bağlantılarda Bir Problem Var: {ex.Message}",
                        MessageType = LogType.Error.ToString(),
                        DateTime = currentTime,
                        MessageSourceType = "Software",
                        HardwareIP = "127.0.0.1" ?? "Unknown"
                    });
                    await Task.Delay(5000, stoppingToken);
                }

            }
        }
    }

}

