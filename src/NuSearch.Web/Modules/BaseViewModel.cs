using NuSearch.Web.Modules.Search.Search;

namespace NuSearch.Web.Modules
{
	//custom viewpages seems broken on asp net mvc core
	//https://github.com/aspnet/Mvc/issues/5397
	//using base viewmodel instead
	public abstract class BaseViewModel
	{
		/// <summary>
		/// The current state of the form that was submitted
		/// </summary>
		public SearchForm Form { get; set; }  = new SearchForm();
		
	}
}