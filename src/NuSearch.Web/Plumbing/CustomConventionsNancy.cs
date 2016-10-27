using System;
using System.Configuration;
using System.IO;
using Nancy;
using Nancy.Conventions;
using Nancy.Responses;
using Nancy.TinyIoc;
using NuSearch.Domain;

namespace NuSearch.Web.Plumbing
{
	public class CustomConventionsNancy : DefaultNancyBootstrapper
	{
		protected override void ApplicationStartup(TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
		{
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

		protected override void ConfigureApplicationContainer(TinyIoCContainer container)
		{
			base.ConfigureApplicationContainer(container);
			container.Register(
				NuSearchConfiguration.Create(
					ConfigurationManager.AppSettings["ElasticClient:Host"],
					int.Parse(ConfigurationManager.AppSettings["ElasticClient:Port"]),
					ConfigurationManager.AppSettings["ElasticClient:Username"],
					ConfigurationManager.AppSettings["ElasticClient:Password"]));
		}
	}
}