using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Shared;
using System.Net.Http.Json;
using TRAM34_DDU.Core.Application.Interfaces.Services;
using TRAM34_DDU.Core.Application.RabbitMQService;

namespace LogicManager.Infrastructure.Services
{
    public class TcmsService : ITcmsService
    {

        private readonly ILogger<TcmsService> _logger;
        private readonly object _lock = new object();
        private TcmsData _currentData = new TcmsData();
        public event Action<TcmsData>? OnTakoDataUpdated; // Güncellenen veriyi bildirmek için event
        private readonly IRabbitService RabbitMQService;

        public TcmsService(IRabbitService rabbitService)
        {
            RabbitMQService = rabbitService;
            InitializeRabbitMQConsumer();
        }

        private async void InitializeRabbitMQConsumer()
        {

            await RabbitMQService.ConsumerAsync<string>(
            RabbitMQConstants.RabbitMQHost,
            RabbitMQConstants.TakoReadExchangeName,
            ExchangeType.Fanout,
            RabbitMQConstants.TakoQueueName,
            "",
            ManagementEnum.Live,
            HandleNewTakoData);


        }

        private async Task HandleNewTakoData(string jsonMessage)
        {
            try
            {
                var newData = JsonConvert.DeserializeObject<TcmsData>(jsonMessage);
                if (newData != null)
                {
                    lock (_lock)
                    {
                        _currentData = newData;
                    }
                    OnTakoDataUpdated?.Invoke(newData);
                }
            }
            catch (Exception ex)
            {
               Console.WriteLine($"Deserialization edilirken hata olustu: {ex.Message}");
            }
        }


        //private async Task HandleNewTakoData(TcmsData newData)
        //{
        //    lock (_lock)
        //    {
        //        _currentData =  newData; // Güncel veriyi sakla

        //        //var obj = JsonConvert.DeserializeObject<TcmsData>(_currentData);

        //    }

        //    OnTakoDataUpdated?.Invoke(newData); // Event'i tetikle, dinleyen sınıflar güncellemeyi alsın
        //}
        //private async Task HandleNewTakoData(string jsonString)
        //{
        //    try
        //    {
        //        TcmsData newData = JsonConvert.DeserializeObject<TcmsData>(jsonString);

        //        if (newData != null)
        //        {
        //            lock (_lock)
        //            {
        //                _currentData = newData;
        //            }

        //            OnTakoDataUpdated?.Invoke(newData);
        //        }
        //        else
        //        {
        //            _logger.LogError("JSON deserialization failed.");
        //        }
        //    }
        //    catch (JsonException ex)
        //    {
        //        _logger.LogError($"JSON deserialization error: {ex.Message}");
        //    }
        //}

        public Task<TcmsData> GetLatestTakoDataAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_currentData); // Güncel veriyi döndür
            }
        }

        public Task<TcmsData> GetTcmsDataAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> CheckIfLeaderAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsConnectedAsync()
        {
            throw new NotImplementedException();
        }


    }

}
