using System;
using System.Linq;
using Nancy.Helpers;

namespace NuSearch.Web.Plumbing
{
	public static class ExtensionMethods
	{
		/// <summary>
		/// Not best practice but during workshop allows us to not worry about how folks model their search form
		/// </summary>
		public static string ToQueryString(this object obj)
		{
			var properties = from p in obj.GetType().GetProperties()
				where p.GetValue(obj, null) != null
				let value = HttpUtility.UrlEncode(p.GetValue(obj, null).ToString())
				where !string.IsNullOrEmpty(value)
				select p.Name.ToLowerInvariant() + "=" + value;

			return "?" + string.Join("&", properties.ToArray());
		}
	}
}