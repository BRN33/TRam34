using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Shared;
using TRAM34_DDU.Core.Application.Interfaces.Services;
using TRAM34_DDU.Core.Application.RabbitMQService;

namespace LogicManager.Infrastructure.Services;

public class LeadershipManager 
{

    private readonly ITcmsService _tcmsService;
    private readonly LoggerHelper _logService;
    private readonly IConfiguration _configuration; // appsettings.json dan veri cekmek icin
    private readonly IRabbitService RabbitMQService;

    public LeadershipManager(IConfiguration configuration, LoggerHelper logger,IRabbitService rabbitService)
    {
        _configuration = configuration;
        _logService = logger;
        RabbitMQService = rabbitService;

    }

    public event Action<TcmsData>? OnTakoDataUpdated;

    public async Task<bool> CheckIfLeaderAsync()
    {
        //// Liderlik kontrol algoritması (örneğin TCMS'den bilgi al)
        //var tcmsData = await _tcmsService.GetTcmsDataAsync();
        //isLeader=tcmsData.IsMaster;
        var endpoint = _configuration.GetSection("TcmsSettings:isLider");// "!"  işareti null gelmeyeceğini belirtiyor

        bool isLeader = Convert.ToBoolean(endpoint.Value);

        // Liderlik bilgisi RabbitMQ'ya gönderiliyor
        //  RabbitMQHelper.PublishMessage(
        //    RabbitMQConstants.RabbitMQHost,
        //    RabbitMQConstants.RotaExchangeName,
        //    ExchangeType.Fanout,
        //    "",
        //    isLeader
        //);
        await RabbitMQService.PublishMessage(
                       RabbitMQConstants.RabbitMQHost,
                                  RabbitMQConstants.LeadExchangeName,
                                             ExchangeType.Fanout,
                                                        "",
                                                                   isLeader,
                                                                              ManagementEnum.Live
                                                                                     );

        await _logService.InformationSendLogAsync(new InformationLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"TCMS den Master-Slave verisi okundu : {isLeader} at {DateTime.Now}",
            MessageType = LogType.Information.ToString(),
            DateTime = DateTime.Now,
        });

        Console.WriteLine($"Liderlik Durumu: {(isLeader ? "Lider" : "Takipçi")}");
        return isLeader;
    }

    

    public Task<TcmsData> GetTcmsDataAsync()
    {
        return _tcmsService.GetTcmsDataAsync();
    }

    public Task<bool> IsConnectedAsync()
    {
        return _tcmsService.IsConnectedAsync();
    }
}
