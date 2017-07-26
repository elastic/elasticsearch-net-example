using System;
using System.Linq;
using NuSearch.Domain.Model;

namespace NuSearch.Web.Modules.Packages.PackageDetails
{
	public class PackageDetailsViewModel : BaseViewModel
	{
		private static string[] _hosts = {"localhost", "127.0.0,1", "ipv4.fiddler"};
		public string GoBackUrl
		{
			get
			{
				if (string.IsNullOrEmpty(Referer)
					|| !Uri.TryCreate(Referer, UriKind.Absolute, out Uri uri)
				    || !_hosts.Contains(uri.Host)) return "/";

				return uri.PathAndQuery;

			}

		}
		public string Referer { get; set; }
		public Package Package { get; set; }
	}
}
