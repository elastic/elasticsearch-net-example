using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSearch.Domain.Model
{
	public class PackageDependency
	{
		public PackageDependency()
		{

		}
		public PackageDependency(string name)
		{
			this.Name = name;
		}
		public PackageDependency(string name, string version) : this(name)
		{
			this.Version = version;
		}
		public PackageDependency(string name, string version, string framework) : this(name, version)
		{
			this.Framework = framework;
		}

		public string Name { get; set; }
		public string Version { get; set; }
		public string Framework { get; set; }
	}
}
