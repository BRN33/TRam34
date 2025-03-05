using LogicManager.Shared.DTOs;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LogicManager.Persistence.Interfaces;
using LogicManager.Persistence.Models;
using Microsoft.Extensions.Logging;

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
    private readonly string _trainId;

    //private static long _currentFileSize;

    public LoggerHelper(IConfiguration configuration, HttpClient httpClient, IServiceProvider serviceProvider, ILogger<LoggerHelper> logger)
    {
        _serviceProvider = serviceProvider.CreateScope();
        _mongoDbService = _serviceProvider.ServiceProvider.GetRequiredService<IMongoDbService>();
        _configuration = configuration;
        _httpClient = httpClient;
        _trainId = _configuration["MongoDb:TrainId"]!;//_configuration.GetSection("MongoDb").GetSection("TrainId").Value!;
        _logger = logger;

    }


    public async Task AlarmSendLogAsync(AlarmLogDto alarmLog) => await SendLogByTypeAsync(LogType.Alarm, alarmLog);
    public async Task ErrorSendLogAsync(ErrorLogDto errorLog) => await SendLogByTypeAsync(LogType.Error, errorLog);
    public async Task EventSendLogAsync(EventLogDto eventLog) => await SendLogByTypeAsync(LogType.Event, eventLog);
    public async Task InformationSendLogAsync(InformationLogDto informationLog) => await SendLogByTypeAsync(LogType.Information, informationLog);
    public async Task WarningSendLogAsync(WarningLogDto warningLog) => await SendLogByTypeAsync(LogType.Warning, warningLog);


    private async Task SendLogAsync(string endpointType, object logDto)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)); // 100 milisaniye zaman aşımı
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpointType, logDto, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to send log. StatusCode: {response.StatusCode}, Reason: {response.ReasonPhrase}, Error: {errorContent}");
            }
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

            await SendLogAsync(endpoint, eventLog);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Log gönderilirken hata oluştu: {ex.Message}");
        }
    }

    //public void Dispose()
    //{
    //    _logWriter?.Dispose();
    //    _serviceProvider?.Dispose();
    //}
}