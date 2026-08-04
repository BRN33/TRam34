namespace TRAM34_DDU.Core.Application.RabbitMQService
{
    public class UnsetMessages
    {
        public string Host { get; set; }
        public string ExchangeName { get; set; }
        public string ExchangeType { get; set; }
        public string RoutingKey { get; set; }
        public object Obj { get; set; }
        public ManagementEnum Management { get; set; }
    }
}
