using System;
using System.Linq;
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
        public IActionResult Index(SearchForm form)
        {
	        var result = _client.Search<Package>(s => s
		        .From((form.Page - 1) * form.PageSize)
		        .Size(form.PageSize)
		        .Sort(sort =>
		        {
			        if (form.Sort == SearchSort.Downloads)
				        return sort.Descending(p => p.DownloadCount);
			        if (form.Sort == SearchSort.Recent)
				        return sort.Field(sortField => sortField
					        .NestedPath(p => p.Versions)
					        .Field(p => p.Versions.First().LastUpdated)
					        .Descending()
				        );
			        return sort.Descending(SortSpecialField.Score);
		        })
		        .Aggregations(a => a
			        .Nested("authors", n => n
				        .Path(p => p.Authors)
				        .Aggregations(aa => aa
					        .Terms("author-names", ts => ts
						        .Field(p => p.Authors.First().Name.Suffix("raw"))
					        )
				        )
			        )
		        )
		        .Query(q => (q
					.Match(m => m
						.Field(p => p.Id.Suffix("keyword"))
						.Boost(1000)
						.Query(form.Query)
					) || q
					.FunctionScore(fs => fs
				        .MaxBoost(50)
				        .Functions(ff => ff
					        .FieldValueFactor(fvf => fvf
						    	.Field(p => p.DownloadCount)
						    	.Factor(0.0001)
							)
				        )
				        .Query(query => query
					         .MultiMatch(m => m
						        .Fields(f => f
							        .Field(p => p.Id, 1.5)
							        .Field(p => p.Summary, 0.8)
						        )
						        .Operator(Operator.And)
						        .Query(form.Query)
					        )
				        )
			        ))
			        && +q.Nested(n => n
				        .Path(p => p.Authors)
				        .Query(nq => +nq
							.Term(p => p.Authors.First().Name.Suffix("raw"), form.Author)
						)
			        )
		        )
	        );

			var authors = result.Aggs.Nested("authors")
				.Terms("author-names")
				.Buckets
				.ToDictionary(k => k.Key, v => v.DocCount);

			var model = new SearchViewModel
			{
				Hits = result.Hits,
				Total = result.Total,
				Form = form,
				TotalPages = (int)Math.Ceiling(result.Total / (double)form.PageSize),
				Authors = authors
			};

	        return View(model);
		}
    }
}
