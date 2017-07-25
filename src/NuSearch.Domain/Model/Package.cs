using System;
using System.Collections.Generic;
using System.Linq;
using Nest;

namespace NuSearch.Domain.Model
{
	public class Package
	{
		private static readonly DateTime SpecialUnlistedDate = new DateTime(1901, 01, 01);

		public Package() { }

		public Package(List<FeedPackage> feeds)
		{
			var latestVersion = feeds.Last();

			this.Id = latestVersion.Id;
			this.Copyright = latestVersion.Copyright;
			this.IconUrl = latestVersion.IconUrl;
			this.Summary = latestVersion.Summary;
			this.DownloadCount = latestVersion.DownloadCount;
			this.Tags = latestVersion.Tags?
				        .Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries)
			            .Select(t=>t.ToLowerInvariant()) //could use a normalizer in the mapping
			            .ToArray() ?? Array.Empty<string>();
			this.Authors = latestVersion.Authors.Split('|').Select(author => new PackageAuthor { Name = author }).ToList();
			this.Versions = feeds.Select(f => new PackageVersion(f)).ToList();
			this.AllVersionsUnlisted = feeds.All(f => f.Published < SpecialUnlistedDate);

			this.Suggest = new CompletionField
			{
				Input = new List<string>(latestVersion.Id.Split('.')) { latestVersion.Id },
				Weight = latestVersion.DownloadCount
			};
		}

		public bool AllVersionsUnlisted { get; set;  }
		public string Id { get; set;  }
		public string Copyright { get; set; }
		public string IconUrl { get; set; }
		public string Summary { get; set; }
		public string[] Tags { get; set; }
		public List<PackageAuthor> Authors { get; set;  }
		public List<PackageVersion> Versions { get; set; }
		public int DownloadCount { get; set;  }
		public CompletionField Suggest { get; set;  }
	}
}
