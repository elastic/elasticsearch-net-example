using NuSearch.Domain.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NuSearch.Domain.Data
{
	public class NugetDumpReader
	{
		private readonly string[] _files;
		private readonly XmlSerializer _serializer;

		public int Count { get; set; }

		public NugetDumpReader(string dumpDirectory)
		{
			this._files = Directory.GetFiles(dumpDirectory, "nugetdump-*.xml");
			this.Count = this._files.Count();
			this._serializer = new XmlSerializer(typeof(NugetDump));
		}

		public IEnumerable<NugetDump> Dumps
		{
			get
			{
				foreach (var f in this._files)
					using (var file = File.OpenText(f))
						yield return (NugetDump)this._serializer.Deserialize(file);
			}

		}

		public IEnumerable<Package> GetPackages()
		{
			var packages = new Dictionary<string, Package>();

			foreach (var dump in this.Dumps)
				foreach (var feedPackage in dump.NugetPackages)
				{
					if (packages.ContainsKey(feedPackage.Id))
					{
						var version = new PackageVersion(feedPackage);
						packages[feedPackage.Id].Versions.Add(version);
					}
					else
					{
						var package = new Package(feedPackage);
						packages.Add(package.Id, package);
					}
				}

			return packages.Values;
		}
	}
}
