using System.Text.Json;

namespace HarMockServer;

/// <summary>
/// Monitors the HARs folder for added or removed HAR files and updates the Mocks.Files used for API mocking accordingly,
/// removing the need to restart HarMockServer each time. Runs as a background service, to learn more see
/// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
/// </summary>
public class HarFilesWatcher : BackgroundService
{
    private FileSystemWatcher? _watcher;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HarFilesWatcher> _logger;
    private readonly IServiceProvider _provider;
    private readonly IConfiguration _config;

    public HarFilesWatcher(
        IWebHostEnvironment env,
        ILogger<HarFilesWatcher> logger,
        IServiceProvider provider,
        IConfiguration config
    )
    {
        _env = env;
        _logger = logger;
        _provider = provider;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mocks = _provider.GetRequiredService<Mocks>();
        mocks.Files.Clear();
        var path =
            _config.GetValue<string>("HarsFolder")
            ?? throw new NullReferenceException("HarsFolder");
        if (!Path.IsPathRooted(path))
            path = Path.Combine(_env.ContentRootPath, path);
        var filter = "*.har";
        foreach (var filePath in Directory.GetFiles(path, filter))
        {
            var filename = Path.GetFileName(filePath);
            _logger.LogInformation("HAR file {filename} loaded.", filename);
            using var stream = File.OpenRead(filePath);
            var file = await JsonSerializer.DeserializeAsync<HarFile>(
                stream,
                options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken: stoppingToken
            );
            if (file != null)
                mocks.Files.AddOrUpdate(filename.ToLower(), file, (k, f) => file);
        }

        // Uses a file system watcher class to detect changes to HARs folder.
        _watcher = new FileSystemWatcher
        {
            Path = path,
            Filter = filter,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        return Task.CompletedTask;
    }

    private async void OnChanged(object sender, FileSystemEventArgs e)
    {
        var mocks = _provider.GetRequiredService<Mocks>();
        var change = e.ChangeType == WatcherChangeTypes.Changed ? "loaded" : "unloaded";
        _logger.LogInformation("HAR file {e.Name} {change}", e.Name, change);

        if (e.ChangeType == WatcherChangeTypes.Deleted)
        {
            if (e.Name != null)
                mocks.Files.Remove(e.Name.ToLower(), out var removedFile);
        }
        else
        {
            try
            {
                using var stream = File.OpenRead(e.FullPath);
                var file = await JsonSerializer.DeserializeAsync<HarFile>(
                    stream,
                    options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (file != null && e.Name != null)
                    mocks.Files.AddOrUpdate(e.Name.ToLower(), file, (k, f) => file);
            }
            // Suppress errors from file in use due to change event being fired multiple times when file copied to directory last one should work.
            catch (IOException) { }
        }
    }
}
