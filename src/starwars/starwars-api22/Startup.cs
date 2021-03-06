﻿// *************************************************************
// project:  graphql-aspnet
// --
// repo: https://github.com/graphql-aspnet
// docs: https://graphql-aspnet.github.io
// --
// License:  MIT
// *************************************************************

namespace GraphQL.AspNet.StarWarsAPI
{
    using GraphQL.AspNet.Configuration.Mvc;
    using GraphQL.AspNet.Execution;
    using GraphQL.AspNet.StarwarsAPI.Common.Services;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Startup object used by the runtime to configure various settings for the web api. Your
    /// graph ql schema settings should be added and maintained here (or called here).
    /// </summary>
    public class Startup
    {
        private const string CORS_POLICY_ALL_CLIENTS = "ALL_CLIENTS";

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        /// <summary>
        /// Configures the services. This method gets called by the runtime.
        /// Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<StarWarsDataRepository>();
            services.AddScoped<IStarWarsDataService, StarWarsDataService>();

            // apply an unrestricted cors policy for the demo services
            // to allow use on many of the tools for testing (graphiql, altair etc.)
            // Do not do this in production
            services.AddCors(options =>
            {
                options.AddPolicy(
                    "_allOrigins",
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    });
            });

            // ----------------------------------------------------------
            // Add the MVC middleware
            // ----------------------------------------------------------
            // Register GraphQL with the application
            // By default graphql will scan your assembly for any GraphControllers
            // and automatically wire them up to the schema
            // you can control which assemblies are scanned and which classes are registered using
            // the schema configuration options set here.
            //
            // in this example because of the two test projects netcore2.2 and netcore3.0
            // we have moved all the shared code to a common assembly (starwars-common) and are injecting it
            // as a single unit
            //
            // Note: This demo api is being registered with minimal configuration options
            //       as razor and other pipeline options are not required.  However, this library will
            //       happily live along side data and pages served via razor etc.
            services.AddGraphQL(options =>
              {
                  options.ResponseOptions.ExposeExceptions = true;
                  options.ResponseOptions.MessageSeverityLevel = GraphMessageSeverity.Information;

                  var assembly = typeof(StarWarsDataRepository).Assembly;
                  options.AddGraphAssembly(assembly);
              });

            services.AddMvcCore()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddFormatterMappings()
                .AddJsonFormatters()
                .AddDataAnnotations();
        }

        /// <summary>
        /// Configures the specified application. This method gets called by the runtime.
        /// Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="application">The application.</param>
        public void Configure(IApplicationBuilder application)
        {
            application.AddStarWarsStartedMessageToConsole();

            application.UseCors("_allOrigins");

            application.UseMvc();

            // ************************************************************
            // Finalize the graphql setup by load the schema, build out the templates for all found graph types
            // and publish the route to hook the graphql runtime to the web.
            // be sure to register it after "UseAuthorization" if you require access to this.User
            // ************************************************************
            application.UseGraphQL();
        }

        /// <summary>
        /// Gets the configuration instance being managed by this application.
        /// </summary>
        /// <value>The configuration.</value>
        public IConfiguration Configuration { get; }
    }
}