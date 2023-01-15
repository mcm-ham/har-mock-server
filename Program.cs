using System.Net;
using HarMockServer;
using Serilog;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(
    (hostingContext, loggerConfiguration) =>
    {
        loggerConfiguration.Enrich
            .FromLogContext()
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

app.UseRouting();

app.Map(
    "/{**catch-all}",
    async httpContext =>
    {
        var mocks = httpContext.RequestServices.GetRequiredService<Mocks>();
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("HarMockServer");
        var httpForwarder = httpContext.RequestServices.GetRequiredService<IHttpForwarder>();

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
        await httpForwarder.SendAsync(
            httpContext,
            app.Configuration.GetValue<string>("ApiUrl")
                ?? throw new NullReferenceException("ApiUrl"),
            httpClient
        );

        // Log errors from forwarded request
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        if (errorFeature != null)
        {
            logger.LogError(errorFeature.Exception, "{Error}", errorFeature.Error);
        }
    }
);

app.Run();
