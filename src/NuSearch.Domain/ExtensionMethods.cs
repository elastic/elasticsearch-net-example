using System;
using System.Linq;
using Nancy.Helpers;

namespace NuSearch.Web.Plumbing
{
	public static class ExtensionMethods
	{
		/// <summary>
		/// Not best practice but during workshop allows us to not worry about how folks model there search form
		/// </summary>
		public static string ToQueryString(this object obj)
		{
			var properties = from p in obj.GetType().GetProperties()
				where p.GetValue(obj, null) != null
				let value = HttpUtility.UrlEncode(p.GetValue(obj, null).ToString())
				where !string.IsNullOrEmpty(value)
				select p.Name.ToLowerInvariant() + "=" + value;

			return "?" + String.Join("&", properties.ToArray());
		}
	}
		public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> source, int size)
		{
			T[] array = null;
			int count = 0;
			foreach (T item in source)
			{
				if (array == null)
				{
					array = new T[size];
				}
				array[count] = item;
				count++;
				if (count == size)
				{
					yield return new ReadOnlyCollection<T>(array);
					array = null;
					count = 0;
				}
			}
			if (array != null)
			{
				Array.Resize(ref array, count);
				yield return new ReadOnlyCollection<T>(array);
			}
		}
}