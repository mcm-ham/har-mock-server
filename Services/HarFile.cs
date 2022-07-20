public class HarFile
{
    public HarFileLog Log { get; set; } = new HarFileLog();
}

public class HarFileLog
{
    public HarFileEntry[] Entries { get; set; } = new HarFileEntry[0];
}

public class HarFileEntry
{
    public HarFileTimings Timings { get; set; } = new HarFileTimings();
    public HarFileRequest Request { get; set; } = new HarFileRequest();
    public HarFileResponse Response { get; set; } = new HarFileResponse();
}

public class HarFileTimings
{
    public double Wait { get; set; }
}

public class HarFileRequest
{
    public string? Method { get; set; }
    public string? Url { get; set; }
    public HarFileHeader[] Headers { get; set; } = new HarFileHeader[0];
    public HarFileContent Content { get; set; } = new HarFileContent();
}

public class HarFileResponse
{
    public int Status { get; set; }
    public HarFileHeader[] Headers { get; set; } = new HarFileHeader[0];
    public HarFileContent Content { get; set; } = new HarFileContent();
}

public class HarFileHeader
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}

public class HarFileContent
{
    public string? MimeType { get; set; }
    public string? Text { get; set; }
}