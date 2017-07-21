using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using static System.StringComparison;


namespace NuSearch.Web.Plumbing
{
	public class ModuleConvention : IControllerModelConvention
	{
		public const string PropertyName = "module";
		public void Apply(ControllerModel controller) =>
            controller.Properties.Add(PropertyName, GetModuleNameFromNamespace(controller.ControllerType));
		
        private static string GetModuleNameFromNamespace(TypeInfo controllerType)
        {
	        var ns = typeof(Program).Namespace.Split('.').Length;
	        return controllerType.FullName.Split('.')
		        .Skip(ns + 1) // + .Modules
            	.FirstOrDefault();
        }

		public static void AddMvc(MvcOptions mvc) => mvc.Conventions.Add(new ModuleConvention());

	}
}