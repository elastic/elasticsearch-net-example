using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuSearch.Domain.Model;

namespace NuSearch.Harvester.Nuget
{
	// simpler implementation of V2FeedParser from https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Protocol/LegacyFeed/V2FeedParser.cs
	// This implementation includes missing properties and a count method.
	public class NugetFeedReader
	{
		private const string W3Atom = "http://www.w3.org/2005/Atom";
		private const string MetadataNS = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
		private const string DataServicesNS = "http://schemas.microsoft.com/ado/2007/08/dataservices";

		// XNames used in the feed
		private static readonly XName _xnameEntry = XName.Get("entry", W3Atom);
		private static readonly XName _xnameTitle = XName.Get("title", W3Atom);
		private static readonly XName _xnameContent = XName.Get("content", W3Atom);
		private static readonly XName _xnameLink = XName.Get("link", W3Atom);
		private static readonly XName _xnameProperties = XName.Get("properties", MetadataNS);
		private static readonly XName _xnameId = XName.Get("Id", DataServicesNS);
		private static readonly XName _xnameVersion = XName.Get("Version", DataServicesNS);
		private static readonly XName _xnameSummary = XName.Get("summary", W3Atom);
		private static readonly XName _xnameDescription = XName.Get("Description", DataServicesNS);
		private static readonly XName _xnameIconUrl = XName.Get("IconUrl", DataServicesNS);
		private static readonly XName _xnameLicenseUrl = XName.Get("LicenseUrl", DataServicesNS);
		private static readonly XName _xnameProjectUrl = XName.Get("ProjectUrl", DataServicesNS);
		private static readonly XName _xnameTags = XName.Get("Tags", DataServicesNS);
		private static readonly XName _xnameReportAbuseUrl = XName.Get("ReportAbuseUrl", DataServicesNS);
		private static readonly XName _xnameDependencies = XName.Get("Dependencies", DataServicesNS);
		private static readonly XName _xnameRequireLicenseAcceptance = XName.Get("RequireLicenseAcceptance", DataServicesNS);
		private static readonly XName _xnameDownloadCount = XName.Get("DownloadCount", DataServicesNS);
		private static readonly XName _xnameCreated = XName.Get("Created", DataServicesNS);
		private static readonly XName _xnamePublished = XName.Get("Published", DataServicesNS);
		private static readonly XName _xnameName = XName.Get("name", W3Atom);
		private static readonly XName _xnameAuthor = XName.Get("author", W3Atom);
		private static readonly XName _xnamePackageHash = XName.Get("PackageHash", DataServicesNS);
		private static readonly XName _xnamePackageHashAlgorithm = XName.Get("PackageHashAlgorithm", DataServicesNS);
		private static readonly XName _xnameMinClientVersion = XName.Get("MinClientVersion", DataServicesNS);
		private static readonly XName _xnamePackageSize = XName.Get("PackageSize", DataServicesNS);
		private static readonly XName _xnameVersionDownloadCount = XName.Get("VersionDownloadCount", DataServicesNS);
		private static readonly XName _xnameCopyright = XName.Get("Copyright", DataServicesNS);
		private static readonly XName _xnameLicenseNames = XName.Get("LicenseNames", DataServicesNS);
		private static readonly XName _xnameLicenseReportUrl = XName.Get("LicenseReportUrl", DataServicesNS);
		private static readonly XName _xnameGalleryDetailsUrl = XName.Get("GalleryDetailsUrl", DataServicesNS);
		private static readonly XName _xnameReleaseNotes = XName.Get("ReleaseNotes", DataServicesNS);
		private static readonly XName _xnameLastUpdated = XName.Get("LastUpdated", DataServicesNS);
		private static readonly XName _xnameLastEdited = XName.Get("LastEdited", DataServicesNS);
		private static readonly XName _xnameIsAbsoluteLatestVersion = XName.Get("IsAbsoluteLatestVersion", DataServicesNS);
		private static readonly XName _xnameIsLatestVersion = XName.Get("IsLatestVersion", DataServicesNS);
		private static readonly XName _xnameIsPrelease = XName.Get("IsPrerelease", DataServicesNS);
		private static readonly XName _xnamePackageUrl = XName.Get("PackageUrl", DataServicesNS);
		private static readonly XName _xnameLanguage = XName.Get("Language", DataServicesNS);

