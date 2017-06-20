using System;
using System.Configuration;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Serilog;
using System.Linq;

namespace AzureservicebusToDatadog
{
    internal class MetricsReporter
    {
        private bool _continue = true;
        private Thread _thread;
        private static string _url = "https://app.datadoghq.com/api/v1/series?api_key=****apikey******";

        public void Start()
        {
            Log.Information("Application has been started!");

            _thread = new Thread(t => DoWork())
            {
                IsBackground = true,
                Name = "AzureservicebusToDatadog"
            };

            _thread.Start();
        }

        private async void DoWork()
        {
            var environments = new[] { "dev", "qa", "stg", "prd" };

            var namespaceManagers = (from environment in environments
                                     select new
                                     {
                                         Environment = environment,
                                         NamespaceManager = NamespaceManager.CreateFromConnectionString(ConfigurationManager.AppSettings["ServicebusConnectionString." + environment])
                                     }).ToArray();

            while (_continue)
            {
                try
                {
                    foreach (var namespaceManager in namespaceManagers)
                    {
                        foreach (var queue in await namespaceManager.NamespaceManager.GetQueuesAsync())
                        {
                            await LogMetricsAsync(queue, namespaceManager.Environment);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not send metrics", ex);
                }
                finally
                {
                    Thread.Sleep(TimeSpan.FromSeconds(20));
                }
            }

            Log.Information("Thread has finished it's work");
        }

        private static async Task LogMetricsAsync(QueueDescription queue, string env)
        {
            var payload = new
            {
                series = new[]
                {
                    CreateMetric(queue.Path, "active_message_count", queue.MessageCountDetails.ActiveMessageCount, env),
                    CreateMetric(queue.Path, "dead_letter_message_count", queue.MessageCountDetails.DeadLetterMessageCount, env),
                    CreateMetric(queue.Path, "queue_size_percentage", GetPercentage(queue.SizeInBytes,queue.MaxSizeInMegabytes), env)
                 }
            };

            Log.Debug($"Posting: {queue.Path} {queue.MessageCountDetails.ActiveMessageCount} {queue.MessageCountDetails.DeadLetterMessageCount}");
            var result = await new HttpClient().PostAsJsonAsync(_url, payload);
            Log.Debug(result.StatusCode.ToString());
        }

        private static object CreateMetric(string entityName, string metricName, long messageCount, string env) => new
        {
            metric = $"servicebus.{metricName}",
            points = new[]
            {
                new [] { DateTimeOffset.UtcNow.ToUnixTimeSeconds(), messageCount }
            },
            type = "gauge",
            tags = new[] { "env:" + env, "entity:" + entityName }
        };

        private static long GetPercentage(long currentQueueSizeInBytes, long maxQueueSizeinMegabytes)
        {
            var percentage = currentQueueSizeInBytes / 1024M / 1024M / maxQueueSizeinMegabytes * 100M;

            return (long)Math.Round(percentage, 0, MidpointRounding.AwayFromZero);
        }

        public void Stop()
        {
            _continue = false;

            if (!_thread.Join(TimeSpan.FromSeconds(3)))
            {
                Log.Warning("Thread has been aborted");
                _thread.Abort();
            }

            Log.Information("Application has been stopped!");
        }
    }
}