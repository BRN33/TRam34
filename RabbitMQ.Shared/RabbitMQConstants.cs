namespace RabbitMQ.Shared
{
    public class RabbitMQConstants
    {
        public const string RabbitMQHost = "RabbitMQ"; // localhost idi fakat dockerda calısması icin rabbitmq yapıldı
        public const string ExchangeType = "fanout";

        public const string TakoReadExchangeName = "TakoRead";
        public const string AnnounceExchangeName = "AnnouncementAutoPush";
        public const string NextStationInfoExchangeName = "NextStationInfoPush";
        public const string DistanceInfoExchangeName = "DistanceInfoPush";
        public const string LedExchangeName = "LedPush";
        public const string LedQueName = "LedPushQue";
        public const string RotaExchangeName = "SavedRoute";
        public const string RotaQueueName = "LogicSavedRoute";

        public const string ContiniueSyncRotaExchangeName = "ContiniueRoute"; // ikisi de programın kapanıp acılması durumunda rotanın devam etmesini saglamak icin yazıldı
        public const string ContiniueSyncRotaQueueName = "LogicContiniueRoute";
        

        public const string TakoExchangeName = "TakoRead";
        public const string TakoQueueName = "TakoReadQueue";

        public const string CoupledTrainsExchangeName = "CoupledTrains";
        public const string CoupledTrainsQueueName = "CoupledTrainsQueue";

        public const string LeadExchangeName = "LeadRead";
        public const string RouteCompletedExchangeName = "RouteCompleted";
    }
}
