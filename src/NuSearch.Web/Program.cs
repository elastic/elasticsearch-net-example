using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NuSearch.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
	            .ConfigureWebHostDefaults(webBuilder =>
	            {
		            webBuilder.UseSetting("detailedErrors", "true")
			            .CaptureStartupErrors(true)
			            .UseContentRoot(Directory.GetCurrentDirectory())
			            .UseStartup<Startup>();
	            })
	            .ConfigureLogging(logging =>
	            {
		            logging.AddConsole()
			            .AddDebug();
	            })
	            .Build();

            host.Run();
        }
    }
}
