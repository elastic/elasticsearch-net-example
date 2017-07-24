namespace NuSearch.Web.Modules.Search.Search
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
		public string Query { get; set; }
		public string Author { get; set; }
		public int PageSize { get; set; }
		public SearchSort Sort { get; set; }

		public SearchForm()
		{
			this.PageSize = DefaultPageSize;
			this.Page = DefaultPage;
			this.Sort = DefaultSort;
		}

		public SearchForm Clone() => new SearchForm
		{
			Page = this.Page,
			Query = this.Query,
			Author = this.Author,
			PageSize = this.PageSize,
			Sort = this.Sort,
		};
	}


}