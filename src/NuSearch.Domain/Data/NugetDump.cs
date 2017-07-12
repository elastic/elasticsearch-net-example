using System.Collections.Generic;
using NuSearch.Domain.Model;

namespace NuSearch.Domain.Data
{
	public class NugetDump
	{
		public NugetDump() => NugetPackages = new List<FeedPackage>();

		public List<FeedPackage> NugetPackages { get; set; }
	}
}