		private readonly HttpSource _httpSource;
		private readonly string _baseAddress;
		private readonly V2FeedQueryBuilder _queryBuilder;

		public NugetFeedReader(HttpSource httpSource, string baseAddress)
			: this(httpSource, baseAddress, baseAddress)
		{
		}

		public NugetFeedReader(HttpSource httpSource, string baseAddress, string source)
		{
			_httpSource = httpSource ?? throw new ArgumentNullException(nameof(httpSource));
			_baseAddress = baseAddress?.Trim('/') ?? throw new ArgumentNullException(nameof(baseAddress));
			_queryBuilder = new V2FeedQueryBuilder();
			Source = source ?? throw new ArgumentNullException(nameof(source));
		}

		public string Source { get; }

		public async Task<IEnumerable<FeedPackage>> GetPackagesAsync(
			string searchTerm,
			SearchFilter filters,
			int skip,
			int take,
			ILogger log,
			CancellationToken token)
		{
			var uri = _queryBuilder.BuildGetPackagesUri(searchTerm, filters, skip, take);
			var page = await QueryV2FeedAsync(uri, null, take, false, log, token);
			return page;
		}

		public async Task<int> GetCountAsync(ILogger log, CancellationToken token)
		{
			var uri = string.Format("{0}/Packages/$count", _baseAddress);
			return await _httpSource.ProcessResponseAsync(
				new HttpSourceRequest(
					() =>
					{
						var request = HttpRequestMessageFactory.Create(HttpMethod.Get, uri, log);
						request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
						request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
						return request;
					}),
				async response =>
				{
					if (response.StatusCode == HttpStatusCode.OK)
					{
						var intString = await response.Content.ReadAsStringAsync();
						return int.Parse(intString);
					}

					throw new FatalProtocolException(string.Format(
						CultureInfo.CurrentCulture,
						"The V2 feed at '{0}' returned an unexpected status code '{1} {2}'.",
						uri,
						(int)response.StatusCode,
						response.ReasonPhrase));
				},
				log,
				token);
		}

		private static IEnumerable<FeedPackage> ParsePage(XDocument doc, string id, MetadataReferenceCache metadataCache)
		{
			return doc.Root.Name == _xnameEntry 
				? new List<FeedPackage> { ParsePackage(id, doc.Root, metadataCache) } 
				: doc.Root.Elements(_xnameEntry).Select(x => ParsePackage(id, x, metadataCache));
		}

