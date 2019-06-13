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
        public DateTimeOffset NodeStartTime { get; set; }
        public DateTimeOffset LastMessageReceived { get; set; }
        public DateTimeOffset LastStatusUpdate { get; set; }
        public long NumberOfMessagesReceived { get; set; }

        public List<ReceiverDowntimes> ReceiverDowntimes { get; set; } = new List<ReceiverDowntimes>();

        public TimeSpan CumulatedDownTime { get; set; } = TimeSpan.Zero;

        public bool Critical { get; set; }
    }

    public class ReceiverDowntimes
    {
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }

        public TimeSpan Duration => End - Start;
    }

    public class ReceiverStatusService
    {
        private readonly ILogger _logger;
        private readonly ReceiverStatus _receiverStatus = new ReceiverStatus();
        private readonly IOptions<ReceiverStatusOptions> _receiverStatusOptions;
        private DateTimeOffset _downSince = DateTimeOffset.MinValue;

        public ReceiverStatusService(IOptions<ReceiverStatusOptions> receiverStatusOptions,
            ILoggerFactory loggerFactory)
        {
            _receiverStatusOptions = receiverStatusOptions;
            _logger = loggerFactory.CreateLogger(GetType());
            _receiverStatus.NodeStartTime = DateTimeOffset.Now;
        }

        public void UpdateStatus(DateTimeOffset lastMessageReceived, long numberOfMessagesReceived)
        {
            try
            {
                using (var redisClient = new RedisClient(_receiverStatusOptions.Value.RedisDatabaseHostname,
                    _receiverStatusOptions.Value.RedisDatabasePort))
                {
                    _receiverStatus.LastMessageReceived = lastMessageReceived;
                    _receiverStatus.NumberOfMessagesReceived = numberOfMessagesReceived;
                    _receiverStatus.LastStatusUpdate = DateTimeOffset.Now;
                    _receiverStatus.Critical =
                        _receiverStatus.LastStatusUpdate - _receiverStatus.LastMessageReceived.DateTime >
                        TimeSpan.FromMinutes(1);
                    if (_receiverStatus.Critical)
                    {
                        if (_downSince == DateTimeOffset.MinValue)
                        {
                            _downSince = lastMessageReceived;
                        }
                        this._logger.LogWarning($"Node {Environment.MachineName} didn't receive any new message for {DateTimeOffset.Now - lastMessageReceived}");
                    }
                    else if (!_receiverStatus.Critical && _downSince != DateTimeOffset.MinValue)
                    {
                        _receiverStatus.ReceiverDowntimes.Add(new ReceiverDowntimes
                        { Start = _downSince, End = lastMessageReceived });
                        this._logger.LogWarning($"Node {Environment.MachineName} recovered after {lastMessageReceived - _downSince}");
                        this._downSince = DateTimeOffset.MinValue;
                    }

                    var cumulatedDowntime = TimeSpan.Zero;
                    foreach (var downTime in this._receiverStatus.ReceiverDowntimes)
                    {
                        cumulatedDowntime += (downTime.End - downTime.Start);
                    }

                    if (_downSince != DateTimeOffset.MinValue)
                    {
                        cumulatedDowntime += lastMessageReceived - _downSince;
                    }

                    this._receiverStatus.CumulatedDownTime = cumulatedDowntime;

                    redisClient.Set($"{Environment.MachineName}", _receiverStatus);
                }
            }
            catch (RedisException e)
            {
                this._logger.LogWarning(e, "Error pushing status update to redis store");
            }
        }

        public Dictionary<string, ReceiverStatus> GetStatus()
        {
            using (var redisClient = new RedisClient(_receiverStatusOptions.Value.RedisDatabaseHostname,
                _receiverStatusOptions.Value.RedisDatabasePort))
            {
                var result = new Dictionary<string, ReceiverStatus>();
                foreach (var key in redisClient.GetAllKeys())
                    if (key != "counter")
                        try
                        {
                            var content = redisClient.Get<ReceiverStatus>(key);
                            result.Add(key, content);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning($"Error reading receiver status from key {key}");
                        }

                return result;
            }
        }
    }
}