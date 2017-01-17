using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;
using NuSearch.Domain.Model;

namespace NuSearch.Domain
{
	public class NuSearchConfiguration
	{
		private readonly ConnectionSettings _connectionSettings;

		public static string LiveIndexAlias => "nusearch";

		public static string OldIndexAlias => "nusearch-old";

		public static Uri CreateUri(string host, int port)
		{
			if (Process.GetProcessesByName("fiddler").Any())
				host = "ipv4.fiddler";

			return new Uri("http://" + host + ":" + port);
		}

		private NuSearchConfiguration(string host, int port, string userName, string password)
		{
			_connectionSettings = new ConnectionSettings(CreateUri(host, port))
				.DefaultIndex("nusearch")
				.InferMappingFor<Package>(i => i
					.TypeName("package")
					.IndexName("nusearch")
				)
				.InferMappingFor<FeedPackage>(i => i
					.TypeName("package")
					.IndexName("nusearch")
				);

			if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
			{
				_connectionSettings.BasicAuthentication(userName, password);
			}
		}

		public static NuSearchConfiguration Create(string host = "localhost", int port = 9200, string userName = "", string password = "")
		{
			return new NuSearchConfiguration(host, port, userName, password);
		}

		public ElasticClient GetClient()
		{
			return new ElasticClient(_connectionSettings);
		}

		public string CreateIndexName()
		{
			return $"{LiveIndexAlias}-{DateTime.UtcNow:dd-MM-yyyy-HH-mm-ss}";
		}
	}
}
