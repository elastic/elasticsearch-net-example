using NuSearch.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSearch.Domain.Data
{
	public class NugetDump
	{
		public NugetDump()
		{
			NugetPackages = new List<FeedPackage>();
		}

		public List<FeedPackage> NugetPackages { get; set; }
	}
}
