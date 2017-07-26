using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nest;
using NuSearch.Domain.Model;

namespace NuSearch.Web.Modules.Packages.PackageDetails
{
	public class PackageDetailsController : Controller
    {
		private readonly IElasticClient _client;

		public PackageDetailsController(IElasticClient client) => _client = client;

		[HttpGet]
		[Route("packages/{id}")]
        public IActionResult PackageDetails(string id)
		{
			var result = _client.Get<Package>(id);
			var model = new PackageDetailsViewModel
			{
				Package = result.Source,
				Referer = Request.Headers.TryGetValue("Referer", out StringValues r ) ? r.ToString() : null
			};

			return View(model);
		}
    }
}
