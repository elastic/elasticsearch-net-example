using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nest;
using NuSearch.Domain.Model;
using NuSearch.Web.Models;

namespace NuSearch.Web.Controllers
{
	public class SearchController : Controller
    {
		private readonly IElasticClient _client;

		public SearchController(IElasticClient client) => _client = client;

	    [HttpGet]
        public async Task<IActionResult> Index(SearchForm form)
        {
			var response = await _client.SearchAsync<Package>(s => s
		        .From((form.Page - 1) * form.PageSize)
		        .Size(form.PageSize)
		        .Sort(sort => ApplySort(sort, form))
		        .Aggregations(aggs => ApplyAggregations(aggs, form))
		        .Query(q => ApplyQuery(form, q))
	        );

			if (response == null) throw new Exception("elastic response is null");

			var authors = response.Aggregations.Nested("authors")
				.Terms("author-names")
				.Buckets
				.ToDictionary(k => k.Key, v => v.DocCount);

	        Dictionary<string, long> tags;
	        if (form.Significance)
		        tags = response.Aggregations.SignificantTerms("tags")
			        .Buckets
			        .ToDictionary(k => k.Key, v => v.DocCount);
	        else
		        tags = response.Aggregations.Terms("tags")
			        .Buckets
			        .ToDictionary(k => k.Key, v => v.DocCount ?? 0);

			var model = new SearchViewModel
			{
				Hits = response.Hits,
				Total = response.Total,
				Form = form,
				TotalPages = (int)Math.Ceiling(response.Total / (double)form.PageSize),
				Authors = authors,
				Tags = tags
			};

	        return View(model);
		}

	    private static AggregationContainerDescriptor<Package> ApplyAggregations(AggregationContainerDescriptor<Package> aggs, SearchForm form)
	    {
			aggs = form.Significance
			    ? aggs.SignificantTerms("tags", t => t.Field(p => p.Tags))
			    : aggs.Terms("tags", t => t.Field(p => p.Tags));

		    return aggs.Nested("authors", n => n
			    .Path(p => p.Authors)
			    .Aggregations(aa => aa
				    .Terms("author-names", ts => ts
					    .Field(p => p.Authors.First().Name.Suffix("raw"))
				    )
			    )
		    );
		}

	    private static SortDescriptor<Package> ApplySort(SortDescriptor<Package> sort, SearchForm form)
	    {
			    if (form.Sort == SearchSort.Downloads)
				    return sort.Descending(p => p.DownloadCount);
			    if (form.Sort == SearchSort.Recent)
				    return sort.Field(sortField => sortField
					    .Nested(n => n
						    .Path(p => p.Versions)
					    )
					    .Field(p => p.Versions.First().LastUpdated)
					    .Descending()
				    );

			    return sort.Descending(SortSpecialField.Score);
		}

	    private static QueryContainer ApplyQuery(SearchForm form, QueryContainerDescriptor<Package> q)
	    {
		    return (ExactIdKeywordMatch(form, q) || QueryWithRelevancyTunedBasedOnDownloadCount(form, q))
			    && FilterAuthorSelection(form, q)
			    && FilterTagSelection(form, q);
		}

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

		private static QueryContainer FilterTagSelection(
		    SearchForm form,
		    QueryContainerDescriptor<Package> q) => form.Tags.Aggregate(new QueryContainer(), (c, s) => c && +q.Term(p => p.Tags, s), c => c);
	}
}
