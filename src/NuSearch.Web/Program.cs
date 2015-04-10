using System;
using Microsoft.Owin.Hosting;
using NuSearch.Domain;
using NuSearch.Web.Plumbing;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace NuSearch.Web
{
	class Program
	{
		static void Main(string[] args)
		{
			var url = "http://+:8080";

			var options = new StartOptions
			{
				ServerFactory = "Nowin",
				Port = 8080
			};

			var loggerConfig = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(NuSearchConfiguration.CreateUri(9200))
				{
					AutoRegisterTemplate = true,
					Period = TimeSpan.FromSeconds(2)
				});

			Serilog.Log.Logger = loggerConfig.CreateLogger();

			using (WebApp.Start<Bootstrap>(options))
			{
				Console.WriteLine("Running on {0}", url);
				Console.WriteLine("Press enter to exit");
				Console.ReadLine();
			}


		}
	}
}
