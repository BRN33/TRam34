using TRAM34_DDU.Core.Application.RabbitMQService;


namespace TRAM34_DDU.Core.Application.Interfaces.Services
{
    public interface IRabbitService
    {
        Task<bool> PublishMessage(string host, string exchangeName, string exchangeType, string routingKey, object obj, ManagementEnum management, CancellationToken cancellationToken = default);
        Task ConsumerAsync<T>(string host, string exchangeName, string exchangeType, string queueName, string routingKey, ManagementEnum management, Func<T, Task> act);
    }
}
