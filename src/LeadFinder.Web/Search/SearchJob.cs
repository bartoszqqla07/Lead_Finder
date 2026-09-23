using System.Threading.Channels;
using LeadFinder.Models;
using LeadFinder.Web.Contracts;

namespace LeadFinder.Web.Search;

/// <summary>
/// Trwające (lub zakończone) wyszukiwanie w pamięci: zbiera zdarzenia postępu i rozsyła je
/// do subskrybentów SSE. Nowy subskrybent (np. po odświeżeniu strony) dostaje najpierw całą
/// dotychczasową historię, a potem zdarzenia na żywo.
/// </summary>
public sealed class SearchJob : IProgress<SearchProgress>
{
    private readonly object _gate = new();
    private readonly List<SearchEventDto> _history = [];
    private readonly List<Channel<SearchEventDto>> _subscribers = [];

    public SearchJob(int searchRunId) => SearchRunId = searchRunId;

    public int SearchRunId { get; }

    public CancellationTokenSource Cancellation { get; } = new();

    public bool IsFinished
    {
        get { lock (_gate) return _isFinished; }
    }

    private bool _isFinished;

    /// <summary>Wywoływane przez pipeline z Core przy każdym kroku.</summary>
    public void Report(SearchProgress value) => Publish(new SearchEventDto(
        SearchEventTypes.Progress, value.Stage, value.Message, value.Current, value.Total, DateTime.UtcNow));

    /// <summary>Publikuje zdarzenie końcowe i zamyka wszystkie strumienie.</summary>
    public void Finish(string type, string message, SearchRunDto run) =>
        Publish(new SearchEventDto(type, null, message, 0, 0, DateTime.UtcNow, run), isFinal: true);

    /// <summary>
    /// Subskrypcja: historia + kanał zdarzeń na żywo (null, gdy wyszukiwanie już się zakończyło).
    /// Zwolnienie subskrypcji wypisuje subskrybenta.
    /// </summary>
    public Subscription Subscribe()
    {
        lock (_gate)
        {
            var history = _history.ToList();
            if (_isFinished)
                return new Subscription(history, null, () => { });

            var channel = Channel.CreateUnbounded<SearchEventDto>(new UnboundedChannelOptions { SingleReader = true });
            _subscribers.Add(channel);
            return new Subscription(history, channel.Reader, () =>
            {
                lock (_gate) _subscribers.Remove(channel);
            });
        }
    }

    private void Publish(SearchEventDto @event, bool isFinal = false)
    {
        lock (_gate)
        {
            if (_isFinished)
                return;

            _history.Add(@event);
            foreach (var subscriber in _subscribers)
                subscriber.Writer.TryWrite(@event);

            if (!isFinal)
                return;

            _isFinished = true;
            foreach (var subscriber in _subscribers)
                subscriber.Writer.TryComplete();
            _subscribers.Clear();
        }
    }

    public sealed class Subscription(
        IReadOnlyList<SearchEventDto> history, ChannelReader<SearchEventDto>? live, Action unsubscribe) : IDisposable
    {
        public IReadOnlyList<SearchEventDto> History { get; } = history;
        public ChannelReader<SearchEventDto>? Live { get; } = live;
        public void Dispose() => unsubscribe();
    }
}
