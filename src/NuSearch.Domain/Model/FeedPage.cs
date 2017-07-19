using System;
using System.Collections.Generic;

namespace NuSearch.Domain.Model
{
	public class FeedPage
	{
		public IReadOnlyList<FeedPackage> Items { get; }
		public string NextUri { get; }

		public FeedPage(List<FeedPackage> items, string nextUri)
		{
			this.Items = items ?? throw new ArgumentNullException(nameof(items));
			this.NextUri = nextUri;
		}
	}
}