using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSearch.Web.Search
{
	public enum SearchSort
	{
		Relevance,
		Downloads,
		Recent
	}

	public class SearchForm
	{
		public int Page { get; set; }
		public string Query { get; set; }
		public string Author { get; set; }
		public int PageSize { get; set; }
		public SearchSort Sort { get; set; }

		public SearchForm()
		{
			this.PageSize = 25;
			this.Page = 1;
		}

		public SearchForm Clone()
		{
			return new SearchForm()
			{
				Page = this.Page,
				Query = this.Query,
				Author = this.Author,
				PageSize = this.PageSize,
				Sort = this.Sort,
			};
		}
	}
}
