using System;
using System.Diagnostics;
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
		private static string CurrentIndexName { get; set; }

		static void Main(string[] args)
		{
			Client = NuSearchConfiguration.GetClient();
			var directory = args.Length > 0 && !string.IsNullOrEmpty(args[0]) 
				? args[0] 
				: NuSearchConfiguration.PackagePath;
			DumpReader = new NugetDumpReader(directory);
			CurrentIndexName = NuSearchConfiguration.CreateIndexName();

			CreateIndex();
			IndexDumps();
			SwapAlias();

			Console.WriteLine("Press any key to continue");
			Console.Read();
		}

		static void DeleteIndexIfExists()
		{
			if (Client.IndexExists("nusearch").Exists)
				Client.DeleteIndex("nusearch");
		}

		static void CreateIndex()
		{
			Client.CreateIndex(CurrentIndexName, i => i
				.Settings(s => s
					.NumberOfShards(2)
					.NumberOfReplicas(0)
					.RefreshInterval(-1)
					.Analysis(Analysis)
				)
				.Mappings(m => m
					.Map<Package>(MapPackage)
				)
			);
		}

		private static TypeMappingDescriptor<Package> MapPackage(TypeMappingDescriptor<Package> map) => map
			.AutoMap()
			.Properties(ps => ps
				.Text(t => t
					.Name(p => p.Id)
					.Analyzer("nuget-id-analyzer")
					.Fields(f => f
						.Text(p => p.Name("keyword").Analyzer("nuget-id-keyword"))
						.Keyword(p => p.Name("raw"))
					)
				)
				.Completion(c => c
					.Name(p => p.Suggest)
				)
				.Nested<PackageVersion>(n => n
					.Name(p => p.Versions.First())
					.AutoMap()
					.Properties(pps => pps
						.Nested<PackageDependency>(nn => nn
							.Name(pv => pv.Dependencies.First())
							.AutoMap()
						)
					)
				)
				.Keyword(k=>k.Name(p=>p.Tags))
				.Nested<PackageAuthor>(n => n
					.Name(p => p.Authors.First())
					.Properties(props => props
						.Text(t => t
							.Name(a => a.Name)
							.Fields(fs => fs
								.Keyword(ss => ss
									.Name("raw")
								)
							)
						)
					)
					.AutoMap()
				)
			);

		private static AnalysisDescriptor Analysis(AnalysisDescriptor analysis) => analysis
			.Tokenizers(tokenizers => tokenizers
				.Pattern("nuget-id-tokenizer", p => p.Pattern(@"\W+"))
			)
			.TokenFilters(tokenfilters => tokenfilters
				.WordDelimiter("nuget-id-words", w => w
					.SplitOnCaseChange()
					.PreserveOriginal()
					.SplitOnNumerics()
					.GenerateNumberParts(false)
					.GenerateWordParts()
				)
			)
			.Analyzers(analyzers => analyzers
				.Custom("nuget-id-analyzer", c => c
					.Tokenizer("nuget-id-tokenizer")
					.Filters("nuget-id-words", "lowercase")
				)
				.Custom("nuget-id-keyword", c => c
					.Tokenizer("keyword")
					.Filters("lowercase")
				)
			);

		static void IndexDumps()
		{
			Console.WriteLine("Setting up a lazy xml files reader that yields packages...");
			var packages = DumpReader.GetPackages();
			
			var sw = Stopwatch.StartNew();
			Console.Write("Indexing documents into elasticsearch: ");

			var bulkAll = Client.BulkAll(packages, b => b
				.Index(CurrentIndexName)
				.BackOffRetries(2)
				.BackOffTime("30s")
				.RefreshOnCompleted()
				.MaxDegreeOfParallelism(8)
				.Size(1000)
			);
	
			var result = bulkAll.Wait(TimeSpan.FromMinutes(30), b => Console.Write(b.Retries == 0 ? "." : "!"));

			//we can get the count because we set RefreshOnCompleted in the BulkAll helper
			//since we have not applied the alias yet we need to explicitly specify the current index name
			var count = Client.Count<Package>(c=>c.Index(CurrentIndexName));

			Console.WriteLine($"{Environment.NewLine}Took: {sw.Elapsed}.");
			Console.WriteLine($"Indexed: {count.Count} documents into {CurrentIndexName}.");
			Console.WriteLine($"Number of retries needed: {result.TotalNumberOfRetries}.");
			
			//re enable refresh after bulk all
			Client.UpdateIndexSettings(CurrentIndexName, i => i.IndexSettings(s => s.RefreshInterval("1s")));
		}

		private static void SwapAlias()
		{
			var indexExists = Client.IndexExists(NuSearchConfiguration.LiveIndexAlias).Exists;

			Client.Alias(aliases =>
			{
				if (indexExists)
					aliases.Add(a => a.Alias(NuSearchConfiguration.OldIndexAlias).Index(NuSearchConfiguration.LiveIndexAlias));

				return aliases
					.Remove(a => a.Alias(NuSearchConfiguration.LiveIndexAlias).Index("*"))
					.Add(a => a.Alias(NuSearchConfiguration.LiveIndexAlias).Index(CurrentIndexName));
			});

			var oldIndices = Client.GetIndicesPointingToAlias(NuSearchConfiguration.OldIndexAlias)
				.OrderByDescending(name => name)
				.Skip(2);

			foreach (var oldIndex in oldIndices)
				Client.DeleteIndex(oldIndex);
		}
	}
}
