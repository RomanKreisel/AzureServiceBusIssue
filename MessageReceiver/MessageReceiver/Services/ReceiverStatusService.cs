using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MessageReceiver.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceStack.Redis;

namespace MessageReceiver.Services
{
    public class ReceiverStatus
    {
        public DateTimeOffset LastMessageReceived { get; set; }
        public DateTimeOffset LastStatusUpdate { get; set; }
        public long NumberOfMessagesReceived { get; set; }
        public Boolean Critical { get; set; } = false;

    }

    public class ReceiverStatusService
    {
        private IOptions<ReceiverStatusOptions> _receiverStatusOptions;
        private ILogger _logger;

        public ReceiverStatusService(IOptions<ReceiverStatusOptions> receiverStatusOptions, ILoggerFactory loggerFactory)
        {
            this._receiverStatusOptions = receiverStatusOptions;
            this._logger = loggerFactory.CreateLogger(this.GetType());
        }

        public void UpdateStatus(DateTimeOffset lastMessageReceived, long numberOfMessagesReceived)
        {
            using (var redisClient = new RedisClient(this._receiverStatusOptions.Value.RedisDatabaseHostname,
                this._receiverStatusOptions.Value.RedisDatabasePort))
            {
                var receiverStatus = new ReceiverStatus
                {
                    LastMessageReceived = lastMessageReceived,
                    NumberOfMessagesReceived = numberOfMessagesReceived,
                    LastStatusUpdate = DateTimeOffset.Now
                };
                receiverStatus.Critical = receiverStatus.LastStatusUpdate - receiverStatus.LastMessageReceived.DateTime >
                               TimeSpan.FromMinutes(5);
                redisClient.Set($"{Environment.MachineName}", receiverStatus);
            }
        }

        public Dictionary<string, ReceiverStatus> GetStatus()
        {
            using (var redisClient = new RedisClient(this._receiverStatusOptions.Value.RedisDatabaseHostname,
                this._receiverStatusOptions.Value.RedisDatabasePort))
            {
                var result = new Dictionary<string, ReceiverStatus>();
                foreach (var key in redisClient.GetAllKeys())
                {
                    if (key != "counter")
                    {
                        try
                        {
                            var content = redisClient.Get<ReceiverStatus>(key);
                            result.Add(key, content);
                        }
                        catch (Exception e)
                        {
                            this._logger.LogWarning($"Error reading receiver status from key {key}");
                        }
                    }
                }

                return result;
            }
        }
    }
}
