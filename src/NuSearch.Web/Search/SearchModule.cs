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

				var result = client.Search<Package>(s=>s);

				model.Packages = result.Documents;
				model.Total = result.Total;
				model.Form = form;
				return View[model];
			};

		}
	}
}
