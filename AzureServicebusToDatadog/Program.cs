using Serilog;
using Serilog.Events;
using Topshelf;
using Topshelf.Autofac;

namespace AzureservicebusToDatadog
{
    public static class Program
    {
        public static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel
                    .Debug()
                .WriteTo
                    .RollingFile("log-{Date}.txt", LogEventLevel.Information)
                .WriteTo
                    .Console()
                .CreateLogger();

            HostFactory.Run(x =>
            {
                x.UseAutofacContainer(AutofacContainerFactory.Create());
                x.Service<MetricsReporter>(s =>
                {
                    s.ConstructUsingAutofacContainer();
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("AzureservicebusToDatadog. Send metrics of asb to datadog.");
                x.SetDisplayName("AzureservicebusToDatadog");
                x.SetServiceName("AzureservicebusToDatadog");
                x.UseSerilog();
            });
        }
    }
}