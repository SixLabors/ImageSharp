using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageSharp.Web.Sample
{
    using ImageSharp.Web.Caching;
    using ImageSharp.Web.Commands;
    using ImageSharp.Web.DependencyInjection;
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Resolvers;

    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Add the default service and options.
            services.AddImageSharp();

            //// Or add the default service and custom options.
            //services.AddImageSharp(
            //    options =>
            //        {
            //            options.Configuration = Configuration.Default;
            //            options.MaxBrowserCacheDays = 7;
            //            options.MaxCacheDays = 365;
            //            options.OnValidate = _ => { };
            //            options.OnProcessed = _ => { };
            //            options.OnPrepareResponse = _ => { };
            //        });

            //// Or we can fine-grain control adding the default options and configure all other services.
            //services.AddImageSharpCore()
            //        .SetUriParser<QueryCollectionUriParser>()
            //        .SetCache<PhysicalFileSystemCache>()
            //        .AddResolver<PhysicalFileSystemResolver>()
            //        .AddProcessor<ResizeWebProcessor>();


            //// Or we can fine-grain control adding custom options and configure all other services.
            //services.AddImageSharpCore(
            //        options =>
            //            {
            //                options.Configuration = Configuration.Default;
            //                options.MaxBrowserCacheDays = 7;
            //                options.MaxCacheDays = 365;
            //                options.OnValidate = _ => { };
            //                options.OnProcessed = _ => { };
            //                options.OnPrepareResponse = _ => { };
            //            })
            //        .SetUriParser<QueryCollectionUriParser>()
            //        .SetCache<PhysicalFileSystemCache>()
            //        .AddResolver<PhysicalFileSystemResolver>()
            //        .AddProcessor<ResizeWebProcessor>();

            // There are also factory methods for each builder that will allow building from configuration files.
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            app.UseDefaultFiles();
            app.UseImageSharp();
            app.UseStaticFiles();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
        }
    }
}