		private static FeedPackage ParsePackage(string id, XElement element, MetadataReferenceCache metadataCache)
		{
			var properties = element.Element(_xnameProperties);
			var idElement = properties.Element(_xnameId);
			var titleElement = element.Element(_xnameTitle);
			var identityId = metadataCache.GetString(idElement?.Value ?? titleElement?.Value ?? id);
			var versionString = properties.Element(_xnameVersion).Value;
			var downloadUrl = metadataCache.GetString(element.Element(_xnameContent).Attribute("src").Value);
			var title = metadataCache.GetString(titleElement?.Value);
			var summary = metadataCache.GetString(GetString(element, _xnameSummary));
			var description = metadataCache.GetString(GetString(properties, _xnameDescription));
			var iconUrl = metadataCache.GetString(GetString(properties, _xnameIconUrl));
			var licenseUrl = metadataCache.GetString(GetString(properties, _xnameLicenseUrl));
			var projectUrl = metadataCache.GetString(GetString(properties, _xnameProjectUrl));
			var reportAbuseUrl = metadataCache.GetString(GetString(properties, _xnameReportAbuseUrl));
			var tags = metadataCache.GetString(GetString(properties, _xnameTags));
			var dependencies = metadataCache.GetString(GetString(properties, _xnameDependencies));
			var downloadCount = metadataCache.GetString(GetString(properties, _xnameDownloadCount));
			var requireLicenseAcceptance = StringComparer.OrdinalIgnoreCase.Equals(bool.TrueString, GetString(properties, _xnameRequireLicenseAcceptance));
			var packageHash = metadataCache.GetString(GetString(properties, _xnamePackageHash));
			var packageHashAlgorithm = metadataCache.GetString(GetString(properties, _xnamePackageHashAlgorithm));
			var copyright = metadataCache.GetString(GetString(properties, _xnameCopyright));
			var galleryDetailsUrl = metadataCache.GetString(GetString(properties, _xnameGalleryDetailsUrl));
			var releaseNotes = metadataCache.GetString(GetString(properties, _xnameReleaseNotes));
			var minClientVersionString = GetString(properties, _xnameMinClientVersion);
			var created = GetDate(properties, _xnameCreated);
			var published = GetDate(properties, _xnamePublished);
			var lastUpdated = GetDate(properties, _xnameLastUpdated);
			var lastEdited = GetDate(properties, _xnameLastEdited);
			var isAbsoluteLatestVersion = StringComparer.OrdinalIgnoreCase.Equals(bool.TrueString, GetString(properties, _xnameIsAbsoluteLatestVersion));
			var isLatestVersion = StringComparer.OrdinalIgnoreCase.Equals(bool.TrueString, GetString(properties, _xnameIsLatestVersion));
			var isPrerelease = StringComparer.OrdinalIgnoreCase.Equals(bool.TrueString, GetString(properties, _xnameIsPrelease));
			var language = metadataCache.GetString(GetString(properties, _xnameLanguage));
			var licenseNames = metadataCache.GetString(GetString(properties, _xnameLicenseNames));
			var licenseReportUrl = metadataCache.GetString(GetString(properties, _xnameLicenseReportUrl));
			var packageUrl = metadataCache.GetString(GetString(properties, _xnamePackageUrl));
			var packageSize = GetLong(properties, _xnamePackageSize);
			var versionDownloadCount = GetInt(properties, _xnameVersionDownloadCount);

			string authors = null;
			var authorNode = element.Element(_xnameAuthor);
			if (authorNode != null)
			{
				authors = string.Join(" ",authorNode.Elements(_xnameName).Select(e => metadataCache.GetString(e.Value)));
			}

			return new FeedPackage
			{
				Authors = authors,
				Copyright = copyright,
				Created = created,
				Dependencies = dependencies,
				Description = description,
				DownloadCount = int.TryParse(downloadCount, out int d) ? d : 0,
				DownloadUrl = downloadUrl,
				GalleryDetailsUrl = galleryDetailsUrl,
				IconUrl = iconUrl,
				Id = identityId,
				IsAbsoluteLatestVersion = isAbsoluteLatestVersion,
				IsLatestVersion = isLatestVersion,
				IsPreRelease = isPrerelease,
				Language = language,
				LastUpdated = lastUpdated,
				LastEdited = lastEdited,
				LicenseNames = licenseNames,
				LicenseReportUrl = licenseReportUrl,
				LicenseUrl = licenseUrl,
				MinClientVersion = minClientVersionString,
				PackageHash = packageHash,
				PackageHashAlgorithm = packageHashAlgorithm,
				ProjectUrl = projectUrl,
				PackageSize = packageSize,
				PackageUrl = packageUrl,
				Published = published,
				ReleaseNotes = releaseNotes,
				ReportAbuseUrl = reportAbuseUrl,
				RequireLicenseAcceptance = requireLicenseAcceptance,
				Summary = summary,
				Tags = tags,
				Title = title,
				Version = versionString,
				VersionDownloadCount = versionDownloadCount
			};
		}

		private static string GetString(XElement parent, XName childName)
		{
			var child = parent?.Element(childName);
			return child?.Value;
		}

		private static int GetInt(XElement parent, XName childName)
		{
			var intString = GetString(parent, childName);
			return int.TryParse(intString, out int value) ? value : 0;
		}

		private static long GetLong(XElement parent, XName childName)
		{
			var longString = GetString(parent, childName);
			return long.TryParse(longString, out long value) ? value : 0;
		}

		private static DateTime GetDate(XElement parent, XName childName)
		{
			var dateString = GetString(parent, childName);
			return DateTime.TryParse(dateString, out DateTime date) 
				? date 
				: new DateTime(1901, 01, 01);
		}

