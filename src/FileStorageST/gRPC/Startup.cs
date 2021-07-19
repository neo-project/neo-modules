using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Neo.FileStorage.Storage.Services.Accounting;
using Neo.FileStorage.Storage.Services.Container;
using Neo.FileStorage.Storage.Services.Control;
using Neo.FileStorage.Storage.Services.Control.Service;
using Neo.FileStorage.Storage.Services.Netmap;
using Neo.FileStorage.Storage.Services.Object.Acl;
using Neo.FileStorage.Storage.Services.Reputaion.Service;
using Neo.FileStorage.Storage.Services.Session;
using APIAccountingService = Neo.FileStorage.API.Accounting.AccountingService;
using APIContainerService = Neo.FileStorage.API.Container.ContainerService;
using APINetmapService = Neo.FileStorage.API.Netmap.NetmapService;
using APIObjectService = Neo.FileStorage.API.Object.ObjectService;
using APIReputationService = Neo.FileStorage.API.Reputation.ReputationService;
using APISessionService = Neo.FileStorage.API.Session.SessionService;

namespace Neo.FileStorage.Storage
{
    public class Startup
    {
        private readonly Dictionary<Type, object> registedServices = new();

        public void InjectSingletonService(Type t, object impl)
        {
            registedServices.Add(t, impl);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();
            foreach (var (t, s) in registedServices)
                services.AddSingleton(t, s);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<APIAccountingService.AccountingServiceBase>();
                endpoints.MapGrpcService<APIContainerService.ContainerServiceBase>();
                endpoints.MapGrpcService<ControlService.ControlServiceBase>();
                endpoints.MapGrpcService<APINetmapService.NetmapServiceBase>();
                endpoints.MapGrpcService<APIObjectService.ObjectServiceBase>();
                endpoints.MapGrpcService<APIReputationService.ReputationServiceBase>();
                endpoints.MapGrpcService<APISessionService.SessionServiceBase>();
            });
        }
    }
}
