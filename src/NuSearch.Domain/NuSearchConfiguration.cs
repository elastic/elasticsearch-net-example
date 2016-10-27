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
	public static class NuSearchConfiguration
	{
		private static readonly ConnectionSettings _connectionSettings;

		public static string LiveIndexAlias => "nusearch";

		public static string OldIndexAlias => "nusearch-old";

		public static Uri CreateUri(int port)
		{
			var host = "localhost";
			if (Process.GetProcessesByName("fiddler").Any())
				host = "ipv4.fiddler";

			return new Uri("http://" + host + ":" + port);
		}

		static NuSearchConfiguration()
		{
			_connectionSettings = new ConnectionSettings(CreateUri(9200))
				.DefaultIndex("nusearch")
				.InferMappingFor<Package>(i => i
					.TypeName("package")
					.IndexName("nusearch")
				)
				.InferMappingFor<FeedPackage>(i => i
					.TypeName("package")
					.IndexName("nusearch")
				);
		}

		public static ElasticClient GetClient()
		{
			return new ElasticClient(_connectionSettings);
		}

		public static string CreateIndexName()
		{
			return $"{LiveIndexAlias}-{DateTime.UtcNow:dd-MM-yyyy-HH-mm-ss}";
		}

	}
}
