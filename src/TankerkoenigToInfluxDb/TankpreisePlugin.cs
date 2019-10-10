using Framework.Abstraction.Plugins;
using ServiceHost.Contracts;
using System;
using Framework.Abstraction.Extension;
using Framework.Abstraction.IocContainer;
using Framework.Abstraction.Services.Scheduling;
using Framework.Core.Scheduling;
using Framework.Abstraction.Services;
using Framework.Abstraction.Services.DataAccess.InfluxDb;

namespace Tankpreise
{
    public class TankpreisePlugin : Framework.Abstraction.Plugins.Plugin, IServicePlugin
    {
        private readonly PluginDescription _description;

        public TankpreisePlugin(IDependencyResolver resolver,
                                IDependencyResolverConfigurator configurator,
                                IEventService eventService,
                                ILogger logger)
            : base(resolver, configurator, eventService, logger)
        {
            _description = new AutostartServicePluginDescription()
            {
                Description = "Plugin um regelmäßig Sprit Preise abzufragen und auf eine InfluxDB hochzuladen",
                Name = "Tankpreise",
                NeededServices = new[] { typeof(IConfiguration), typeof(ISchedulingService), typeof(IInfluxDbUpload) }
            };
        }

        public override PluginDescription Description => _description;

        protected override void ActivateInternal()
        {
            var schedulingService = Resolver.GetInstance<ISchedulingService>();
            var job = Resolver.CreateConcreteInstanceWithDependencies<TankpreiseJob>();

            schedulingService.AddJob(job, new PollingPlan(new TimeSpan(0, 6, 23)));
        }
    }
}
