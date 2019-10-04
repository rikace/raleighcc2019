using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Akka.Actor;
using Akka.Routing;

using AkkaFractal.Core;
using AkkaFractal.Core.Akka;
using AkkaFractal.Web.Akka;
using static AkkaFractal.Core.ColorConsole;

using Lib.AspNetCore.ServerSentEvents;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AkkaFractal.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddServerSentEvents(options =>
            {
                options.KeepaliveMode = ServerSentEventsKeepaliveMode.Always;
                options.KeepaliveInterval = 15;
            });
            
            services.AddSingleton(_ => ActorSystem.Create("fractal", ConfigurationLoader.Load()));
      
            services.AddSingleton<SseTileActorProvider>(provider =>
            {
                var serverSentEventsService = provider.GetService<IServerSentEventsService>();

                var actorSystem = provider.GetService<ActorSystem>();


                var tileRenderActor = actorSystem.ActorOf(Props.Create<TileRenderActor>()
                    .WithRouter(FromConfig.Instance), "remoteactor");
                
                    
                var sseTileActor = actorSystem.ActorOf(Props.Create(() => new SseTileActor(serverSentEventsService, tileRenderActor)), "sse-tile");
                return () => sseTileActor;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            app.MapServerSentEvents("/fractal-tiles");
            
            lifetime.ApplicationStarted.Register(() =>
            {
                app.ApplicationServices.GetService<ActorSystem>(); // Start Akka.NET
            });
            lifetime.ApplicationStopping.Register(() =>
            {
                app.ApplicationServices.GetService<ActorSystem>().Terminate()
                    .GetAwaiter().GetResult();
            });
        }
    }
}
