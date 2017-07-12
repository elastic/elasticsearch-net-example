using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace NuSearch.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
				.UseIISIntegration()
				.UseSetting("detailedErrors", "true")
				.CaptureStartupErrors(true)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
