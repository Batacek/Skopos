using Skopos.Highlighting;

namespace Skopos.Services;

/// <summary>
/// Grammars load on demand, not all of them at startup. A second JSON file pays for
/// neither loading the library nor compiling the query.
/// </summary>
public sealed class AnalyzerCache : IDisposable
{
    private readonly Dictionary<string, SyntaxAnalyzer?> _analyzers = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Returns the analyzer for a language, or <c>null</c> when the grammar is unavailable.
    /// Failure is cached too — no point retrying a missing library for every file.
    /// </summary>
    public SyntaxAnalyzer? Get(LanguageDefinition? definition)
    {
        if (definition is null) return null;

        lock (_gate)
        {
            if (_analyzers.TryGetValue(definition.Id, out var cached)) return cached;

            var analyzer = SyntaxAnalyzer.TryCreate(definition);
            _analyzers[definition.Id] = analyzer;
            return analyzer;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var analyzer in _analyzers.Values) analyzer?.Dispose();
            _analyzers.Clear();
        }
    }
}
