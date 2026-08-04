using LogicManager.Persistence.Interfaces;
using LogicManager.Shared.DTOs;
using LogicManager.Shared.Helpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Shared;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using TRAM34_DDU.Core.Application.Interfaces.Services;
using TRAM34_DDU.Core.Application.RabbitMQService;


namespace LogicManager.Infrastructure.Services;

public class RabbitService : IRabbitService
{


    private readonly LoggerHelper _logService;
    private readonly IMongoDbService _mongoDbService;

    public DateTime currentTime => DateTime.Now; // Güncel zamanı döndüren özellik

    private string? _cachedYbsPcIp = null;// YbsPcIp'yi hafızada tutmak için değişken

    public RabbitService(LoggerHelper logService, IMongoDbService mongoDbService)
    {


        _logService = logService;
        _mongoDbService = mongoDbService;

        _ = GetIpWithCacheAsync(); // Başlangıçta IP'yi çek ve hafızaya at

    }

    private readonly ConcurrentDictionary<string, IConnection> _connections = new();
    private readonly ConcurrentDictionary<string, (List<IChannelConfiguration> Active, List<IChannelConfiguration> Lost)> _channelConfigurations = new();

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostSemaphores = new();
    private readonly List<UnsetMessages> unsetMessages = new();

    private async Task<IConnection> CreateConnectionAsync(string host, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(host, out IConnection existingConnection) && existingConnection.IsOpen)
            return existingConnection;

        Console.WriteLine($"{host} bağlantısı kapalı! Yeni bağlantı oluşturuluyor...");
        ConnectionFactory _factory = new()
        {
            HostName = host,
            AutomaticRecoveryEnabled = false,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(1),
            RequestedHeartbeat = TimeSpan.FromMinutes(1)

        };

        using (var newConnection = await _factory.CreateConnectionAsync(cancellationToken))
        {
            _factory.AutomaticRecoveryEnabled = true;
            _connections[host] = await _factory.CreateConnectionAsync(cancellationToken);
        }
        ;

