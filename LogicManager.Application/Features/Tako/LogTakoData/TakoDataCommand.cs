using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Infrastructure.Services;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace LogicManager.Application.Features.Tako;

public class TakoDataCommand : BackgroundService
{

    private readonly IServiceScope _serviceProvider;//DI entegrasyonu için ServiceProvider 

    private readonly LoggerHelper _logService;
    private readonly ILedService _ledService;
    private IRouteService _routeService;

    //private readonly LogicManager.Infrastructure.Services.TrainManagement _trainManagement;
    private readonly ITrainManagement _trainManagement;
    private readonly ITakoReaderService _takoReaderService; 
    private readonly LeadershipManager _leadershipManager;

    public List<Station> ?_stations;
    public static int _globalTakoValue = 0;
    public static string _globalLedValue = "YENİMAHALLE";
    public TakoDataCommand(LoggerHelper logService, LeadershipManager leadershipManager,IServiceProvider serviceProvider, ITrainManagement trainManagement)
    {
        _leadershipManager = leadershipManager;
        _logService = logService;
        _serviceProvider = serviceProvider.CreateScope();
        _trainManagement = _serviceProvider.ServiceProvider.GetRequiredService<ITrainManagement>();
        _takoReaderService = _serviceProvider.ServiceProvider.GetRequiredService<ITakoReaderService>();
        _ledService = _serviceProvider.ServiceProvider.GetRequiredService<ILedService>();
        _routeService = _serviceProvider.ServiceProvider.GetRequiredService<IRouteService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {

            try
            {

                //// **1. Lider tren mi kontrol et**
                //bool isLeader = await _leadershipManager.CheckIfLeaderAsync();
                //if (!isLeader)
                //{
                //    Console.ForegroundColor = ConsoleColor.Green;
                //    Console.WriteLine("Bu tren lider değil, beklemeye geçiyorum...");
                //    Console.ResetColor();
                //    await _logService.InformationSendLogAsync(new InformationLogDto
                //    {
                //        MessageSource = "LogicManager",
                //        MessageContent = $"TCMS den Master-Slave verisi okundu : {isLeader} at {DateTime.Now}",
                //        MessageType = LogType.Information.ToString(),
                //        DateTime = DateTime.Now,
                //    });

                //    await Task.Delay(2000, stoppingToken);
                //    continue;
                //}



                ////// **1. Rota bilgisi alınıncaya kadar bekle**
                //while (!await _routeService.IsRouteEstablishedAsync())
                //{
                //    Console.WriteLine(">...>...>...Rota kurulması bekleniyor >...>...>...");
                //    await Task.Delay(2000, stoppingToken);
                //}

                // **2. Rota bilgisi al**
                await _trainManagement.CheckAndInitializeRouteAsync();



                // **3. Tako verisini oku**
                await _trainManagement.ReadAndProcessTakoAsync();




                // **4. Küçük bir bekleme ekleyerek işlem döngüsünü stabilize et**
                await Task.Delay(100, stoppingToken);



                //var listeningTasks =  _trainManagement.CheckAndInitializeRouteAsync();
                //await Task.WhenAll(listeningTasks); // *Burada bir kere başlatıyoruz*



            }
            catch (Exception)
            {
                Console.WriteLine("Rota işleme hatası....................");
                await Task.Delay(5000, stoppingToken);
            }



            ////ikinci  testler
            //await _trainManagement.ReadAndProcessTakoAsync();
            //await Task.Delay(100); // 100 ms bekleyerek döngüyü hızlandır veya yavaşlat



            //ilk deneme testleri
            //await DoWork(stoppingToken);
        }
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        try
        {

            //await RabbitMQHelperAsync.SendMessageToExchangeAsync(RabbitMQConstants.TakoReadExchangeName, _globalLedValue);
            Console.WriteLine("RabbitMQ ye giden deger : " + _globalLedValue);



            var routeStations = await _routeService.GetAllRouteAsync();

            foreach (var station in routeStations)
            {
                Console.WriteLine($"İstasyon Adı: {station.stationName}, Mesafe: {station.stationDistance}");
            }



            try
            {
                if (await _takoReaderService.ReadTakoPulseAsync() == 1)
                {
                    _globalTakoValue = Convert.ToInt32(_trainManagement.CalculateDistance(_globalTakoValue));

                }

                Console.WriteLine("RabbitMQ ye giden deger : " + _globalTakoValue);
                //RabbitMQHelper.SendMessageToExchange(RabbitMQConstants.TakoReadExchangeName, Digital_Tako_Data);
                //await RabbitMQHelperAsync.SendMessageToExchangeAsync(RabbitMQConstants.TakoReadExchangeName, "cebeci");

                //LED e veri gönderme             
                List<string> randomStrings = GetRandomStringsFromList();

                foreach (string randomString in randomStrings)
                {

                    await _ledService.UpdateDisplay(LedDisplayType.stationTerminalLed, randomString);

                    await Task.Delay(10); // 1 saniye bekle
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RabbitMQ gönderim hatası: {ex.Message}");

                var errorLog = new ErrorLogDto
                {
                    MessageSource = "RAbbitMQ",
                    MessageContent = "RabbitMQ baglantısı yok  Hüseyin...",
                    MessageType = LogType.Error.ToString(),
                    DateTime = DateTime.UtcNow,
                    ErrorType = LogType.Error.ToString(),
                    HardwareIP = "10.10.10.1"
                };
                await _logService.ErrorSendLogAsync(errorLog);
            }



            try
            {
                var informationLog = new InformationLogDto
                {
                    MessageSource = "PUMPis",
                    MessageContent = $"Kadayıf Dolmaasııı:{_globalTakoValue}",
                    MessageType = LogType.Information.ToString(),
                    DateTime = DateTime.UtcNow
                };

                var alarmLog = new AlarmLogDto
                {
                    MessageSource = "PUMPis",
                    MessageContent = $"Büryannnn:{_globalTakoValue}",
                    MessageType = LogType.Alarm.ToString(),
                    DateTime = DateTime.UtcNow,
                    AlarmType = LogType.Alarm.ToString(),
                    HardwareIP = "10.10.10.1"
                };
                var errorLog = new ErrorLogDto
                {
                    MessageSource = "PUMPis",
                    MessageContent = $"Şırdanncı Hüseyin:{_globalTakoValue}",
                    MessageType = LogType.Error.ToString(),
                    DateTime = DateTime.UtcNow,
                    ErrorType = LogType.Error.ToString(),
                    HardwareIP = "10.10.10.1"
                };
                var eventLog = new EventLogDto
                {
                    MessageSource = "PUMPis",
                    MessageContent = $"Kavurmacııı:{_globalTakoValue}",
                    MessageType = LogType.Event.ToString(),
                    DateTime = DateTime.UtcNow,
                    SourceIP = "10.10.1.2",
                    DestinationName = LogType.Event.ToString(),
                    DestinationIP = "10.10.10.1"
                };
                var warningLog = new WarningLogDto
                {
                    MessageSource = "PUMPis",
                    MessageContent = $"Ne halin varsa gor Hüseyin:{_globalTakoValue}",
                    MessageType = LogType.Warning.ToString(),
                    DateTime = DateTime.UtcNow,
                    WarningType = LogType.Warning.ToString(),
                    HardwareIP = "10.10.10.1"
                };

                //Console.WriteLine($"Giden dto: {System.Text.Json.JsonSerializer.Serialize(informationLog)}");

                //await _logService.InformationSendLogAsync(informationLog);
                //await _logService.AlarmSendLogAsync(alarmLog);
                //await _logService.ErrorSendLogAsync(errorLog);
                //await _logService.EventSendLogAsync(eventLog);
                //await _logService.WarningSendLogAsync(warningLog);
                //Console.WriteLine("Log başarıyla gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log gönderimi sırasında bir hata oluştu: {ex.Message}");
            }


            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {

            Console.WriteLine($"Bir hata olustu.{ex.Message}");
        }

    }


    public static List<string> GetRandomStringsFromList()
    {
        // JSON'dan veri okuma örneği (Newtonsoft.Json kullanımı)
        string jsonPath = "D:\\Arge Projeler\\TTRam34\\LogicManager\\LogicManager.API\\led.json";
        string jsonString = File.ReadAllText(jsonPath);
        List<string> stringList = JsonConvert.DeserializeObject<List<string>>(jsonString)!;

        // Listeyi karıştırma
        Random random = new Random();
        stringList = stringList.OrderBy(item => random.Next()).ToList();

        return stringList;
    }
}

