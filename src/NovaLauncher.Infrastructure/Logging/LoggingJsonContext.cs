using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace NovaLauncher.Infrastructure.Logging;

[JsonSerializable(typeof(JsonLinesFileLoggerProvider.LogRecord))]
[ExcludeFromCodeCoverage(Justification = "System.Text.Json source generation supplies the executable members.")]
internal sealed partial class LoggingJsonContext : JsonSerializerContext;
