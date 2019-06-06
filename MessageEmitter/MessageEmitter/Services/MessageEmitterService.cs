using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MessageEmitter.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessageEmitter.Services
{
    public static class MessageEmitterServiceCollectionExtension
    {
        public static void AddMessageEmitter(this IServiceCollection services)
        {
            services.AddSingleton<MessageEmitterService>();
        }
    }

    public static class MessageEmitterApplicationBuilderExtension
    {
        public static void UseMessageEmitter(this IApplicationBuilder app)
        {
            var emitter = app.ApplicationServices.GetRequiredService<MessageEmitterService>();
            emitter.RegisterForApplication(app.ApplicationServices.GetRequiredService<IApplicationLifetime>());
        }
    }

    public class MessageEmitterService
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ILogger _logger;
        private readonly Random _randomGenerator = new Random();
        private readonly IOptions<ServiceBusOptions> _serviceBusOptions;


        public MessageEmitterService(ILoggerFactory loggerFactory, IOptions<ServiceBusOptions> serviceBusOptions)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _serviceBusOptions = serviceBusOptions;
        }

        public DateTimeOffset StartTime { get; private set; } = DateTimeOffset.Now;
        public long MessageCount { get; private set; }

        internal void RegisterForApplication(IApplicationLifetime applicationLifetime)
        {
            applicationLifetime.ApplicationStopping.Register(Stop);
            Start();
        }


        private void Start()
        {
            _logger.LogInformation("Emitting messages started");
            var thread = new Thread(Run);
            thread.Start();
        }

        private void Stop()
        {
            _logger.LogInformation("Emitting messages stopping");
            _cancellationTokenSource.Cancel();
        }


        private void Run()
        {
            //warmup:
            Thread.Sleep(5000);

            var sender = new MessageSender(_serviceBusOptions.Value.ConnectionString,
                _serviceBusOptions.Value.QueueName);
            StartTime = DateTimeOffset.Now;
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var messages = new List<Message>(_serviceBusOptions.Value.BatchSize);
                for (var i = 0; i < _serviceBusOptions.Value.BatchSize; ++i)
                {
                    var body = new byte[_serviceBusOptions.Value.MessageSize];
                    _randomGenerator.NextBytes(body);
                    messages.Add(new Message
                    {
                        ContentType = "application/octet-stream",
                        Body = body,
                        Label = "MyLabel"
                    });
                }

                Task.WaitAll(sender.SendAsync(messages));
                MessageCount += messages.Count;
            }
        }
    }
}