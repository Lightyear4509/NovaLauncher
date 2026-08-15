using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaLauncher.Infrastructure.Logging;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class JsonLinesFileLoggerProviderTests
{
    [Fact]
    public void LoggerWritesOneStructuredJsonRecord()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"NovaLauncher-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "test.jsonl");

        try
        {
            using (var provider = new JsonLinesFileLoggerProvider(path))
            {
                provider.CreateLogger("Test").Log(
                    LogLevel.Information,
                    new EventId(1, "FoundationReady"),
                    "Foundation ready",
                    exception: null,
                    static (state, _) => state);
            }

            var lines = File.ReadAllLines(path);
            var record = JsonDocument.Parse(Assert.Single(lines)).RootElement;

            Assert.Equal("Information", record.GetProperty("Level").GetString());
            Assert.Equal("Test", record.GetProperty("Category").GetString());
            Assert.Equal("Foundation ready", record.GetProperty("Message").GetString());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
