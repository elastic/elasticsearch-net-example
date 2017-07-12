using System.Linq;
using System.Net;
using System.Reflection;

namespace NuSearch.Web.Extensions
{
	public static class ExtensionMethods
	{
		public static string ToQueryString(this object obj)
		{
			var properties = from p in obj.GetType().GetProperties()
				where p.GetValue(obj, null) != null
				let value = WebUtility.UrlEncode(p.GetValue(obj, null).ToString())
				where !string.IsNullOrEmpty(value)
				select p.Name.ToLowerInvariant() + "=" + value;

			return "?" + string.Join("&", properties.ToArray());
		}
	}
}

