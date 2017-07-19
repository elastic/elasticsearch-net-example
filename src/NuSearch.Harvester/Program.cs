using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuSearch.Domain.Data;
using NuSearch.Domain.Model;
using NuSearch.Domain;
using NuSearch.Harvester.Nuget;

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
			var repo = Repository.Factory.GetCoreV3(NugetODataFeedUrl);
			var httpSource = HttpSource.Create(repo);
			var reader = new NugetFeedReader(httpSource, NugetODataFeedUrl);
			var logger = NullLogger.Instance;
			var totalPackageCount = reader.GetCountAsync(logger, CancellationToken.None).Result;

			var dumpPath = args.Length > 0 && !string.IsNullOrEmpty(args[0]) ? args[0] : NuSearchConfiguration.PackagePath;
			var numberOfPackages = args.Length > 1 && int.TryParse(args[1], out int n) ? n : totalPackageCount;
			var partitionSize = args.Length > 2 && int.TryParse(args[2], out int p) ? p : 1000;

			Console.WriteLine($"Downloading packages from {NugetODataFeedUrl} to {dumpPath}");

			var startTime = DateTime.Now;
			var take = Math.Min(100, numberOfPackages);
			var numberOfPages = (int)Math.Ceiling((double)numberOfPackages / take);

			Console.WriteLine($"Total {numberOfPages} pages to download");

			var sync = new object();
			var packages = new List<FeedPackage>(partitionSize);
			int page = 0, partition = 0;
			var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4 };
			var searchFilter = new SearchFilter(true, null) { OrderBy = SearchOrderBy.Id };

			Parallel.For(0, numberOfPages, parallelOptions, (i, state) =>
			{
				var foundPackages = reader.GetPackagesAsync(null, searchFilter, i * take, take, logger, CancellationToken.None).Result;

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

			if (packages.Count > 0)
			{
				WritePackages(packages, dumpPath, partition);
				partition++;
				Console.WriteLine($"Downloaded {page}/{numberOfPages} pages, written {partition} files");
			}

			var span = DateTime.Now - startTime;
			Console.WriteLine($"Harvesting completed in: {span}");
			Console.WriteLine("Press Enter to continue");
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
