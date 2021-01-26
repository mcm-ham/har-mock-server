using HarMockServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Service.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace HarMockServer
{
    public class Startup
    {
        private readonly IConfiguration _config;

        public Startup(IConfiguration config)
        {
            _config = config;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpProxy();
            services.AddSingleton<Mocks>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHttpProxy httpProxy)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            });

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("/{**catch-all}", async httpContext =>
                {
                    var mocks = httpContext.RequestServices.GetRequiredService<Mocks>();
                    var logger = httpContext.RequestServices.GetRequiredService<ILogger<Startup>>();

                    var match = mocks.Files.Values
                        .SelectMany(v => v.Log.Entries)
                        .Where(e => new Uri(e.Request.Url).AbsolutePath.ToLower() == httpContext.Request.Path.Value.ToLower())
                        .FirstOrDefault();

                    // Found match in HAR file mock api use HAR response
                    if (match != null)
                    {
                        logger.LogInformation($"Mocking API {new Uri(match.Request.Url).AbsolutePath}");

                        foreach (var header in match.Response.Headers)
                        {
                            if (!new[] { "content-length", "content-encoding" }.Any(h =>  header.Name.ToLower() == h))
                                httpContext.Response.Headers.TryAdd(header.Name, header.Value);
                        }

                        httpContext.Response.StatusCode = match.Response.Status;
                        await httpContext.Response.WriteAsync(match.Response.Content.Text);
                        return;
                    }

                    // No match found, forward request to original api
                    await httpProxy.ProxyAsync(httpContext, _config.GetValue<string>("Api:Url"), httpClient);

                    // Log errors from forwarded request
                    var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
                    if (errorFeature != null)
                    {
                        logger.LogError(errorFeature.Exception, $"{errorFeature.Error}");
                    }
                });
            });
        }
    }
}
