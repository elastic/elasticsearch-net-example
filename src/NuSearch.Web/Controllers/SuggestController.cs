using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nest;
using NuSearch.Domain.Model;
using NuSearch.Web.Models;

namespace NuSearch.Web.Controllers
{
	public class SuggestController : Controller
    {
		private readonly IElasticClient _client;

		public SuggestController(IElasticClient client) => _client = client;

		[HttpPost]
        public IActionResult Index([FromBody]SearchForm form)
        {
			var result = _client.Search<Package>(s => s
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

			return Json(suggestions);
		}
    }
}
