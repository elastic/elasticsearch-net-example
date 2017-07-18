using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using NuSearch.Domain.Data;
using NuSearch.Domain.Model;
using NuSearch.Domain;
using Simple.OData.Client;

namespace NuSearch.Harvester
{
	class Program
	{
		private const string NugetODataFeedUrl = "https://www.nuget.org/api/v2/";

		//// args[0] = Dump path
		//// args[1] = Number of packages to fetch
		//// args[2] = Dump partition size
		static void Main(string[] args)
		{
			var client = new ODataClient(NugetODataFeedUrl);
			var totalPackageCount = client.For<FeedPackage>("packages").Count().FindScalarAsync<int>().Result;
			var dumpPath = string.IsNullOrEmpty(args[0]) ? NuSearchConfiguration.PackagePath : args[0];

			Console.WriteLine($"Downloading packages from {NugetODataFeedUrl} to {dumpPath}");

			var numberOfPackages = args[1] == "0" ? totalPackageCount : Convert.ToInt32(args[1]);
			var partitionSize = args[2] == "0" ? totalPackageCount : Convert.ToInt32(args[2]);
			var startTime = DateTime.Now;
			var take = Math.Min(100, numberOfPackages);
			var numberOfPages = (int)Math.Ceiling((double)numberOfPackages / take);

			Console.WriteLine($"Total {numberOfPages} pages to download");

			var sync = new object();
			var packages = new List<FeedPackage>(partitionSize);
			int page = 0, partition = 0;
			var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4 };

			Parallel.For(0, numberOfPages, parallelOptions, (i, state) =>
			{
				var foundPackages = client.For<FeedPackage>("packages")
					.Skip(i * take)
					.Top(take)
					.FindEntriesAsync().Result;

				lock (sync)
				{
					packages.AddRange(foundPackages);
					if (packages.Count >= partitionSize)
					{
						WritePackages(packages, dumpPath, partition);
						partition++;
						packages = new List<FeedPackage>(partitionSize);
					}
				}
				Interlocked.Increment(ref page);
				Console.WriteLine($"Downloaded {page}/{numberOfPages} pages, written {partition} files");
			});

			var span = DateTime.Now - startTime;
			Console.WriteLine($"Harvesting completed in: {span}");
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

			using (var stream = File.OpenWrite(dumpFile))
			using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true }))
			{
				serializer.Serialize(writer, dump);
			}
		}
	}
}
