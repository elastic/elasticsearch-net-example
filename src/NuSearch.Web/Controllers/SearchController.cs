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
			// TODO: You'll write most of the implementation here
			var model = new SearchViewModel();

	        return View(model);
		}
    }
}
