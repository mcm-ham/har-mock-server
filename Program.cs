using System.Net;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using Yarp.ReverseProxy.Forwarder;

namespace HarMockServer;

public class Program
{
    private const string logTemplate = "[{@t:HH:mm:ss} {@l:w4} {SourceContext}] {@m}\n{@x}";

    /// <summary>
    /// HAR mock server provides the ability to mock API requests using HAR files. This can be useful for
    /// testing or reproduction of reported bugs where a HAR file has been captured in the browser.
    /// </summary>
    /// <param name="apiUrl">The destination API url to proxy request to if no match in HAR file is found. Required.</param>
    /// <param name="harsFolder">The folder to load HAR files from. Default current directory.</param>
    /// <param name="args">Optional arguments passed to ASP.NET Configuration default host. See https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/#command-line-arguments</param>
    public static void Main(string apiUrl, string[] args, string? harsFolder = null)
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel
            .Information()
            .WriteTo.Console(new ExpressionTemplate(logTemplate, theme: TemplateTheme.Code))
            .CreateLogger();

        try
        {
            Run(apiUrl, harsFolder, args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void Run(string apiUrl, string? harsFolder, string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseSetting("HarsFolder", harsFolder ?? Directory.GetCurrentDirectory());
        builder.WebHost.UseSetting("AllowedHosts", "*");
        builder.Host.UseSerilog(
            (hostingContext, loggerConfiguration) =>
            {
                loggerConfiguration.MinimumLevel
                    .Information()
                    .WriteTo.Console(new ExpressionTemplate(logTemplate, theme: TemplateTheme.Code))
                    .Enrich.FromLogContext()
                    .ReadFrom.Configuration(hostingContext.Configuration);
            }
        );
        builder.Services.AddHostedService<HarFilesWatcher>();
        builder.Services.AddReverseProxy();
        builder.Services.AddSingleton<Mocks>();

        var app = builder.Build();

        var httpClient = new HttpMessageInvoker(
            new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            }
        );

        app.Map(
            "/{**catch-all}",
            async httpContext =>
            {
                var mocks = httpContext.RequestServices.GetRequiredService<Mocks>();
                var logger = httpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("HarMockServer");
                var httpForwarder =
                    httpContext.RequestServices.GetRequiredService<IHttpForwarder>();

                var match = mocks.Files.Values
                    .SelectMany(v => v.Log.Entries)
                    .FirstOrDefault(
                        e =>
                            e.Request.Url != null
                            && new Uri(e.Request.Url).AbsolutePath.ToLower()
                                == httpContext.Request.Path.Value?.ToLower()
                            && e.Response._error != "net::ERR_ABORTED"
                    );

                // Found match in HAR file mock api use HAR response
                if (match != null)
                {
                    logger.LogInformation(
                        "Mocking API {AbsolutePath}, Status {Status}",
                        new Uri(match.Request.Url!).AbsolutePath,
                        match.Response.Status
                    );

                    // Simulate API delay in receiving response
                    await Task.Delay((int)match.Timings.Wait);

                    foreach (var header in match.Response.Headers)
                    {
                        if (
                            header.Name != null
                            && !new[]
                            {
                                "content-length",
                                "content-encoding",
                                "traceparent",
                                "tracestate"
                            }.Any(h => header.Name.ToLower() == h)
                        )
                            httpContext.Response.Headers.TryAdd(header.Name, header.Value);
                    }

                    httpContext.Response.StatusCode = match.Response.Status;
                    await httpContext.Response.WriteAsync(match.Response.Content.Text ?? "");
                    return;
                }

                // No match found, forward request to original api
                await httpForwarder.SendAsync(httpContext, apiUrl, httpClient);

                // Log errors from forwarded request
                var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                if (errorFeature != null)
                {
                    logger.LogError(errorFeature.Exception, "{Error}", errorFeature.Error);
                }
            }
        );

        app.Run();
    }
}
