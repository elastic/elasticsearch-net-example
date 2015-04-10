using System;
using System.IO;
using Nancy;
using Nancy.Conventions;
using Nancy.Responses;
using Nancy.TinyIoc;

namespace NuSearch.Web.Plumbing
{
	public class CustomConventionsNancy : DefaultNancyBootstrapper
	{
		protected override void ApplicationStartup(TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
		{
			StaticConfiguration.Caching.EnableRuntimeViewUpdates = true;

			this.Conventions.ViewLocationConventions.Add((viewName, model, context) =>
			{
				//default is to just cut Model of razor view model type name
				//but we're more explicitly using ViewModel
				viewName = viewName.Replace("View", "");

				//this convention allows you to place your razor views in the same folder as your modules
				return string.Concat(context.ModuleName, "/", viewName);
			});

			this.Conventions.StaticContentsConventions.Add(
				StaticContentConventionBuilder.AddDirectory("static", @"static")
			);

		}
	}

	public class MyRootPathProvider : IRootPathProvider
	{
		public static readonly string RootPath;
		static MyRootPathProvider()
		{
			var root = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			if (System.Diagnostics.Debugger.IsAttached)
				root = Path.GetFullPath(Path.Combine(root, "..\\.."));
			RootPath = root;
		}

		public string GetRootPath()
		{
			return RootPath;
		}
	}
}