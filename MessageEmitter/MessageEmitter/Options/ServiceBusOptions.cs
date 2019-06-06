namespace MessageEmitter.Options
{
    public class ServiceBusOptions
    {
        public string ConnectionString { get; set; }

        public string QueueName { get; set; }

        public int MessageSize { get; set; } = 1024;

        public int BatchSize { get; set; } = 10;
    }
}