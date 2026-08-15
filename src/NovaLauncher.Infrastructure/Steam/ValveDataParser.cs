namespace NovaLauncher.Infrastructure.Steam;

public sealed class ValveDataException(string message) : FormatException(message);

public sealed class ValveDataNode
{
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<KeyValuePair<string, object>> Values => _values;

    public string? GetString(string key) =>
        _values.TryGetValue(key, out var value) ? value as string : null;

    public ValveDataNode? GetNode(string key) =>
        _values.TryGetValue(key, out var value) ? value as ValveDataNode : null;

    internal void Add(string key, object value)
    {
        if (!_values.TryAdd(key, value))
        {
            throw new ValveDataException($"Duplicate key '{key}'.");
        }
    }
}

public static class ValveDataParser
{
    public const int MaximumCharacters = 4 * 1024 * 1024;
    private const int MaximumDepth = 32;
    private const int MaximumTokens = 250_000;
    private const int MaximumTokenLength = 32_768;

    public static ValveDataNode Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > MaximumCharacters)
        {
            throw new ValveDataException("Valve data exceeds the size limit.");
        }

        var reader = new TokenReader(content);
        var root = ParseObject(reader, 0, stopAtBrace: false);
        if (reader.Read() is not null)
        {
            throw new ValveDataException("Unexpected trailing data.");
        }

        return root;
    }

    private static ValveDataNode ParseObject(TokenReader reader, int depth, bool stopAtBrace)
    {
        if (depth > MaximumDepth)
        {
            throw new ValveDataException("Valve data nesting is too deep.");
        }

        var node = new ValveDataNode();
        while (true)
        {
            var key = reader.Read();
            if (key is null)
            {
                if (stopAtBrace)
                {
                    throw new ValveDataException("Valve data object is not closed.");
                }

                return node;
            }

            if (key == "}")
            {
                if (!stopAtBrace)
                {
                    throw new ValveDataException("Unexpected closing brace.");
                }

                return node;
            }

            if (key == "{")
            {
                throw new ValveDataException("Expected a key before an object.");
            }

            var value = reader.Read() ?? throw new ValveDataException($"Missing value for '{key}'.");
            node.Add(key, value == "{" ? ParseObject(reader, depth + 1, stopAtBrace: true) : value);
        }
    }

    private sealed class TokenReader(string content)
    {
        private int _position;
        private int _tokens;

        public string? Read()
        {
            SkipTrivia();
            if (_position >= content.Length)
            {
                return null;
            }

            if (++_tokens > MaximumTokens)
            {
                throw new ValveDataException("Valve data contains too many tokens.");
            }

            var current = content[_position++];
            if (current is '{' or '}')
            {
                return current.ToString();
            }

            if (current == '"')
            {
                return ReadQuoted();
            }

            var start = _position - 1;
            while (_position < content.Length &&
                   !char.IsWhiteSpace(content[_position]) &&
                   content[_position] is not '{' and not '}')
            {
                _position++;
            }

            return ValidateToken(content[start.._position]);
        }

        private string ReadQuoted()
        {
            var value = new System.Text.StringBuilder();
            while (_position < content.Length)
            {
                var current = content[_position++];
                if (current == '"')
                {
                    return ValidateToken(value.ToString());
                }

                if (current == '\\')
                {
                    if (_position >= content.Length)
                    {
                        throw new ValveDataException("Unterminated escape sequence.");
                    }

                    var escaped = content[_position++];
                    value.Append(escaped switch { 'n' => '\n', 't' => '\t', _ => escaped });
                }
                else if (char.IsControl(current) && current is not '\r' and not '\n' and not '\t')
                {
                    throw new ValveDataException("Valve data contains a disallowed control character.");
                }
                else
                {
                    value.Append(current);
                }

                if (value.Length > MaximumTokenLength)
                {
                    throw new ValveDataException("Valve data token exceeds the length limit.");
                }
            }

            throw new ValveDataException("Unterminated quoted string.");
        }

        private void SkipTrivia()
        {
            while (_position < content.Length)
            {
                if (char.IsWhiteSpace(content[_position]))
                {
                    _position++;
                    continue;
                }

                if (content[_position] == '/' && _position + 1 < content.Length && content[_position + 1] == '/')
                {
                    _position += 2;
                    while (_position < content.Length && content[_position] is not '\r' and not '\n')
                    {
                        _position++;
                    }

                    continue;
                }

                break;
            }
        }

        private static string ValidateToken(string value) =>
            value.Length <= MaximumTokenLength
                ? value
                : throw new ValveDataException("Valve data token exceeds the length limit.");
    }
}
