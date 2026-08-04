using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Persistence.Interfaces;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Shared;
using TRAM34_DDU.Core.Application.Interfaces.Services;
using TRAM34_DDU.Core.Application.RabbitMQService;

namespace LogicManager.Infrastructure.Services
{
    public class TrainCoupledService : ITrainCoupledService
    {
        private IConfiguration _configuration;
        private TrainCouplingData? trainCouplingData;
        private readonly IMongoDbService _mongoDbService;
        private readonly IRabbitService _rabbitService;
        private readonly LoggerHelper _logService;
        private readonly string? _ybsPcIp;//YBS PC IP adresi

        public DateTime currentTime => DateTime.Now; // Güncel zamanı döndüren özellik

        public event Action<TrainCouplingData> OnTrainDataUpdated;
        public TrainCoupledService(IServiceScopeFactory serviceScopeFactory, IConfiguration configuration,IRabbitService rabbitService)
        {
            _configuration = configuration;
            trainCouplingData = new TrainCouplingData();
            _mongoDbService = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IMongoDbService>();
            _logService = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<LoggerHelper>();
            _rabbitService = rabbitService;

            _ybsPcIp = _mongoDbService.GetYbsPcIpAsync().GetAwaiter().GetResult();
            InitializeStartConsumMongo(); // RabbitMQ'dan veri almak için başlat

        }
        private async void InitializeStartConsumMongo()
        {

            await _rabbitService.ConsumerAsync<string>(
            RabbitMQConstants.RabbitMQHost,
            RabbitMQConstants.CoupledTrainsExchangeName,
            ExchangeType.Fanout,
            RabbitMQConstants.CoupledTrainsQueueName,
            "",
            ManagementEnum.LastMessage,
            StartConsumMongo);


        }

        public async Task StartConsumMongo(string jsonMessage)
        {
            try
            {
                var trainData = JsonConvert.DeserializeObject<TrainCouplingData>(jsonMessage);
                var trainId = trainData?.CurrentTrain.ID;
                
                //var jsonData = JsonSerializer.Deserialize<TrainCouplingData>(message);

                if (trainCouplingData != trainData)
                {
                    trainCouplingData = trainData!;
                    //_cacheService.Set<TrainCouplingData>("TrainCouplingData", jsonData);
                    Console.WriteLine("TrainCouplingData Verisi Geldi." + trainData!.CurrentTrain.ID);
                    // Veriyi MongoDB'ye kaydet
                    await _mongoDbService.SetTrainId(trainData.CurrentTrain.ID!);
                    _logService.SetTrainId(trainData.CurrentTrain.ID!);
                    //await RabbitMQService.ConnectionClose(_trainCouplingService.TrainCouplingIps());
                    OnTrainDataUpdated?.Invoke(trainData);

                   

                    await _logService.EventSendLogAsync(new EventLogDto
                    {
                        MessageSource = "LogicManager",
                        MessageContent = "432 - TCMS den veriler alındı.İslenmeye baslıyor",
                        MessageType = LogType.Event.ToString(),
                        DateTime = currentTime,
                        SourceIP = _ybsPcIp,
                        DestinationIP = _ybsPcIp,
                        DestinationName = "TCMSController"
                    });

                }
                else
                {
                    var configTrainId = _configuration["MongoDb:TrainId"];
                    await _mongoDbService.SetTrainId(configTrainId!);
                    _logService.SetTrainId(configTrainId!);
                    
                    await _logService.EventSendLogAsync(new EventLogDto
                    {
                        MessageSource = "LogicManager",
                        MessageContent = "433 - TCMS den veriler gelmedi. Sadece apsettings ten TrainId alındı",
                        MessageType = LogType.Event.ToString(),
                        DateTime = currentTime,
                        SourceIP = _ybsPcIp,
                        DestinationIP = _ybsPcIp,
                        DestinationName = "TCMSController"
                    });

                }
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Tcms Kuplajdan veriler okunurken hata olustu: {ex.Message}");
            }


        }

        public Task<TrainCouplingData> GetLastTrainData()
        {
            return Task.FromResult(trainCouplingData!);
        }
    }

}
