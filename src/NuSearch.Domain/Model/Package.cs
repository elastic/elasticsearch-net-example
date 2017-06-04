using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;

namespace NuSearch.Domain.Model
{
	public class Package
	{
		private static DateTime SpecialUnlistedDate = new DateTime(1901, 01, 01);

		public Package(List<FeedPackage> feeds)
		{
			var latestVersion = feeds.Last();

			this.Id = latestVersion.Id;
			this.Copyright = latestVersion.Copyright;
			this.IconUrl = latestVersion.IconUrl;
			this.Summary = latestVersion.Summary;
			this.DownloadCount = latestVersion.DownloadCount;
			this.Authors = latestVersion.Authors.Split('|').Select(author => new PackageAuthor {Name = author}).ToList();
			this.Versions = feeds.Select(f => new PackageVersion(f)).ToList();
			this.AllVersionsUnlisted = feeds.All(f => f.Published < SpecialUnlistedDate);

			this.Created = feeds.Min(f => f.Created);
			this.LastUpdate = feeds.Max(f => f.Created);

			this.Suggest = new CompletionField
			{
				Input = new List<string>(latestVersion.Id.Split('.')) {latestVersion.Id},
				Weight = latestVersion.DownloadCount
			};
		}

		public string Id { get; }
		public bool AllVersionsUnlisted { get; }
		public DateTime Created { get; }
		public DateTime LastUpdate { get; }
		public string Copyright { get; }
		public string IconUrl { get; }
		public string Summary { get; }
		public List<PackageAuthor> Authors { get; }
		public List<PackageVersion> Versions { get; }
		public int DownloadCount { get; }
		public CompletionField Suggest { get; }
	}
}