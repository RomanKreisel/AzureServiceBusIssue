namespace MessageReceiver.Options
{
    public class ServiceBusOptions
    {
        public int SimulateProcessingMilliseconds = 300;
        public string ConnectionString { get; set; }

        public string QueueName { get; set; }

        public int PrefetchCount { get; set; } = 2;

        public int Workers { get; set; } = 10;


        public int MaxAutoRenewSeconds { get; set; } = 60;
    }
}