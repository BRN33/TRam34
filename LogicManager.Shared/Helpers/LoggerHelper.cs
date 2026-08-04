using LogicManager.Shared.DTOs;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LogicManager.Persistence.Interfaces;
using LogicManager.Persistence.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogicManager.Shared.Helpers;

public class LoggerHelper
{
    private readonly IServiceScope _serviceProvider;
    private readonly IMongoDbService _mongoDbService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoggerHelper> _logger;
    private readonly HttpClient _httpClient;
    //private static readonly string logFilePath = "log.txt";
    //private static readonly long maxFileSize = 1 * 1024 * 1024;
    private readonly Dictionary<string, TrainConfiguration> _trainConfigurations = new Dictionary<string, TrainConfiguration>();
    private string _trainId;

    // YENİ ALAN: AppSettings'ten okunan log ayarları
    private readonly LogEndpointSettings _logSettings;

    public LoggerHelper(IConfiguration configuration, HttpClient httpClient, IServiceProvider serviceProvider, ILogger<LoggerHelper> logger, IOptions<LogEndpointSettings> logSettings)
    {
        _serviceProvider = serviceProvider.CreateScope();
        _mongoDbService = _serviceProvider.ServiceProvider.GetRequiredService<IMongoDbService>();
        _configuration = configuration;
        _httpClient = httpClient;
        //_trainId = _configuration["MongoDb:TrainId"]!;//_configuration.GetSection("MongoDb").GetSection("TrainId").Value!;
        _trainId = "";
        _logger = logger;

        // Ayarların değerini alıyoruz
        _logSettings = logSettings.Value;

    }


    public async Task AlarmSendLogAsync(AlarmLogDto alarmLog) => await SendLogByTypeAsync(LogType.Alarm, alarmLog);
    public async Task ErrorSendLogAsync(ErrorLogDto errorLog) => await SendLogByTypeAsync(LogType.Error, errorLog);
    public async Task EventSendLogAsync(EventLogDto eventLog) => await SendLogByTypeAsync(LogType.Event, eventLog);
    public async Task InformationSendLogAsync(InformationLogDto informationLog) => await SendLogByTypeAsync(LogType.Information, informationLog);
    public async Task WarningSendLogAsync(WarningLogDto warningLog) => await SendLogByTypeAsync(LogType.Warning, warningLog);


    private void SendLogAsync(string endpointType, object logDto)
    {
        //using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)); // 50 milisaniye zaman aşımı
        Task.Run(async () =>
        {
            try
            {
                await _httpClient.PostAsJsonAsync(endpointType, logDto);
                //if (!response.IsSuccessStatusCode)
                //{
                //    var errorContent = await response.Content.ReadAsStringAsync();
                //    _logger.LogError($"Failed to send log. StatusCode: {response.StatusCode}, Reason: {response.ReasonPhrase}, Error: {errorContent}");
                //}
                _logger.LogInformation($"{endpointType} Log bilgisi ElasticSearch Veritabanına başarılı bir şekilde gönderildi...");
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Log gönderme işlemi zaman aşımına uğradı! Bağlantıları kontrol ediniz");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Log gönderilirken hata oluştu: {ex.Message}");
            }
        });

    }

    //private void LogError(string message)
    //{
    //    string logMessage = $"{DateTime.Now}: {message}";
    //    _logWriter?.WriteLine(logMessage);
    //    _currentFileSize += logMessage.Length + Environment.NewLine.Length;

    //    if (_currentFileSize > maxFileSize)
    //    {
    //        _logWriter?.Close();
    //        File.Delete(logFilePath);
    //        InitializeLogWriter();
    //        _currentFileSize = 0;
    //    }
    //}

    private async Task SendLogByTypeAsync(LogType logType, object eventLog)
    {

        string messageContent = string.Empty;
        //  Kontrol Mantığı: Log tipi kapalı olsa BİLE mesaj "IMALIVE" ise devam et
        bool isImAlive = messageContent == "IMALIVE";

        // *** Kontrol Mantığı Buraya Eklendi ***
        if (!IsLoggingEnabled(logType) && !isImAlive) // Ayar kapalıysa VE IMALIVE değilse engelle
        {
            _logger.LogDebug($"Log tipi {logType} için ayarlar nedeniyle gönderim iptal edildi.");
            return;
        }
        // ************************************


        try
        {


            var trainConfig = await _mongoDbService.GetTrainConfigurationAsync();
            if (trainConfig == null)
            {
                _logger.LogWarning($"Train configuration bulunamadı! Train ID: {trainConfig}");
                return;
            }



            var deneIp = trainConfig.Software?.FirstOrDefault(h => h.Name == "LoggerLinuxServer")?.ip;
            if (string.IsNullOrEmpty(deneIp))
            {
                _logger.LogWarning("SIP Server için IP bulunamadı!");
                return;
            }

            var denePort = trainConfig.Software?.FirstOrDefault(s => s.Name == "LoggerLinuxServer")?.Port;
            if (denePort == null)
            {
                _logger.LogWarning("Logger için Port bulunamadı!");
                return;
            }

            string endpoint = $"http://{deneIp}:{denePort}/api/Producer/{logType}";

             SendLogAsync(endpoint, eventLog);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Log gönderilirken hata oluştu: {ex.Message}");
        }
    }


    // *** YENİ: Log yazma iznini kontrol eden yardımcı metot ***
    public bool IsLoggingEnabled(LogType logType)
    {
        // Gelen log tipine göre LogEndpointSettings nesnesindeki ilgili bool değeri döndürür.
        return logType switch
        {
            LogType.Alarm => _logSettings.Alarm,
            LogType.Error => _logSettings.Error,
            LogType.Event => _logSettings.Event,
            LogType.Information => _logSettings.Information,
            LogType.Warning => _logSettings.Warning,
            _ => true // Tanımsız bir LogType gelirse, varsayılan olarak logla
        };
    }


    public void SetTrainId(string trainId)
    {
        _trainId = trainId;
    }
    //public void Dispose()
    //{
    //    _logWriter?.Dispose();
    //    _serviceProvider?.Dispose();
    //}
}