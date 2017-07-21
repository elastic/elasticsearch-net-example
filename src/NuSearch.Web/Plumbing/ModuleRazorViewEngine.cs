using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;

namespace NuSearch.Web.Plumbing
{
	public class ModuleRazorViewEngine : IViewLocationExpander
	{
		public IEnumerable<string> ExpandViewLocations(
			ViewLocationExpanderContext context, 
			IEnumerable<string> viewLocations) 
		{
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (viewLocations == null) throw new ArgumentNullException(nameof(viewLocations));

			var controllerActionDescriptor = context.ActionContext.ActionDescriptor as ControllerActionDescriptor;
			if (controllerActionDescriptor == null) 
				throw new NullReferenceException("ControllerActionDescriptor cannot be null.");

			var moduleName = controllerActionDescriptor.Properties[ModuleConvention.PropertyName] as string;
			foreach (var location in viewLocations) 
				yield return location.Replace("{2}", moduleName);
		}

		public void PopulateValues(ViewLocationExpanderContext context) { }

		public const string RootFolder = "Modules";
		public static void AddRazorOptions(RazorViewEngineOptions razor)
		{
			//0 - action
			//1 - controller
			//2 - module
			razor.ViewLocationFormats.Clear();
			razor.ViewLocationFormats.Add($"/{RootFolder}/{{2}}/{{1}}/{{0}}.cshtml");
			razor.ViewLocationFormats.Add($"/{RootFolder}/{{2}}/{{1}}/_Partials/{{0}}.cshtml");
			razor.ViewLocationFormats.Add($"/{RootFolder}/{{2}}/_Partials/{{0}}.cshtml");
			razor.ViewLocationFormats.Add($"/{RootFolder}/_Layout/{{0}}.cshtml");
			razor.ViewLocationFormats.Add($"/{RootFolder}/_Layout/_Partials/{{0}}.cshtml");
			
			razor.ViewLocationExpanders.Add(new ModuleRazorViewEngine());
		}

	}
}