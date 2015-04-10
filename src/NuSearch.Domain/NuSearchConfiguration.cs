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

		public static string LiveIndexAlias { get { return "nusearch"; } }
		public static string OldIndexAlias { get { return "nusearch-old"; } }

		public static Uri CreateUri(int port)
		{
			var host = "localhost";
			if (Process.GetProcessesByName("fiddler").Any())
				host = "ipv4.fiddler";

			return new Uri("http://" + host + ":" + port);
		}

		static NuSearchConfiguration()
		{
			_connectionSettings = new ConnectionSettings(CreateUri(9200));
		}

		public static ElasticClient GetClient()
		{
			return new ElasticClient(_connectionSettings);
		}

		public static string CreateIndexName()
		{
			return string.Format("{0}-{1:dd-MM-yyyy-HH-mm-ss}", LiveIndexAlias, DateTime.UtcNow);
		}

	}
}
