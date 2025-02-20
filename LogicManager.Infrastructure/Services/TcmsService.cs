using LogicManager.Domain.Entities;
using LogicManager.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace LogicManager.Infrastructure.Services
{
    public class TcmsService : ITcmsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TcmsService> _logger;
        private readonly string _tcmsApiUrl;
        private bool _isConnected;
        private Timer? _pollingTimer;

        public event EventHandler<TcmsData>? OnTcmsDataReceived;

        public TcmsService(ILogger<TcmsService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _tcmsApiUrl = configuration["TcmsSettings:ApiUrl"] ?? "http://localhost:5000/api/tcms";
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_tcmsApiUrl)
            };

            // TCMS verilerini periyodik olarak al
            _pollingTimer = new Timer(PollTcmsData, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        }

        public async Task<TcmsData> GetTcmsDataAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<TcmsData>("/status");
                if (response != null)
                {
                    _isConnected = true;
                    OnTcmsDataReceived?.Invoke(this, response);
                    return response;
                }

                throw new Exception("TCMS data is null");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger.LogError(ex, "TCMS veri alma hatası");
                return new TcmsData
                {
                    ZeroSpeed = 0,
                    DoorLeftReleased = false,
                    DoorRightReleased = false,
                    EmergencyBrakeActive = false,
                    ServiceBrakeActive = false,
                    BatteryVoltage = 0,
                    Timestamp = DateTime.Now
                };
            }
        }

        public Task<bool> IsConnectedAsync()
        {
            return Task.FromResult(_isConnected);
        }

        private async void PollTcmsData(object? state)
        {
            await GetTcmsDataAsync();
        }

        public void Dispose()
        {
            _pollingTimer?.Dispose();
            _httpClient.Dispose();
        }

        public Task<bool> CheckIfLeaderAsync()
        {
            throw new NotImplementedException();
        }
    }

}
