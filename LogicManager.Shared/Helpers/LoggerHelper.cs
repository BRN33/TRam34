using LogicManager.Shared.DTOs;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using LogicManager.Persistence.Interfaces;
using LogicManager.Persistence.Models;


namespace LogicManager.Shared.Helpers;

public class LoggerHelper
{
    private readonly IServiceScope _serviceProvider; //Dependency Injection yapma için 
    private readonly IMongoDbService _mongoDbService;
    private readonly IConfiguration _configuration; // appsettings.json dan veri cekmek icin
    private readonly HttpClient _httpClient;
    private static readonly string logFilePath = "log.txt";
    private static readonly long maxFileSize = 1 * 1024 * 1024; // 

    public LoggerHelper(IConfiguration configuration, HttpClient httpClient, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider.CreateScope();
        _mongoDbService = _serviceProvider.ServiceProvider.GetRequiredService<IMongoDbService>();
        _configuration = configuration;
        _httpClient = httpClient;
    }

    private string GetEndpoint(string logType)//appsettings.json dan LogEndpoints altındaki endpointleri cekmek icin
    {
        var endpoint = _configuration.GetSection("LogEndpoints").GetSection(logType).Value!;// "!"  işareti null gelmeyeceğini belirtiyor

        Console.WriteLine(endpoint);
        if (string.IsNullOrEmpty(endpoint))
        {
            LogError($"Endpoint for log type '{logType}' not found.");
        }
        return endpoint;
    }


    public async Task AlarmSendLogAsync(AlarmLogDto alarmLog)///Alarm Hatası göndermek icin metot
    {
        try
        {
            ////string trainId = await _someOtherService.GetTrainIdAsync(); // Servisten TrainId al
            await SendLogByTypeAsync("Train 7", LogType.Alarm, alarmLog);

            //var ybsPc = await _mongoDbService.GetHardwareByNameAsync("YBS PC");
            //var ybsIp = ybsPc?.ip;

            //var logger = await _mongoDbService.GetSoftwareByNameAsync("Logger");
            //var loggerPort = logger?.Port; // 3404

            //var endpoint = GetEndpoint(LogType.Alarm.ToString());
            //await SendLogAsync(endpoint, alarmLog);

        }
        catch (Exception)
        {

            LogError("Alarm ElasticSearh Veritabanı baglantısı yok...");
        }

    }

    public async Task ErrorSendLogAsync(ErrorLogDto errorLog)///Error Hatası göndermek icin metot
    {
        try
        {
            ////string trainId = await _someOtherService.GetTrainIdAsync(); // Servisten TrainId al
            await SendLogByTypeAsync("Train 7", LogType.Error, errorLog);

            //var endpoint = GetEndpoint(LogType.Error.ToString());
            //await SendLogAsync(endpoint, errorLog);
        }
        catch (Exception)
        {

            LogError("Error ElasticSearh Veritabanı baglantısı yok...");
        }

    }

    public async Task EventSendLogAsync(EventLogDto eventLog)///Event Hatası göndermek icin metot
    {
        try
        {
            ////string trainId = await _someOtherService.GetTrainIdAsync(); // Servisten TrainId al
            await SendLogByTypeAsync("Train 7", LogType.Event, eventLog);


            //var ybsPcc = await _mongoDbService.GetTrainConfigurationAsync("Train 7");
            //var deneip = ybsPcc.Hardware?.FirstOrDefault(h=>h.Name== "SIP Server")?.ip!;
            ////var ybsPc = await _mongoDbService.GetHardwareByNameAsync("YBS PC");
            ////var ybsIp = ybsPc?.ip;

            //var denePort = ybsPcc.Software?.FirstOrDefault(h => h.Name == "Logger")?.Port!;

            ////var logger = await _mongoDbService.GetSoftwareByNameAsync("Logger");
            ////var loggerPort = logger?.Port; // 3404

            //// Endpoint oluştur
            //string endpoint = $"http://{deneip}:{denePort}/api/Producer/event";

            ////var endpoint = GetEndpoint(LogType.Event.ToString());
            //await SendLogAsync(endpoint, eventLog);
            Console.WriteLine("Event Bilgisi ElasticSearh Veritabanına Basarılı Bir Şekilde Gönderildi...");

        }
        catch (Exception)
        {

            LogError("Event ElasticSearh Veritabanı baglantısı yok...");
        }

    }

