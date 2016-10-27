using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.ViewEngines.Razor;

namespace NuSearch.Web.Plumbing
{
	public class RazorConfiguration : IRazorConfiguration
	{
		public IEnumerable<string> GetAssemblyNames()
		{
			yield return "NuSearch.Domain";
			yield return "NuSearch.Web";
		}

		public IEnumerable<string> GetDefaultNamespaces()
		{
			yield return "NuSearch.Domain.Model";
			yield return "NuSearch.Web";
		}

		public bool AutoIncludeModelNamespace => true;
	}
}
