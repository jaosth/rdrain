namespace rdrain
{
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;

    /// <summary>
    /// Entry point
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Standard web host
        /// </summary>
        public static void Main(string[] args)
        {
            var webHost = 
                WebHost.CreateDefaultBuilder(args)
                    .UseStartup<Startup>()
                    .Build();

            webHost.Run();
        }
    }
}
