using System;
using System.Linq;
using System.Threading;
using Nest;
using NuSearch.Domain;
using NuSearch.Domain.Data;
using NuSearch.Domain.Model;

namespace NuSearch.Indexer
{
	class Program
	{
		private static ElasticClient Client { get; set; }
		private static NugetDumpReader DumpReader { get; set; }

		static void Main(string[] args)
		{
			Client = NuSearchConfiguration.GetClient();
			var directory = args.Length > 0 && !string.IsNullOrEmpty(args[0]) 
				? args[0] 
				: NuSearchConfiguration.PackagePath;
			DumpReader = new NugetDumpReader(directory);

			IndexDumps();

			Console.WriteLine("Press Enter to continue");
			Console.Read();
		}

		static void IndexDumps()
		{
			Console.WriteLine("Done.");
		}
	}
}
