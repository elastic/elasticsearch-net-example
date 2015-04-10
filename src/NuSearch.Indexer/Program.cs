using Nest;
using NuSearch.Domain.Data;
using NuSearch.Domain.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using NuSearch.Domain;
using NuSearch.Domain.Extensions;
using ShellProgressBar;

namespace NuSearch.Indexer
{
	class Program
	{
		private static ElasticClient Client { get; set; }
		private static NugetDumpReader DumpReader { get; set; }

		static void Main(string[] args)
		{
			Client = NuSearchConfiguration.GetClient();
			DumpReader = new NugetDumpReader(@"C:\nuget-data");

			IndexDumps();

			Console.Read();
		}

		static void IndexDumps()
		{
			var packages = DumpReader.Dumps.Take(1).First().NugetPackages;
			
			foreach (var package in packages)
			{
				var result = Client.Index(package);

				if (!result.IsValid)
				{
					Console.WriteLine(result.ConnectionStatus.OriginalException.Message);
					Console.Read();
					Environment.Exit(1);
				}
			}

			Console.WriteLine("Done.");
		}
	}
}
