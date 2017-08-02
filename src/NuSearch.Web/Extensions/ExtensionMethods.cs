using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using NuSearch.Web.Models;

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

		public static string ToQueryString(this SearchForm form)
		{
//		public int Page { get; set; }
//		public string Query { get; set; }
//		public string Author { get; set; }
//		public string[] Tags { get; set; }
//		public int PageSize { get; set; }
//		public SearchSort Sort { get; set; }

			Func<string, string> u = WebUtility.UrlEncode;
			
			var properties = new List<string>();
			if (form.Page != SearchForm.DefaultPage) properties.Add($"page={form.Page}");
			if (form.PageSize != SearchForm.DefaultPageSize) properties.Add($"pagesize={form.PageSize}");
			if (form.Sort != SearchForm.DefaultSort) 
				properties.Add($"sort={u(form.Sort.ToString().ToLowerInvariant())}");
			if (form.Significance) properties.Add($"significance={form.Significance.ToString().ToLowerInvariant()}");
			if (!string.IsNullOrEmpty(form.Query)) properties.Add($"query={u(form.Query)}");
			if (!string.IsNullOrEmpty(form.Author)) properties.Add($"author={u(form.Author)}");
			if (form.Tags != null && form.Tags.Length > 0)
				properties.AddRange(form.Tags.Select(t => $"tags={u(t)}"));

			return "?" + string.Join("&", properties.ToArray());
		}
	}
}