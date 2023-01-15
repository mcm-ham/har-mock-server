using System.Collections.Concurrent;

namespace HarMockServer;

public class Mocks
{
    public ConcurrentDictionary<string, HarFile> Files { get; } =
        new ConcurrentDictionary<string, HarFile>();
}
