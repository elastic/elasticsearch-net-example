using Nancy;
using Owin;

namespace NuSearch.Web.Plumbing
{
	public class Bootstrap
	{
		public void Configuration(IAppBuilder app)
		{
			app.UseNancy(x =>
			{
				x.Bootstrapper = new CustomConventionsNancy();
			});
		}

	}
}
