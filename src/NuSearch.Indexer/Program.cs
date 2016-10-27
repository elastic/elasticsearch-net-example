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
		private static string CurrentIndexName { get; set; }

		static void Main(string[] args)
		{
			Client = NuSearchConfiguration.GetClient();
			DumpReader = new NugetDumpReader(@"C:\nuget-data");
			CurrentIndexName = NuSearchConfiguration.CreateIndexName();

			CreateIndex();
			IndexDumps();
			SwapAlias();

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
				.Nested<PackageAuthor>(n => n
					.Name(p => p.Authors.First())
					.Properties(props=>props
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
			Console.WriteLine("Reading all the packages into memory...");
			var packages = DumpReader.GetPackages();

			Console.WriteLine("Indexing documents into elasticsearch...");
			var partitions = packages.Partition(1000);
			foreach (var partition in partitions)
			{
				var result = Client.IndexMany(partition, CurrentIndexName);

				if (!result.IsValid)
				{
					foreach (var item in result.ItemsWithErrors)
						Console.WriteLine("Failed to index document {0}: {1}", item.Id, item.Error);
					Console.WriteLine(result.DebugInformation);
					Console.Read();
					Environment.Exit(1);
				}
			}
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
