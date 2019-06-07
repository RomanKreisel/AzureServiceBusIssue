using System;
using System.Threading;
using System.Threading.Tasks;
using MessageReceiver.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessageReceiver.Services
{
    public static class MessageReceiverServiceCollectionExtension
    {
        public static void AddMessageReceiver(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ServiceBusOptions>(configuration.GetSection("ServiceBus"));
            services.Configure<ReceiverStatusOptions>(configuration.GetSection("ReceiverStatus"));
            services.AddSingleton<MessageReceiverService>();
            services.AddSingleton<ReceiverStatusService>();
        }
    }

    public static class MessageReceiverApplicationBuilderExtension
    {
        public static void UseMessageReceiver(this IApplicationBuilder app)
        {
            var receiver = app.ApplicationServices.GetRequiredService<MessageReceiverService>();
            receiver.RegisterForApplication(app.ApplicationServices.GetRequiredService<IApplicationLifetime>());
        }
    }

    public class MessageReceiverService
    {
        private readonly ILogger _logger;
        private readonly IOptions<ServiceBusOptions> _serviceBusOptions;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private QueueClient _client;
        private readonly ReceiverStatusService _receiverStatusService;

        public MessageReceiverService(ILoggerFactory loggerFactory, IOptions<ServiceBusOptions> serviceBusOptions, ReceiverStatusService receiverStatusService)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _serviceBusOptions = serviceBusOptions;
            _receiverStatusService = receiverStatusService;
        }

        private long _messagesReceived = 0;
        private DateTimeOffset _startTime  = DateTimeOffset.Now;
        private DateTimeOffset _lastMessageReceived = DateTimeOffset.Now;


        public void RegisterForApplication(IApplicationLifetime app)
        {
            app.ApplicationStopping.Register(Stop);
            Start();
        }

        private void Stop()
        {
            _logger.LogInformation("Receiving messages stopping");
            cancellationTokenSource.Cancel();
            Task.WaitAll(_client.CloseAsync());
        }

        private void Start()
        {
            _logger.LogInformation("Receiving messages started");
            var thread = new Thread(Run);
            thread.Start();

            var monitorThread = new Thread(RunPeriodicalHealthCheck);
            monitorThread.Start();
        }

        private void RunPeriodicalHealthCheck()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (DateTimeOffset.Now - this._lastMessageReceived > TimeSpan.FromMinutes(5))
                    _logger.LogError(
                        $"No messages were received for {DateTimeOffset.Now - this._lastMessageReceived}");
                this._receiverStatusService.UpdateStatus(this._lastMessageReceived, this._messagesReceived);
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        private void SimulateCpuIntenseProcessing(TimeSpan duration)
        {
            var startTime = DateTimeOffset.Now;
            while (true)
                if (DateTimeOffset.Now - startTime > duration)
                    break;
        }

        private void Run()
        {
            //warmup
            Thread.Sleep(10000);

            _client = new QueueClient(_serviceBusOptions.Value.ConnectionString,
                _serviceBusOptions.Value.QueueName, ReceiveMode.PeekLock, RetryPolicy.Default)
            {
                PrefetchCount = _serviceBusOptions.Value.PrefetchCount
            };

            this._startTime = DateTimeOffset.Now;
            _client.RegisterMessageHandler((message, token) =>
            {
                try
                {
                    var length = message.Body.Length;
                    SimulateCpuIntenseProcessing(
                        TimeSpan.FromMilliseconds(_serviceBusOptions.Value.SimulateProcessingMilliseconds));
                    if (!_client.IsClosedOrClosing)
                    {
                        _logger.LogTrace($"Message with {length} bytes received");
                        _messagesReceived++;
                        this._lastMessageReceived = DateTimeOffset.Now;
                        _client.CompleteAsync(message.SystemProperties.LockToken);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Exception occurred while processing message");
                }

                return Task.CompletedTask;
            }, new MessageHandlerOptions(args =>
            {
                _logger.LogError(args.Exception, "Error processing message");
                return Task.CompletedTask;
            })
            {
                AutoComplete = false,
                MaxAutoRenewDuration = TimeSpan.FromSeconds(_serviceBusOptions.Value.MaxAutoRenewSeconds),
                MaxConcurrentCalls = _serviceBusOptions.Value.Workers
            });
        }
    }
}