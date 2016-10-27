using NuSearch.Domain.Model;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using NuSearch.Domain.Data;
using System.Collections.ObjectModel;
using System.Threading;
using ShellProgressBar;
using Simple.OData.Client;

namespace NuSearch.Harvester
{

	class Program
	{
		// args[0] = Dump path
		// args[1] = Number of packages to fetch
		// args[2] = Dump partition size

		static void Main(string[] args)
		{
			var client = new ODataClient("https://www.nuget.org/api/v2/");

			var totalPackageCount = client.For<FeedPackage>("packages").Count().FindScalarAsync<int>().Result;

			var dumpPath = args[0];
			var numberOfPackages = (args[1] == "0") ? totalPackageCount : Convert.ToInt32(args[1]);
			var partitionSize = (args[2] == "0") ? totalPackageCount : Convert.ToInt32(args[2]);

			var startTime = DateTime.Now;
			var take = Math.Min(100, numberOfPackages);
			var numberOfPages = (int)Math.Ceiling((double)numberOfPackages / (double)take);

			var sync = new object();
			var packages = new List<FeedPackage>();
			int page = 0, partition = 0;
			var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4 };
			using (var pbar = new ProgressBar(numberOfPages, "Downloading nuget data.."))
			{
				Parallel.For(0, numberOfPages, parallelOptions, (i, state) =>
				{
					var foundPackages = client.For<FeedPackage>("packages")
					.Skip(i * take)
					.Top(take)
					.FindEntriesAsync().Result;

					lock (sync)
					{
						packages.AddRange(foundPackages);
						if (packages.Count() >= partitionSize)
						{
							WritePackages(packages, dumpPath, partition);
							partition++;
							packages = new List<FeedPackage>();
						}
					}
					Interlocked.Increment(ref page);
					pbar.Tick($"Downloaded {page}/{numberOfPages} pages, written {partition} files");
				});
			}

			var span = DateTime.Now - startTime;
			Console.WriteLine("Harvesting completed in: {0}.", span);
			Console.WriteLine("Press any key to quit");
			Console.Read();
		}

		public static void WritePackages(List<FeedPackage> packages, string dumpPath, int partition)
		{
			if (!Directory.Exists(dumpPath))
				Directory.CreateDirectory(dumpPath);

			var dump = new NugetDump { NugetPackages = packages };
			var dumpFile = Path.Combine(dumpPath, $"nugetdump-{partition}.xml");

			var serializer = new XmlSerializer(typeof(NugetDump));
			using (var writer = XmlWriter.Create(dumpFile, new XmlWriterSettings { Indent = true }))
			{
				serializer.Serialize(writer, dump);
			}
		}

	}
}
