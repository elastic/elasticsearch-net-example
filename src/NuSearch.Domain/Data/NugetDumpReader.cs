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

		public int Count { get; set; }

		public NugetDumpReader(string dumpDirectory)
		{
			this._files = Directory.GetFiles(dumpDirectory, "nugetdump-*.xml");
			this.Count = this._files.Count();
			this._serializer = new XmlSerializer(typeof(FeedPackage));
		}

		public IEnumerable<FeedPackage> Dumps => this._files.SelectMany(this.LazilyReadDumps);

		public IEnumerable<FeedPackage> LazilyReadDumps(string file)
		{
			var reader = XmlReader.Create(file);
			while (reader.ReadToFollowing("FeedPackage"))
			{
				var dumpReader = reader.ReadSubtree();
				yield return (FeedPackage)this._serializer.Deserialize(dumpReader);
			}
		}
		
		/*
		public IEnumerable<Package> GetPackages()
		{
			var currentId = string.Empty;
			var versions = new List<FeedPackage>();
			foreach (var packageVersion in this.Dumps)
			{
				if (packageVersion.Id != currentId && currentId != string.Empty)
				{
					yield return new Package(versions);
					versions = new List<FeedPackage>();

				}
				currentId = packageVersion.Id;
				versions.Add(packageVersion);

			}
		}*/
	}
}