		private async Task<IEnumerable<FeedPackage>> QueryV2FeedAsync(
			string relativeUri,
			string id,
			int max,
			bool ignoreNotFounds,
			ILogger log,
			CancellationToken token)
		{
			var metadataCache = new MetadataReferenceCache();
			var results = new List<FeedPackage>();
			var uris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var page = 1;

			var uri = string.Format("{0}{1}", _baseAddress, relativeUri);
			uris.Add(uri);

			// first request
			Task<XDocument> docRequest = LoadXmlAsync(uri, ignoreNotFounds, log, token);

			// TODO: re-implement caching at a higher level for both v2 and v3
			string nextUri = null;
			while (!token.IsCancellationRequested && docRequest != null)
			{
				// TODO: Pages for a package Id are cached separately.
				// So we will get inaccurate data when a page shrinks.
				// However, (1) In most cases the pages grow rather than shrink;
				// (2) cache for pages is valid for only 30 min.
				// So we decide to leave current logic and observe.
				var doc = await docRequest;
				if (doc != null)
				{
					var result = ParsePage(doc, id, metadataCache);
					results.AddRange(result);

					nextUri = GetNextUrl(doc);
				}

				docRequest = null;
				if (max < 0 || results.Count < max)
				{
					// Request the next url in parallel to parsing the current page
					if (!string.IsNullOrEmpty(nextUri))
					{
						// a bug on the server side causes the same next link to be returned
						// for every page. To avoid falling into an infinite loop we must
						// keep track of all uri and error out for any duplicate uri which means
						// potential bug at server side.

						if (!uris.Add(nextUri))
						{
							throw new FatalProtocolException(string.Format(
								CultureInfo.CurrentCulture,
								"'{0}' is a duplicate url which has already been downloaded and will lead to a cyclic dependency. Please correct from server.",
								nextUri));
						}

						docRequest = LoadXmlAsync(nextUri, ignoreNotFounds, log, token);
					}

					page++;
				}
			}

			if (max > -1 && results.Count > max)
			{
				// Remove extra results if the page contained extras
				results = results.Take(max).ToList();
			}

			return results;
		}

		private async Task<XDocument> LoadXmlAsync(
			string uri,
			bool ignoreNotFounds,
			ILogger log,
			CancellationToken token)
		{
			return await _httpSource.ProcessResponseAsync(
				new HttpSourceRequest(
					() =>
					{
						var request = HttpRequestMessageFactory.Create(HttpMethod.Get, uri, log);
						request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
						request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
						return request;
					}),
				async response =>
				{
					if (response.StatusCode == HttpStatusCode.OK)
					{
						var networkStream = await response.Content.ReadAsStreamAsync();
						return await LoadXmlAsync(networkStream);
					}
					else if (ignoreNotFounds && response.StatusCode == HttpStatusCode.NotFound)
					{
						// Treat "404 Not Found" as an empty response.
						return null;
					}
					else if (response.StatusCode == HttpStatusCode.NoContent)
					{
						// Always treat "204 No Content" as exactly that.
						return null;
					}
					else
					{
						throw new FatalProtocolException(string.Format(
							CultureInfo.CurrentCulture,
							"The V2 feed at '{0}' returned an unexpected status code '{1} {2}'.",
							uri,
							(int)response.StatusCode,
							response.ReasonPhrase));
					}
				},
				log,
				token);
		}

		private static string GetNextUrl(XDocument doc)
		{
			// Example of what this looks like in the odata feed:
			// <link rel="next" href="{nextLink}" />
			return (from e in doc.Root.Elements(_xnameLink)
				let attr = e.Attribute("rel")
				where attr != null && string.Equals(attr.Value, "next", StringComparison.OrdinalIgnoreCase)
				select e.Attribute("href") into nextLink
				where nextLink != null
				select nextLink.Value).FirstOrDefault();
		}

		private static async Task<XDocument> LoadXmlAsync(Stream stream)
		{
			using (var memStream = await stream.AsSeekableStreamAsync())
			{
				var xmlReader = XmlReader.Create(memStream, new XmlReaderSettings
				{
					CloseInput = true,
					IgnoreWhitespace = true,
					IgnoreComments = true,
					IgnoreProcessingInstructions = true,
					DtdProcessing = DtdProcessing.Ignore, // for consistency with earlier behavior (v3.3 and before)
				});

				return XDocument.Load(xmlReader, LoadOptions.None);
			}
		}
	}
}
