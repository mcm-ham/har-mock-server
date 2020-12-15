using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HarMockServer.Services
{
    public class HarFile
    {
        public HarFileLog Log { get; set; }
    }

    public class HarFileLog
    {
        public HarFileEntry[] Entries { get; set; }
    }

    public class HarFileEntry
    {
        public HarFileRequest Request { get; set; }
        public HarFileResponse Response { get; set; }
    }

    public class HarFileRequest
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public HarFileHeader[] Headers { get; set; }
        public HarFileContent Content { get; set; }
    }

    public class HarFileResponse
    {
        public int Status { get; set; }
        public HarFileHeader[] Headers { get; set; }
        public HarFileContent Content { get; set; }
    }

    public class HarFileHeader
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class HarFileContent
    {
        public string MimeType { get; set; }
        public string Text { get; set; }
    }
}
