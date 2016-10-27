using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using NuSearch.Domain;
using NuSearch.Domain.Model;

namespace NuSearch.Web.Search
{
	public class SuggestModule : NancyModule
	{
		public SuggestModule()
		{

			Post["/suggest"] = x =>
			{
				var form = this.Bind<SearchForm>();
				var client = NuSearchConfiguration.GetClient();
				var result = client.Search<Package>(s => s
					.Index<Package>()
					.Source(sf => sf
						.Includes(f => f
							.Field(ff => ff.Id)
							.Field(ff => ff.DownloadCount)
							.Field(ff => ff.Summary)
						)
					)
					.Suggest(su => su
						.Completion("package-suggestions", c => c
							.Prefix(form.Query)
							.Field(p => p.Suggest)
						)
					)
	
				);

				var suggestions = result.Suggest["package-suggestions"]
					.FirstOrDefault()
					.Options
					.Select(suggest => new 
					{
						id = suggest.Source.Id,
						downloadCount = suggest.Source.DownloadCount,
						summary = !string.IsNullOrEmpty(suggest.Source.Summary) 
							? string.Concat(suggest.Source.Summary.Take(200)) 
							: string.Empty
					});

				return Response.AsJson(suggestions);
			};
}
	}
}
