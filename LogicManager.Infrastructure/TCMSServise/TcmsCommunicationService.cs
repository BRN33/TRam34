using System.Text;
using System.Net.Sockets;

namespace LogicManager.Infrastructure.TCMSServise
{


    public class TcmsCommunicationService
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private readonly string _tcmsIp = "192.168.1.100"; // TCMS IP adresi
        private readonly int _tcmsPort = 5000; // TCMS Port numarası
        private int _counter = 0;
        private bool _isConnected = false;

        public async Task StartCommunicationAsync(CancellationToken stoppingToken)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_tcmsIp, _tcmsPort);
                _networkStream = _tcpClient.GetStream();
                _isConnected = true;
                Console.WriteLine("TCMS bağlantısı başarılı!");

                // Veri alımı ve counter gönderimini başlat
                _ = Task.Run(() => ReceiveDataAsync(stoppingToken), stoppingToken);
                _ = Task.Run(() => SendCounterAsync(stoppingToken), stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TCMS bağlantı hatası: {ex.Message}");
            }
        }

        private async Task ReceiveDataAsync(CancellationToken stoppingToken)
        {
            byte[] buffer = new byte[1024];

            while (!stoppingToken.IsCancellationRequested && _isConnected)
            {
                try
                {
                    int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, stoppingToken);
                    if (bytesRead > 0)
                    {
                        string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"TCMS'den Gelen Veri: {receivedData}");

                        // TCMS verilerini işleme
                        ProcessTcmsData(receivedData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Veri okuma hatası: {ex.Message}");
                    _isConnected = false;
                    break;
                }
            }
        }

        private void ProcessTcmsData(string data)
        {
            // Gelen veriyi parse et ve işle
            // Örneğin: ZeroSpeed ve AllDoorsReleased bilgisini al
            double zeroSpeed = ExtractZeroSpeed(data);
            bool allDoorsReleased = ExtractAllDoorsReleased(data);

            // TCMS verilerini güncelle
            UpdateTcmsData(zeroSpeed, allDoorsReleased);
        }

        private async Task SendCounterAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested && _isConnected)
            {
                try
                {
                    _counter++; // Counter değerini artır
                    string message = $"COUNTER={_counter}";
                    byte[] data = Encoding.ASCII.GetBytes(message);
                    await _networkStream.WriteAsync(data, 0, data.Length, stoppingToken);
                    Console.WriteLine($"TCMS'ye Counter Gönderildi: {_counter}");

                    await Task.Delay(5000, stoppingToken); // 5 saniyede bir counter gönder
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Counter gönderme hatası: {ex.Message}");
                    _isConnected = false;
                    break;
                }
            }
        }

        private double ExtractZeroSpeed(string data)
        {
            // Gelen string içinde ZeroSpeed değerini bul ve çevir
            // Örnek olarak sabit bir değer döndürüyoruz
            return 0.0;
        }

        private bool ExtractAllDoorsReleased(string data)
        {
            // Gelen string içinde AllDoorsReleased değerini bul ve çevir
            // Örnek olarak sabit bir değer döndürüyoruz
            return true;
        }

        private void UpdateTcmsData(double zeroSpeed, bool allDoorsReleased)
        {
            // Güncellenen TCMS verilerini ilgili servislere aktar
            Console.WriteLine($"ZeroSpeed: {zeroSpeed}, AllDoorsReleased: {allDoorsReleased}");
        }
    }

}
