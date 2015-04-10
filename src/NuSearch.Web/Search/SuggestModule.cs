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
				var result = client.Suggest<Package>(s => s
					.Index<Package>()
					.Completion("package-suggestions", c => c
						.Text(form.Query)
						.Field(p => p.Suggest)
					)
				);

				var suggestions = result.Suggestions["package-suggestions"]
					.FirstOrDefault()
					.Options
					.Select(suggest => suggest.Payload<object>());

				return Response.AsJson(suggestions);
			};
}
	}
}
