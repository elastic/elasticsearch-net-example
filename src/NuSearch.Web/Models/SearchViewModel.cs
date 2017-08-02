using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nest;
using NuSearch.Domain.Model;
using NuSearch.Web.Extensions;

namespace NuSearch.Web.Models
{
	public class SearchViewModel
	{
		private static readonly ReadOnlyCollection<IHit<Package>> EmptyHits = 
			new ReadOnlyCollection<IHit<Package>>(new List<IHit<Package>>());

		/// <summary>
		/// The current state of the form that was submitted
		/// </summary>
		public SearchForm Form { get; set; }

		/// <summary>
		/// The total number of matching results
		/// </summary>
		public long Total { get; set; }
		
		/// <summary>
		/// The total number of pages
		/// </summary>
		public int TotalPages { get; set; }
		
		/// <summary>
		/// The current page of package results
		/// </summary>
		public IReadOnlyCollection<IHit<Package>> Hits { get; set; }
		
		/// <summary>
		/// Returns how long the elasticsearch query took in milliseconds
		/// </summary>
		public int ElapsedMilliseconds { get; set; }

		/// <summary>
		/// Returns the top authors with the most packages
		/// </summary>
		public Dictionary<string, long?> Authors { get; set; }
		
		/// <summary>
		/// Returns the top tags for the current search
		/// </summary>
		public Dictionary<string, long> Tags { get; set; }

		public SearchViewModel()
		{
			this.Hits = EmptyHits;
			this.Form = new SearchForm();
			this.Authors = new Dictionary<string, long?>();
			this.Tags = new Dictionary<string, long>();
		}

		public string UrlForFirstPage(Action<SearchForm> alter) => this.UrlFor(form =>
		{
			alter(form);
			form.Page = 1;
		});

		public string UrlFor(Action<SearchForm> alter)
		{
			var clone = this.Form.Clone();
			alter(clone);
			return clone.ToQueryString();
		}

		public string UrlFor(Package package) => $"https://www.nuget.org/packages/{package.Id}";
	}
}
