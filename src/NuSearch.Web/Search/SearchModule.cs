using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Owin;
using NuSearch.Domain;
using NuSearch.Domain.Model;
using Nest;
using static Nest.Infer;

namespace NuSearch.Web.Search
{
	public class SearchModule : NancyModule
	{
		public SearchModule()
		{
			Get["/"] = x =>
			{
				var form = this.Bind<SearchForm>();
				var model = new SearchViewModel();
				var client = NuSearchConfiguration.GetClient();

				var result = client.Search<Package>(s => s
					.From((form.Page - 1) * form.PageSize)
					.Size(form.PageSize)
					.Sort(sort =>
					{
						if (form.Sort == SearchSort.Downloads)
							return sort.Descending(p => p.DownloadCount);
						else if (form.Sort == SearchSort.Recent)
							return sort.Field(sortField => sortField
								.NestedPath(p => p.Versions)
								.Field(p => p.Versions.First().LastUpdated)
								.Descending()
							);
						else return sort.Descending("_score");
					})
					.Aggregations(a => a
						.Nested("authors", n => n
							.Path("authors")
							.Aggregations(aa => aa
								.Terms("author-names", ts => ts
									.Field(p => p.Authors.First().Name.Suffix("raw"))
								)
							)
						)
					)
					.Query(q => 
						(q.Match(m => m.Field(p => p.Id.Suffix("keyword")).Boost(1000).Query(form.Query))
						|| q.FunctionScore(fs => fs
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
						))
						&& +q.Nested(n=>n
						    .Path(p=>p.Authors)
						    .Query(nq=>+nq.Term(p=>p.Authors.First().Name.Suffix("raw"), form.Author))
						)
					)
				);
				model.Packages = result.Documents;
				model.Total = result.Total;
				model.Form = form;
				model.TotalPages = (int)Math.Ceiling(result.Total / (double)form.PageSize);

				var authors = result.Aggs.Nested("authors")
					.Terms("author-names")
					.Buckets
					.ToDictionary(k => k.Key, v => v.DocCount);

				model.Authors = authors;

				return View[model];
			};
		}
	}
}
