using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HarMockServer.Services
{
    public class HarFilesWatcher : BackgroundService
    {
        private FileSystemWatcher _watcher;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<HarFilesWatcher> _logger;
        private readonly IServiceProvider _provider;
        private readonly IConfiguration _config;

        public HarFilesWatcher(IWebHostEnvironment env, ILogger<HarFilesWatcher> logger, IServiceProvider provider, IConfiguration config)
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
            var path = _config.GetValue<string>("HarsFolder");
            if (!Path.IsPathRooted(path))
                path = Path.Combine(_env.ContentRootPath, path);
            var filter = "*.har";
            foreach (var filePath in Directory.GetFiles(path, filter))
            {
                var filename = Path.GetFileName(filePath);
                _logger.LogInformation($"HAR file {filename} loaded.");
                using var stream = File.OpenRead(filePath);
                var file = await JsonSerializer.DeserializeAsync<HarFile>(stream, options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken: stoppingToken);
                mocks.Files.AddOrUpdate(filename.ToLower(), file, (k, f) => file);
            }

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
            _watcher.Dispose();
            return Task.CompletedTask;
        }

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            var mocks = _provider.GetRequiredService<Mocks>();
            var change = e.ChangeType == WatcherChangeTypes.Changed ? "loaded" : "unloaded";
            _logger.LogInformation($"HAR file {e.Name} {change}");

            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                mocks.Files.Remove(e.Name.ToLower(), out var removedFile);
            }
            else
            {
                try
                {
                    using var stream = File.OpenRead(e.FullPath);
                    var file = await JsonSerializer.DeserializeAsync<HarFile>(stream, options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    mocks.Files.AddOrUpdate(e.Name.ToLower(), file, (k, f) => file);
                }
                // Suppress errors from file in use due to change event being fired multiple times when file copied to directory last one should work.
                catch (IOException) { }
            }
        }
    }
}
