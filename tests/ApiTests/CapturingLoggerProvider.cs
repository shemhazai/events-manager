using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EventsManager.ApiTests;

/// <summary>
/// Capte les entrées de journal de l'hôte de test. Sert à vérifier ce qu'une réponse ne dit
/// pas : qu'un 404 d'état normal ne se journalise pas comme une erreur, et qu'un 500 délibéré,
/// lui, le fait — sans quoi l'incident serait invisible, le corps de la réponse n'en disant rien
/// (docs/api-conventions.md, sections 3.6, 4.5 et 4.6).
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyCollection<CapturedLogEntry> Entries
    {
        get { return _entries; }
    }

    /// <summary>
    /// À appeler juste avant la requête sous test : le démarrage de l'hôte journalise lui aussi,
    /// et ces entrées n'ont rien à voir avec ce qui est vérifié.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(categoryName, _entries);
    }

    public void Dispose()
    {
        // Rien à libérer : la file vit aussi longtemps que la fabrique de test.
    }

    private sealed class CapturingLogger(string categoryName, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        private readonly string _categoryName = categoryName;
        private readonly ConcurrentQueue<CapturedLogEntry> _entries = entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            _entries.Enqueue(new CapturedLogEntry(logLevel, _categoryName, formatter(state, exception)));
        }
    }
}

internal sealed record CapturedLogEntry(LogLevel Level, string Category, string Message);