        _connections[host].ConnectionShutdownAsync += async (sender, @event) => await ConnectionShutdownAsync(sender, @event, host);
        _connections[host].ConnectionRecoveryErrorAsync += async (sender, args) => await ConnectionRecoveryErrorAsync(sender, args, host);
        _connections[host].RecoverySucceededAsync += async (sender, args) => await RecoverySucceededAsync(sender, args, host);
        return _connections[host];
    }

    private async Task RecoverySucceededAsync(object sender, AsyncEventArgs @event, string host)
    {
        Console.WriteLine($"[{host}] için RabbitMQ bağlantısı yeniden oluşturuldu...");

        if (_channelConfigurations.TryGetValue(host, out var configurations))
        {
            foreach (var config in configurations.Lost)
            {
                IChannel newChannel = config.Channel ?? await _connections[host].CreateChannelAsync();
                await BindDeclareAsync(newChannel, config.ExchangeName, config.ExchangeType, config.QueueName, config.RoutingKey, config.Management);
                await config.ConsumeAsync(newChannel);
                configurations.Active.Add(config);
                Console.WriteLine("Kayıp channel oluşturuldu.");
            }
            configurations.Lost.Clear();
        }

        foreach (var unsetMessage in unsetMessages.ToList())
        {
            if (unsetMessage.Host == host)
            {
                await PublishMessage(unsetMessage.Host, unsetMessage.ExchangeName, unsetMessage.ExchangeType, unsetMessage.RoutingKey, unsetMessage.Obj, unsetMessage.Management);
                unsetMessages.Remove(unsetMessage);
            }
        }
        /*_logService.SendLogAsync<InformationLog>(
            _logFactory.CreateInformationLog($"[RabbitMQ][{host}] RabbitMQ {host} bağlantısı yeniden oluşturuldu.", "HMIController"));*/
        //string? _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();

        _logService?.EventSendLogAsync(new EventLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"1001 - RabbitMQ {host} bağlantısı yeniden oluşturuldu.", // Dinamik içerik buraya gelecek
            MessageType = LogType.Event.ToString(),
            DateTime = currentTime,
            SourceIP = _cachedYbsPcIp, // Bu IP'ler sabitse, burada kalabilir.
            DestinationIP = _cachedYbsPcIp,
            DestinationName = "StretchController"
        });

        await Task.CompletedTask;
    }


    private async Task ConnectionRecoveryErrorAsync(object sender, ConnectionRecoveryErrorEventArgs @event, string host)
    {
        Console.WriteLine($"{host} için RabbitMQ bağlantısı yeniden bağlanıyor... {@event.Exception.Message}");

        //string? _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();// bulunduğu trenin ybs pc ipsi

        _logService?.ErrorSendLogAsync(new ErrorLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"1003 - RabbitMQ'da {host} için yeniden bağlanmayı deniyor.", // Dinamik içerik buraya gelecek
            MessageType = LogType.Error.ToString(),
            DateTime = currentTime,
            MessageSourceType = "Software",
            HardwareIP = _cachedYbsPcIp
        });

        await Task.CompletedTask;
    }

    private async Task ConnectionShutdownAsync(object sender, ShutdownEventArgs @event, string host)
    {
        Console.WriteLine($"{host} için RabbitMQ bağlantısı kesildi... {@event.ReplyText}");

        //string? _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();// bulunduğu trenin ybs pc ipsi

        _logService?.ErrorSendLogAsync(new ErrorLogDto
        {
            MessageSource = "LogicManager",
            MessageContent = $"1004 - RabbitMQ'da {host} için bağlantı kesildi.", // Dinamik içerik buraya gelecek
            MessageType = LogType.Error.ToString(),
            DateTime = currentTime,
            MessageSourceType = "Software",
            HardwareIP = _cachedYbsPcIp
        });

        await Task.CompletedTask;
    }

    private async Task<IConnection> GetConnectionAsync(string host, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(host, out IConnection existingConnection) && existingConnection.IsOpen)
            return existingConnection;

        if (existingConnection?.CloseReason?.ReplyCode is not null)
            throw new Exception("Yeniden bağlantı bekleniyor.");

        var semaphore = _hostSemaphores.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));

        var connectionStatus = _connections.TryGetValue(host, out IConnection hostConnection);
        int attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            attempt++;
            await semaphore.WaitAsync();
            try
            {
                if (host != RabbitMQConstants.RabbitMQHost)
                {
                    return null;
                }
                var connection = await CreateConnectionAsync(host, cancellationToken);
                if (connection != null && connection.IsOpen)
                    return connection;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{host} için ilk bağlantı hatası ({attempt}. deneme): {ex.Message}");
                //var _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();// bulunduğu trenin ybs pc ipsi

                _logService?.ErrorSendLogAsync(new ErrorLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = $"1005 - RabbitMQ {host} 'a bağlanamadı.: {ex.Message}", // Dinamik içerik buraya gelecek
                    MessageType = LogType.Error.ToString(),
                    DateTime = currentTime,
                    MessageSourceType = "Software",
                    HardwareIP = _cachedYbsPcIp
                });

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            finally
            {
                semaphore.Release();
            }
        }
        throw new OperationCanceledException("RabbitMQ bağlantısı sağlanamadı.");
    }

    //public async Task ConnectionClose(List<string> ip)
    //{
    //    var incomingIps = new HashSet<string>(ip.Select(t => t.Ip));

    //    foreach (var key in _connections.Keys)
    //    {
    //        if (!incomingIps.Contains(key) && key != RabbitMQConstants.RabbitMQHost)
    //        {
    //            if (_connections.TryRemove(key, out var connection))
    //            {
    //                await connection.CloseAsync();
    //                connection.Dispose();
    //                Console.WriteLine($"[{key}] için bağlantı kapatıldı.");
    //            }
    //        }
    //    }
    //}


    private void AddMessage(UnsetMessages newMessage)
    {
        // Aynı Host, ExchangeName, ExchangeType ve RoutingKey değerine sahip olanları sil
        unsetMessages.RemoveAll(m =>
            m.Host == newMessage.Host &&
            m.ExchangeName == newMessage.ExchangeName &&
            m.ExchangeType == newMessage.ExchangeType &&
            m.RoutingKey == newMessage.RoutingKey);
        // Yeni mesajı listeye ekle
        unsetMessages.Add(newMessage);
    }

    private async Task ConsumeAsync<T>(IChannel channel, string queueName, ManagementEnum management, Func<T, Task> act)
    {
        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                byte[] body = ea.Body.ToArray();
                string message = Encoding.UTF8.GetString(body);
                T? data = JsonSerializer.Deserialize<T>(message);

                if (data != null)
                    await act(data);

                if (management == ManagementEnum.LastMessage && ea.DeliveryTag > 1)
                    await channel.BasicAckAsync(ea.DeliveryTag - 1, true);

                if (management == ManagementEnum.Live || management == ManagementEnum.UnlostMessage)
                    await channel.BasicAckAsync(ea.DeliveryTag, false);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Consume error: " + ex.Message);
                string host = _channelConfigurations
                .Where(kvp => kvp.Value.Active.Any(c => c.Channel == channel) || kvp.Value.Lost.Any(c => c.Channel == channel))
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

                //var _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();// bulunduğu trenin ybs pc ipsi
                Console.WriteLine($"[JsonException][RabbitMQ][{host}] RabbitMQ {consumer.Channel.CurrentQueue} kuyruğuna yanlış türde veri geldi.");

                _logService?.ErrorSendLogAsync(new ErrorLogDto
                {
                    MessageSource = "LogicManager",
                    MessageContent = $"1006 - RabbitMQ {consumer.Channel.CurrentQueue} kuyruğuna yanlış türde veri geldi: {ex.Message}", // Dinamik içerik buraya gelecek
                    MessageType = LogType.Error.ToString(),
                    DateTime = currentTime,
                    MessageSourceType = "Software",
                    HardwareIP = _cachedYbsPcIp
                });

            }
        };

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer);

    }

    private async Task BindDeclareAsync(IChannel channel, string exchangeName, string exchangeType, string queueName, string routingKey, ManagementEnum management)
    {
        var args = new Dictionary<string, object>();
        if (management == ManagementEnum.Live)
        {
            await channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: true, arguments: args);
        }
        else if (management == ManagementEnum.LastMessage)
        {
            args["x-max-length"] = 1;
            args["x-overflow"] = "drop-head";
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
        }
        else//UNLOST
        {
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
        }
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: exchangeType, durable: true, autoDelete: false);
        await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);
    }

    public async Task ConsumerAsync<T>(string host, string exchangeName, string exchangeType, string queueName, string routingKey, ManagementEnum management, Func<T, Task> act)
    {
        IChannel channel = null;
        try
        {
            if (_channelConfigurations.TryGetValue(host, out var configurations) && (configurations.Active.Any(c => c.QueueName == queueName) || configurations.Lost.Any(c => c.QueueName == queueName)))
            {
                Console.WriteLine($"[{host}] -> [{queueName}] için zaten bir kanal tanımlı.");
                return;
            }
            if (!_channelConfigurations.ContainsKey(host))
            {
                _channelConfigurations[host] = (new List<IChannelConfiguration>(), new List<IChannelConfiguration>());
            }

            IConnection connection = await GetConnectionAsync(host);
            channel = await connection.CreateChannelAsync();

            //channel.ChannelShutdownAsync += async (sender, @event) => await ChannelShutdownAsync(sender, @event, host);
            await BindDeclareAsync(channel, exchangeName, exchangeType, queueName, routingKey, management);
            await ConsumeAsync<T>(channel, queueName, management, act);

            _channelConfigurations[host].Active.Add(new ChannelConfiguration<T>(host, exchangeName, exchangeType, queueName, routingKey, management, act, channel));


            Console.WriteLine($"[{host}] -> [{queueName}] Bağlandı");


            _logService?.InformationSendLogAsync(new InformationLogDto
            {

                MessageSource = "LogicManager",
                MessageContent = $"1000 - RabbitMQ {host} -> {queueName} kuyruğuna bağlandı.",
                MessageType = LogType.Information.ToString(),
                DateTime = currentTime,
            });


            /*var _cachedYbsPcIp = await _couplingService.CurrentTrainYBSPcIp();
            _logService.SendLogAsync<EventLog>(
                _logFactory.CreateEventLog($"[RabbitMQ][{host}]. RabbitMQ {host} -> {queueName} kuyruğuna bağlandı.", "HMIController", _cachedYbsPcIp.Ip, "RabbitMQ", host));
            */
        }
        catch (Exception ex)
        {
            //var _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();// bulunduğu trenin ybs pc ipsi

            _logService?.ErrorSendLogAsync(new ErrorLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"1007 - RabbitMQ {host} -> {queueName} kuyruğuna bağlanırken bir hata oluştu: {ex.Message}", // Dinamik içerik buraya gelecek
                MessageType = LogType.Error.ToString(),
                DateTime = currentTime,
                MessageSourceType = "Software",
                HardwareIP = _cachedYbsPcIp
            });

            _channelConfigurations[host].Lost.Add(new ChannelConfiguration<T>(host, exchangeName, exchangeType, queueName, routingKey, management, act, channel));
            Console.WriteLine("Hata oluştu :" + ex.Message);
        }
    }


    public async Task<bool> PublishMessage(string host, string exchangeName, string exchangeType, string routingKey, object obj, ManagementEnum management, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            IConnection connection = await GetConnectionAsync(host, linkedCts.Token);
            if (connection == null)
                return false;
            using IChannel channel = await connection.CreateChannelAsync();// her publish için yeni channel açmak daha sağlıklı olur, zaten RabbitMQ.Client kütüphanesi channel'ları hafif yapıyor, performans sorunu olmaz
            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: exchangeType, durable: true, autoDelete: false);
            var message = JsonSerializer.Serialize(obj);
            var body = Encoding.UTF8.GetBytes(message);
            var properties = new BasicProperties() { Persistent = true };
            await channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, mandatory: false, basicProperties: properties, body: body);
            Console.WriteLine($"[{host}] -> [{exchangeName}] mesaj gönderildi.");

            /*_logService.SendLogAsync<InformationLog>(_logFactory.CreateInformationLog($"[RabbitMQ][{host}]. RabbitMQ {host} -> {exchangeName} exchange'ine veri gönderildi.", "HMIController"));*/

            //var _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();

            _logService?.EventSendLogAsync(new EventLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"1002 - RabbitMQ {host} -> {exchangeName} exchange'ine veri gönderildi.", // Dinamik içerik buraya gelecek
                MessageType = LogType.Event.ToString(),
                DateTime = currentTime,
                SourceIP = _cachedYbsPcIp, // Bu IP'ler sabitse, burada kalabilir.
                DestinationIP = _cachedYbsPcIp,
                DestinationName = "RabbitMQ"
            });


            return true;
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine("Publish işlemi 1 saniyeyi geçti ve iptal edildi.");
            //var _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();// bulunduğu trenin ybs pc ipsi

            _logService?.ErrorSendLogAsync(new ErrorLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"1008 - RabbitMQ {host} -> {exchangeName} exchange'ine veri gönderilemedi.", // Dinamik içerik buraya gelecek
                MessageType = LogType.Error.ToString(),
                DateTime = currentTime,
                MessageSourceType = "Software",
                HardwareIP = _cachedYbsPcIp
            });

            return false;
        }
        catch (Exception ex)
        {

            Console.WriteLine($"[{host}] -> [{exchangeName}] Mesaj gönderilirken hata oluştu: " + ex.Message);
            //var _cachedYbsPcIp = await _mongoDbService.GetYbsPcIpAsync();// bulunduğu trenin ybs pc ipsi

            _logService?.ErrorSendLogAsync(new ErrorLogDto
            {
                MessageSource = "LogicManager",
                MessageContent = $"1008 - RabbitMQ {host} -> {exchangeName} exchange'ine veri gönderilemedi.", // Dinamik içerik buraya gelecek
                MessageType = LogType.Error.ToString(),
                DateTime = currentTime,
                MessageSourceType = "Software",
                HardwareIP = _cachedYbsPcIp
            });


            if (management == ManagementEnum.LastMessage)
            {
                AddMessage(new() { Host = host, ExchangeName = exchangeName, ExchangeType = exchangeType, RoutingKey = routingKey, Obj = obj, Management = management });
            }
            else if (management == ManagementEnum.UnlostMessage)
            {
                unsetMessages.Add(new() { Host = host, ExchangeName = exchangeName, ExchangeType = exchangeType, RoutingKey = routingKey, Obj = obj, Management = management });
            }
            return false;
        }
    }


    // Mongodan her seferinde veri çekmek yerine tek seferIP'yi çekip hafızaya atarak performansı artırmak için yardımcı metot
    public async Task<string?> GetIpWithCacheAsync()
    {
        // Eğer daha önce IP'yi başarıyla aldıysak, direkt hafızadan döndür (Mongo'ya gitme)
        if (!string.IsNullOrEmpty(_cachedYbsPcIp))
            return _cachedYbsPcIp;

        try
        {
            // IP henüz yoksa Mongo'dan çekmeyi dene
            var ip = await _mongoDbService.GetYbsPcIpAsync();
            if (!string.IsNullOrEmpty(ip))
            {
                _cachedYbsPcIp = ip; // Gelecek sefer için hafızaya yaz
            }
            return ip;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Mongo IP çekme hatası: " + ex.Message);
            return "Unknown-IP"; // Hata durumunda logların akması için fallback
        }
    }


}

