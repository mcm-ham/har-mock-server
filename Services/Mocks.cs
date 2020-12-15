using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HarMockServer.Services
{
    public class Mocks
    {
        public ConcurrentDictionary<string, HarFile> Files { get; } = new ConcurrentDictionary<string, HarFile>();
    }
}
