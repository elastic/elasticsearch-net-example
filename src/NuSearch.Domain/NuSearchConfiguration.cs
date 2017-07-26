using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Nest;
using NuSearch.Domain.Model;

namespace NuSearch.Domain
{
	public static class NuSearchConfiguration
	{
		public class WebAppSettings
		{
			public string Username { get; set; }
			public string Password { get; set; }
			public string Host { get; set; }
		}

		public static string LiveIndexAlias => "nusearch";
		public static string OldIndexAlias => "nusearch-old";
		public static string CreateIndexName() => $"{LiveIndexAlias}-{DateTime.UtcNow:dd-MM-yyyy-HH-mm-ss}";
		
		public static ElasticClient GetClient(WebAppSettings webAppSettings = null) => 
			new ElasticClient(GetConnectionSettings(webAppSettings));

		private static ConnectionSettings GetConnectionSettings(WebAppSettings webAppSettings)
		{
			var settings = new ConnectionSettings(CreateUri(webAppSettings?.Host, 9200))
				.DefaultIndex(LiveIndexAlias)
				.InferMappingFor<Package>(i => i
					.TypeName("package")
					.IndexName(LiveIndexAlias)
				)
				.InferMappingFor<FeedPackage>(i => i
					.TypeName("package")
					.IndexName(LiveIndexAlias)
				);

			if (!string.IsNullOrEmpty(webAppSettings?.Username))
				settings.BasicAuthentication(webAppSettings.Username, webAppSettings.Password);

			return settings;
		}
		
		private static Uri CreateUri(string host, int port)
		{
			host = Process.GetProcessesByName("fiddler").Any() 
				? "ipv4.fiddler"
				: host ?? "localhost";

			return new Uri($"http://{host}:{port}");
		}


		public static string PackagePath => 
			RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\nuget-data" : "/nuget-data";
	}
}
