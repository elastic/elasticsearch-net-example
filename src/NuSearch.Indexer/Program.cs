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

			Console.WriteLine("Press any key to exit.");
			Console.ReadKey();
		}

		private static void CreateIndex()
		{
			Client.CreateIndex(CurrentIndexName, i => i
				.Settings(s => s
					.NumberOfShards(2)
					.NumberOfReplicas(0)
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
						.Text(p => p
							.Name("keyword")
							.Analyzer("nuget-id-keyword")
						)
						.Keyword(p => p
							.Name("raw")
						)
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
				.Keyword(k => k
					.Name(p=>p.Tags)
				)
				.Nested<PackageAuthor>(n => n
					.Name(p => p.Authors.First())
					.AutoMap()
					.Properties(props => props
						.Text(t => t
							.Name(a => a.Name)
							.Fielddata()
							.Fields(fs => fs
								.Keyword(ss => ss
									.Name("raw")
								)
							)
						)
					)
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
			
			Console.Write("Indexing documents into Elasticsearch...");
			var waitHandle = new CountdownEvent(1);

			var bulkAll = Client.BulkAll(packages, b => b
				.Index(CurrentIndexName)
				.BackOffRetries(2)
				.BackOffTime("30s")
				.RefreshOnCompleted(true)
				.MaxDegreeOfParallelism(4)
				.Size(1000)
			);
	
			bulkAll.Subscribe(new BulkAllObserver(
				onNext: b => Console.Write("."),
				onError: e => throw e,
				onCompleted: () => waitHandle.Signal()
			));

			waitHandle.Wait(TimeSpan.FromMinutes(30));
			Console.WriteLine("Done.");
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
