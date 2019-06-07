namespace MessageReceiver.Options
{
    public class ServiceBusOptions
    {
        public int SimulateProcessingMilliseconds = 0;
        public string ConnectionString { get; set; }

        public string QueueName { get; set; }

        public int PrefetchCount { get; set; } = 100;

        public int Workers { get; set; } = 2;


        public int MaxAutoRenewSeconds { get; set; } = 60;
    }
}