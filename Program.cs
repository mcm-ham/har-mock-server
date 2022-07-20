using System.Net;
using Serilog;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
{
    loggerConfiguration
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(hostingContext.Configuration);
});
builder.Services.AddHostedService<HarFilesWatcher>();
builder.Services.AddReverseProxy();
builder.Services.AddSingleton<Mocks>();

var app = builder.Build();

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
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HarMockServer");
        var httpForwarder = httpContext.RequestServices.GetRequiredService<IHttpForwarder>();

        var match = mocks.Files.Values
            .SelectMany(v => v.Log.Entries)
            .Where(e => e.Request.Url != null && new Uri(e.Request.Url).AbsolutePath.ToLower() == httpContext.Request.Path.Value?.ToLower())
            .FirstOrDefault();

        // Found match in HAR file mock api use HAR response
        if (match != null)
        {
            logger.LogInformation($"Mocking API {new Uri(match.Request.Url!).AbsolutePath}, Status {match.Response.Status}");

            // Simulate API delay in receiving response
            await Task.Delay((int)match.Timings.Wait);

            foreach (var header in match.Response.Headers)
            {
                if (header.Name != null && !new[] { "content-length", "content-encoding", "traceparent", "tracestate" }.Any(h => header.Name.ToLower() == h))
                    httpContext.Response.Headers.TryAdd(header.Name, header.Value);
            }

            httpContext.Response.StatusCode = match.Response.Status;
            await httpContext.Response.WriteAsync(match.Response.Content.Text ?? "");
            return;
        }

        // No match found, forward request to original api
        await httpForwarder.SendAsync(httpContext, app.Configuration.GetValue<string>("Api:Url"), httpClient);

        // Log errors from forwarded request
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        if (errorFeature != null)
        {
            logger.LogError(errorFeature.Exception, $"{errorFeature.Error}");
        }
    });
});

app.Run();