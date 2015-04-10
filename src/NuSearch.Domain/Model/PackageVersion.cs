using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSearch.Domain.Model
{
	public class PackageVersion
	{
		public PackageVersion()
		{

		}

		public PackageVersion(FeedPackage feed)
		{
			this.Version = feed.Version;
			this.Created = feed.Created;
			this.Description = feed.Description;
			this.GalleryDetailsUrl = feed.GalleryDetailsUrl;
			this.IsLatestVersion = feed.IsLatestVersion;
			this.IsPreRelease = feed.IsPreRelease;
			this.Language = feed.Language;
			this.LastUpdated = feed.LastUpdated;
			this.Published = feed.Published;
			this.PackageHash = feed.PackageHash;
			this.PackageHashAlgorithm = feed.PackageHashAlgorithm;
			this.PackageSize = feed.PackageSize;
			this.PackageUrl = feed.PackageUrl;
			this.ReportAbuseUrl = feed.ReportAbuseUrl;
			this.ReleaseNotes = feed.ReleaseNotes;
			this.RequireLicenseAcceptance = feed.RequireLicenseAcceptance;
			this.Tags = feed.Tags;
			this.Title = feed.Title;
			this.DownloadCount = feed.VersionDownloadCount;
			this.MinClientVersion = feed.MinClientVersion;
			this.LastEdited = feed.LastEdited;
			this.LicenseUrl = feed.LicenseUrl;
			this.LicenseNames = feed.LicenseNames;
			this.LicenseReportUrl = feed.LicenseReportUrl;

			if (!string.IsNullOrEmpty(feed.Dependencies))
			{
				this.Dependencies = feed.Dependencies.Split('|').Select(d =>
				{
					var parts = d.Split(':');
					switch(parts.Length)
					{
						case 1:
							return new PackageDependency(parts[0]);
						case 2:
							return new PackageDependency(parts[0], parts[1]);
						case 3:
							return new PackageDependency(parts[0], parts[1], parts[2]);
						default:
							return new PackageDependency(d);
					}
				}).ToList();
			}
		}

		public string Version { get; set; }
		public DateTime Created { get; set; }
		public List<PackageDependency> Dependencies { get; set; }
		public string Description { get; set; }
		public string GalleryDetailsUrl { get; set; }
		public string IconUrl { get; set; }
		public bool IsLatestVersion { get; set; }
		public bool IsAbsoluteLatestVersion { get; set; }
		public bool IsPreRelease { get; set; }
		public string Language { get; set; }
		public DateTime LastUpdated { get; set; }
		public DateTime Published { get; set; }
		public string PackageHash { get; set; }
		public string PackageHashAlgorithm { get; set; }
		public long PackageSize { get; set; }
		public string PackageUrl { get; set; }
		public string ReportAbuseUrl { get; set; }
		public string ReleaseNotes { get; set; }
		public bool RequireLicenseAcceptance { get; set; }
		public string Summary { get; set; }
		public string Tags { get; set; }
		public string Title { get; set; }
		public int DownloadCount { get; set; }
		public string MinClientVersion { get; set; }
		public DateTime LastEdited { get; set; }
		public string LicenseUrl { get; set; }
		public string LicenseNames { get; set; }
		public string LicenseReportUrl { get; set; }
	}
}
