using System;

namespace NuSearch.Web.Models
{
	public enum SearchSort
	{
		Relevance,
		Downloads,
		Recent
	}

	public class SearchForm
	{
		public const int DefaultPageSize = 10;
		public const int DefaultPage = 1;
		public const SearchSort DefaultSort = SearchSort.Relevance;

		public int Page { get; set; }
		public bool Significance { get; set; }
		public string Query { get; set; }
		public string Author { get; set; }
		public string[] Tags { get; set; }
		public int PageSize { get; set; }
		public SearchSort Sort { get; set; }

		public SearchForm()
		{
			this.PageSize = DefaultPageSize;
			this.Page = DefaultPage;
			this.Sort = DefaultSort;
			this.Tags = Array.Empty<string>();
		}

		public SearchForm Clone() => new SearchForm
		{
			Page = this.Page,
			Significance = this.Significance,
			Query = this.Query,
			Author = this.Author,
			Tags = this.Tags,
			PageSize = this.PageSize,
			Sort = this.Sort,
		};
	}


}