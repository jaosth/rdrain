namespace rdrain
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using RoofDrain.Services;

    /// <summary>
    /// Standard startup
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// See MVC documentation
        /// </summary>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Helper to access configuration
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// See MVC documentation
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddScoped<IUpdateService,UpdateService>();
            services.AddScoped<IStateService,StateService>();
        }

        /// <summary>
        /// See MVC documentation
        /// </summary>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
