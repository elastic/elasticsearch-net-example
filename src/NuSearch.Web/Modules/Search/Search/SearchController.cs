using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nest;
using NuSearch.Domain.Model;

namespace NuSearch.Web.Modules.Search.Search
{
	public class SearchController : Controller
	{
		private readonly IElasticClient _client;

		public SearchController(IElasticClient client) => _client = client;

		[HttpGet]
		public IActionResult Search(SearchForm form)
		{
			var result = _client.Search<Package>(s => s
				.From((form.Page - 1) * form.PageSize)
				.Size(form.PageSize)
				.Sort(sort => ApplySort(form.Sort, sort))
				.Aggregations(aggs=> ApplyAggregations(form, aggs))
				.Query(q => ApplyQuery(form, q))
			);

			var authors = result.Aggs.Nested("authors")
				.Terms("author-names")
				.Buckets
				.ToDictionary(k => k.Key, v => v.DocCount);

			Dictionary<string, long> tags;
			if (form.Significance)
				tags = result.Aggs.SignificantTerms("tags")
					.Buckets
					.ToDictionary(k => k.Key, v => v.DocCount);
			else
				tags = result.Aggs.Terms("tags")
					.Buckets
					.ToDictionary(k => k.Key, v => v.DocCount ?? 0);

			var model = new SearchViewModel
			{
				Hits = result.Hits,
				Total = result.Total,
				Form = form,
				TotalPages = (int) Math.Ceiling(result.Total / (double) form.PageSize),
				Authors = authors,
				Tags = tags
			};

			return View(model);
		}

		private static QueryContainer ApplyQuery(SearchForm form, QueryContainerDescriptor<Package> q) =>
			(ExactIdKeywordMatch(form, q) || QueryWithRelevancyTunedBasedOnDownloadCount(form, q))
			&& FilterAuthorSelection(form, q)
			&& FilterTagSelection(form, q);

		// creates a big boolean must query of all the selected tags as term filters (note the + infront of q)
		private static QueryContainer FilterTagSelection(SearchForm form, QueryContainerDescriptor<Package> q) =>
			form.Tags.Aggregate(new QueryContainer(), (c, s) => c && +q.Term(p => p.Tags, s), c => c);

		private static QueryContainer FilterAuthorSelection(SearchForm form, QueryContainerDescriptor<Package> q) => +q
			.Nested(n => n
				.Path(p => p.Authors)
				.Query(nq => +nq
					.Term(p => p.Authors.First().Name.Suffix("raw"), form.Author)
				)
			);

		private static QueryContainer QueryWithRelevancyTunedBasedOnDownloadCount(SearchForm form, QueryContainerDescriptor<Package> q) => q
			.FunctionScore(fs => fs
				.MaxBoost(10)
				.Functions(ff => ff
					.FieldValueFactor(fvf => fvf
						.Field(p => p.DownloadCount)
						.Factor(0.0001)
					)
				)
				.Query(query => query
					.MultiMatch(m => m
						.Fields(f => f
							.Field(p => p.Id.Suffix("keyword"), 1.5)
							.Field(p => p.Id, 1.5)
							.Field(p => p.Summary, 0.8)
						)
						.Operator(Operator.And)
						.Query(form.Query)
					)
				)
			);

		private static QueryContainer ExactIdKeywordMatch(SearchForm form, QueryContainerDescriptor<Package> q) => q
			.Match(m => m
				.Field(p => p.Id.Suffix("keyword"))
				.Boost(1000)
				.Query(form.Query)
			);

		//todo return with NEST 5.5.1 ApplyAuthorsAggregation(form) && ApplyTagsAggregation(form);
		private static IAggregationContainer ApplyAggregations(SearchForm form, AggregationContainerDescriptor<Package> aggs) =>
			ApplyAuthorsAggregation(form, ApplyTagsAggregation(form, aggs));

		private static AggregationContainerDescriptor<Package> ApplyAuthorsAggregation(SearchForm form, AggregationContainerDescriptor<Package> aggs) =>
			aggs.Nested("authors", n => n
				.Path("authors")
				.Aggregations(a => a
					.Terms("author-names", t => t.Field(p => p.Authors.First().Name.Suffix("raw"))))
			);

		private static AggregationContainerDescriptor<Package> ApplyTagsAggregation(SearchForm form, AggregationContainerDescriptor<Package> aggs) => 
			form.Significance 
				? aggs.SignificantTerms("tags", t => t.Field(p => p.Tags)) 
				: aggs.Terms("tags", t => t.Field(p=>p.Tags));

		private static IPromise<IList<ISort>> ApplySort(SearchSort searchSort, SortDescriptor<Package> sort)
		{
			switch (searchSort)
			{
				case SearchSort.Downloads:
					return sort.Descending(p => p.DownloadCount);
				case SearchSort.Recent:
					return sort.Field(sortField => sortField
						.NestedPath(p => p.Versions)
						.Field(p => p.Versions.First().LastUpdated)
						.Descending()
					);
				case SearchSort.Relevance:
				default:
					return sort.Descending("_score");
			}
		}
	}
}