    public async Task InformationSendLogAsync(InformationLogDto informationLog)///Information Hatası göndermek icin metot
    {
        try
        {
            ////string trainId = await _someOtherService.GetTrainIdAsync(); // Servisten TrainId al
            await SendLogByTypeAsync("Train 7", LogType.Information, informationLog);

            //var endpoint = GetEndpoint(LogType.Information.ToString());
            //await SendLogAsync(endpoint, informationLog);
            //Console.WriteLine(endpoint.ToString(), informationLog.ToString());

        }
        catch (Exception)
        {

            LogError("Information ElasticSearh Veritabanı baglantısı yok...");
        }

    }

    public async Task WarningSendLogAsync(WarningLogDto warningLog)///Warning Hatası göndermek icin metot
    {
        try
        {
            ////string trainId = await _someOtherService.GetTrainIdAsync(); // Servisten TrainId al
            await SendLogByTypeAsync("Train 7", LogType.Warning, warningLog);

            //var endpoint = GetEndpoint(LogType.Warning.ToString());
            //await SendLogAsync(endpoint, warningLog);

        }
        catch (Exception)
        {

            LogError("Warning ElasticSearh Veritabanı baglantısı yok...");
        }

    }

    private async Task SendLogAsync(string endpointType, object logDto)///Bütün metotları HttpPost atmak icin
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Log Type URL: {endpointType}");
        Console.WriteLine($"Serialized DTO: {JsonConvert.SerializeObject(logDto)}");
        Console.ResetColor();
        //var jsonContent = (JsonConvert.SerializeObject(logDto), Encoding.UTF8, "application/json");

        //await _httpClient.PostAsJsonAsync(endpointType, jsonContent);
        //var response = await _httpClient.PostAsJsonAsync(endpointType, logDto);

        var responseTask = _httpClient.PostAsJsonAsync(endpointType, logDto);

        // ... saniye içinde tamamlanmazsa iptal et
        if (await Task.WhenAny(responseTask, Task.Delay(100)) == responseTask)
        {
            var response = await responseTask;
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                LogError($"Failed to send log. StatusCode: {response.StatusCode}, Reason: {response.ReasonPhrase}, Error: {errorContent}");
            }
        }
        else
        {
            LogError("Log gönderme işlemi zaman aşımına uğradı! Baglantıları kontrol ediniz!!!");
        }
    }
    private static void LogError(string message)
    {
        // Log dosyasının boyutunu kontrol et
        if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length > maxFileSize)
        {
            File.Delete(logFilePath); // Büyükse sil
        }

        // Log kaydı ekleme
        string logMessage = $"{DateTime.Now}: {message}";
        File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
    }


    private async Task SendLogByTypeAsync(string trainId, LogType logType, object eventLog)
    {
        try
        {
            var trainConfig = await _mongoDbService.GetTrainConfigurationAsync(trainId);
            if (trainConfig == null)
            {
                Console.WriteLine($"Train configuration bulunamadı! Train ID: {trainId}");
                return;
            }

            var deneIp = trainConfig.Software?.FirstOrDefault(h => h.Name == "LoggerLinuxServer")?.ip;
            if (string.IsNullOrEmpty(deneIp))
            {
                Console.WriteLine("SIP Server için IP bulunamadı!");
                return;
            }

            var denePort = trainConfig.Software?.FirstOrDefault(s => s.Name == "LoggerLinuxServer")?.Port;
            if (denePort == null)
            {
                Console.WriteLine("Logger için Port bulunamadı!");
                return;
            }

            string endpoint = $"http://{deneIp}:{denePort}/api/Producer/{logType}";

            await SendLogAsync(endpoint, eventLog);
            Console.WriteLine($"{logType} Log bilgisi ElasticSearch Veritabanına başarılı bir şekilde gönderildi...");
        }
        catch (Exception ex)
        {
            LogError($"Log gönderilirken hata oluştu: {ex.Message}");
        }
    }


}
