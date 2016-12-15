using NuSearch.Domain.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace NuSearch.Domain.Data
{
	public class NugetDumpReader
	{
		private readonly string[] _files;
		private readonly XmlSerializer _serializer;
		private readonly XmlSerializer _dumpSerializer;


		public int Count { get; set; }

		public NugetDumpReader(string dumpDirectory)
		{
			this._files = Directory.GetFiles(dumpDirectory, "nugetdump-*.xml");
			this.Count = this._files.Count();
			this._serializer = new XmlSerializer(typeof(FeedPackage));
			this._dumpSerializer = new XmlSerializer(typeof(NugetDump));
		}

		public IEnumerable<NugetDump> Dumps => this._files.Select(this.EagerlyReadDump);
		public IEnumerable<FeedPackage> Packages => this._files.SelectMany(this.LazilyReadDumps);


		public NugetDump EagerlyReadDump(string f)
		{
			using (var file = File.Open(f, FileMode.Open))
				return (NugetDump)this._dumpSerializer.Deserialize(file);
		}

		public IEnumerable<FeedPackage> LazilyReadDumps(string file)
		{
			var reader = XmlReader.Create(file);
			while (reader.ReadToFollowing("FeedPackage"))
			{
				var dumpReader = reader.ReadSubtree();
				yield return (FeedPackage)this._serializer.Deserialize(dumpReader);
			}
		}
		
		
		public IEnumerable<Package> GetPackages()
		{
			var currentId = string.Empty;
			var versions = new List<FeedPackage>();
			foreach (var packageVersion in this.Packages)
			{
				if (packageVersion.Id != currentId && currentId != string.Empty)
				{
					yield return new Package(versions);
					versions = new List<FeedPackage>();

				}
				currentId = packageVersion.Id;
				versions.Add(packageVersion);

			}
		}
	}
}
