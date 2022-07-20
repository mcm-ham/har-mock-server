using System.Collections.Concurrent;

public class Mocks
{
    public ConcurrentDictionary<string, HarFile> Files { get; } = new ConcurrentDictionary<string, HarFile>();
}