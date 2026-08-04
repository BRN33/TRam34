//using LogicManager.Shared.DTOs;
//using LogicManager.Shared.Helpers;
//using RabbitMQ.Client;
//using RabbitMQ.Client.Events;
//using RabbitMQ.Client.Exceptions;
//using System.Text;
//using System.Text.Json;

//namespace RabbitMQ.Shared
//{
//    public static class RabbitMQHelper
//    {
//        private static readonly object _lock = new();
//        private static IConnection _connection;
//        //private static IModel _channel;
//        private static bool _isConnected = false;
//        private static ConnectionFactory _factory;
//        private static HashSet<string> declaredConsume = new();
//        private static readonly LoggerHelper _logService;

//        private static void EnsureConnection(string host)
//        {
//            lock (_lock)
//            {
//                int retryCount = 0;
//                int maxRetry = 5; // Maksimum 5 deneme

//                while (retryCount < maxRetry)
//                {
//                    try
//                    {
//                        // Bağlantı açıksa çık
//                        if (_connection != null && _connection.IsOpen)
//                        {
//                            return;
//                        }

//                        // Önceki bağlantı varsa event'ten temizle
//                        if (_connection != null)
//                        {
//                            _connection.ConnectionShutdown -= OnConnectionShutdown;
//                        }

//                        _factory = new ConnectionFactory()
//                        {
//                            HostName = host,
//                            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
//                        };

//                        _connection = _factory.CreateConnection();
//                        _connection.ConnectionShutdown += OnConnectionShutdown;

//                        // Kanal mevcutsa dispose et
//                        if (_channel != null)
//                        {
//                            _channel.Dispose();
//                        }

//                        _channel = _connection.CreateModel();
//                        _isConnected = true;

//                        Console.WriteLine("RabbitMQ bağlantısı kuruldu.");
//                        return;
//                    }
//                    catch (Exception ex)
//                    {
//                        _isConnected = false;
//                        retryCount++;
//                        Console.ForegroundColor = ConsoleColor.Red;
//                        Console.WriteLine($"RabbitMQ Bağlantı hatası: {ex.Message} - {retryCount}. deneme. 3 saniye sonra tekrar denenecek...");
//                        Console.ResetColor();
//                        var currentTime = DateTime.Now;
//                        _logService?.ErrorSendLogAsync(new ErrorLogDto
//                        {
//                            MessageSource = "LogicManager",
//                            MessageContent = "RabbitMQ Bağlantı hatası: 3 saniye sonra tekrar denenecek...",
//                            MessageType = LogType.Error.ToString(),
//                            DateTime = currentTime,
//                            ErrorType = LogType.Error.ToString(),
//                            HardwareIP = "100.10.107.20"
//                        });
//                        Thread.Sleep(3000);
//                    }
//                }

//                throw new Exception("RabbitMQ'ya bağlanılamadı. Lütfen bağlantıyı kontrol edin.");
//            }
//        }

//        private static void OnConnectionShutdown(object sender, ShutdownEventArgs e)
//        {
//            Console.WriteLine("RabbitMQ bağlantısı kapandı! Yeniden bağlanıyor...");
//            _isConnected = false;
//            EnsureConnection(_factory.HostName);
//        }

//        public static void PublishMessage(string host, string exchangeName, string exchangeType, string routingKey, object obj)
//        {
//            Task.Run(() =>
//            {
//                EnsureConnection(host);
//                int retryCount = 0;
//                int maxRetry = 3; // Maksimum 3 deneme

//                while (retryCount < maxRetry)
//                {
//                    try
//                    {
//                        if (_channel == null || !_channel.IsOpen)
//                        {
//                            _channel = _connection.CreateModel();
//                            _channel.ExchangeDeclare(exchange: exchangeName, type: exchangeType, durable: true, autoDelete: false);
//                            Console.WriteLine("Yeni kanal oluşturuldu.");
//                        }

//                        var message = JsonSerializer.Serialize(obj);
//                        var body = Encoding.UTF8.GetBytes(message);
//                        var properties = _channel.CreateBasicProperties();
//                        properties.Persistent = true;

//                        _channel.BasicPublish(exchange: exchangeName, routingKey: routingKey, basicProperties: properties, body: body);
//                        return; // Başarıyla gönderildiğinde döngüden çık
//                    }
//                    catch (OperationInterruptedException ex)
//                    {
//                        retryCount++;
//                        Console.WriteLine($"RabbitMQ bağlantısı kesildi. Tekrar bağlanılıyor... Hata: {ex.Message}");
//                        EnsureConnection(host);
//                        Thread.Sleep(2000);
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"Hata: {ex.Message}");
//                        break;
//                    }
//                }
//            });
//        }

//        public static void ConsumeMessage<T>(string host, string exchangeName, string exchangeType, string queueName, string routingKey, Func<T, Task> act)
//        {
//            Task.Run(() =>
//            {
//                while (true)
//                {
//                    EnsureConnection(host);
//                    try
//                    {
//                        if (_channel == null || !_channel.IsOpen)
//                        {
//                            _channel = _connection.CreateModel();
//                            Console.WriteLine("Yeni kanal oluşturuldu.");
//                        }

//                        // Kuyruğa özel argümanlar
//                        var args = new Dictionary<string, object>
//                        {
//                            { "x-max-length", 1 },
//                            { "x-overflow", "drop-head" }
//                        };

//                        _channel.ExchangeDeclare(exchange: exchangeName, type: exchangeType, durable: true, autoDelete: false);
//                        _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
//                        _channel.QueueBind(queueName, exchangeName, routingKey);

//                        var consumer = new EventingBasicConsumer(_channel);
//                        consumer.Received += async (model, ea) =>
//                        {
//                            try
//                            {
//                                var body = ea.Body.ToArray();
//                                var message = Encoding.UTF8.GetString(body);
//                                var data = JsonSerializer.Deserialize<T>(message);

//                                await act(data);
//                                Console.WriteLine($"DeliveryTag: {ea.DeliveryTag}");
//                                _channel.BasicAck(ea.DeliveryTag, false);
//                            }
//                            catch (Exception ex)
//                            {
//                                Console.WriteLine($"İşleme hatası: {ex.Message}");
//                            }
//                        };

//                        if (!declaredConsume.Contains(queueName))
//                        {
//                            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
//                            Console.WriteLine($"{queueName} kuyruğu dinleniyor...");
//                            declaredConsume.Add(queueName);
//                        }
//                        return;
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"Consumer başlatılamadı: {ex.Message} - 3 saniye sonra tekrar denenecek...");
//                        Thread.Sleep(3000);
//                    }
//                }
//            });
//        }
//    }
//}
