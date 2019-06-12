using MessageReceiver.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceStack.Redis;
using System;
using System.Collections.Generic;

namespace MessageReceiver.Services
{
    public class ReceiverStatus
    {
        public DateTimeOffset LastMessageReceived { get; set; }
        public DateTimeOffset LastStatusUpdate { get; set; }
        public long NumberOfMessagesReceived { get; set; }

        public List<ReceiverDowntimes> ReceiverDowntimes { get; } = new List<ReceiverDowntimes>();

        public Boolean Critical { get; set; } = false;
    }

    public class ReceiverDowntimes
    {
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }

        public TimeSpan Duration => this.End - this.Start;
    }

    public class ReceiverStatusService
    {
        private readonly IOptions<ReceiverStatusOptions> _receiverStatusOptions;
        private readonly ILogger _logger;
        private DateTimeOffset _downSince = DateTimeOffset.MinValue;
        private readonly ReceiverStatus _receiverStatus = new ReceiverStatus();

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
                this._receiverStatus.LastMessageReceived = lastMessageReceived;
                this._receiverStatus.NumberOfMessagesReceived = numberOfMessagesReceived;
                this._receiverStatus.LastStatusUpdate = DateTimeOffset.Now;
                this._receiverStatus.Critical = _receiverStatus.LastStatusUpdate - _receiverStatus.LastMessageReceived.DateTime>
                               TimeSpan.FromMinutes(5);
                if (_receiverStatus.Critical && this._downSince == DateTimeOffset.MinValue)
                {
                    this._downSince = lastMessageReceived;
                }
                else if (!_receiverStatus.Critical && this._downSince != DateTimeOffset.MinValue)
                {
                    this._receiverStatus.ReceiverDowntimes.Add(new ReceiverDowntimes { Start = this._downSince, End = lastMessageReceived });
                }
                redisClient.Set($"{Environment.MachineName}", _receiverStatus);
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
