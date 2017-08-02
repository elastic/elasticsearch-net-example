using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Nest;
using NuSearch.Domain;

namespace NuSearch.Web
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

		private IConfigurationRoot Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
            services.AddMvc();

			// register the client as a singleton
			services.Add(ServiceDescriptor.Singleton<IElasticClient>(NuSearchConfiguration.GetClient()));
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();

			app.UseDeveloperExceptionPage();
			app.UseStatusCodePages();
			app.UseStaticFiles(new StaticFileOptions
			{
				OnPrepareResponse = (context) =>
				{
					var headers = context.Context.Response.GetTypedHeaders();
					headers.CacheControl = new CacheControlHeaderValue
					{
						MaxAge = TimeSpan.FromSeconds(60),
					};
				}
			});

			app.UseMvc(routes =>
			{
				routes
					.MapRoute(name: "default", template:"{controller=Search}/{action=Index}"); 
			});
		}
    }
}
