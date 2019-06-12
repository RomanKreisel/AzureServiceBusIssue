namespace MessageReceiver.Options
{
    public class ReceiverStatusOptions
    {
        public string RedisDatabaseHostname { get; set; } = "localhost";
        public int RedisDatabasePort { get; set; } = 6379;
    }
}