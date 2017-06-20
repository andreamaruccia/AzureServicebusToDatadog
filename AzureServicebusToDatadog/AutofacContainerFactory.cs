using Autofac;

namespace AzureservicebusToDatadog
{
    public static class AutofacContainerFactory
    {
        public static IContainer Create()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<MetricsReporter>();
            return builder.Build();
        }
    }
}