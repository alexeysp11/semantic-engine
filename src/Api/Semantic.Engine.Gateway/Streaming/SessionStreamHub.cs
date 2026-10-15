using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Semantic.Engine.Gateway.Streaming;

/// <summary>
/// In-memory pub/sub that carries bus events of a session to the HTTP connection of its client.
/// </summary>
/// <remarks>
/// A stream is created before the first command of a session is published, so events produced before the client
/// connects are buffered instead of lost. A stream accepts a single reader at a time.
/// </remarks>
public sealed class SessionStreamHub(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, SessionStream> _streams = new();

    /// <summary>
    /// Creates the stream of a session when it does not exist yet.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    public void Open(Guid sessionId)
    {
        _streams.GetOrAdd(sessionId, _ => new SessionStream(timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Delivers an event to the stream of a session. Events of unknown sessions are dropped.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="streamEvent">The event to deliver.</param>
    public void Publish(Guid sessionId, SessionStreamEvent streamEvent)
    {
        if (_streams.TryGetValue(sessionId, out SessionStream? stream))
        {
            stream.LastActivity = timeProvider.GetUtcNow();
            stream.Channel.Writer.TryWrite(streamEvent);
        }
    }

    /// <summary>
    /// Attaches a reader to the stream of a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="lease">The reader lease when the method returns <see langword="true"/>; dispose it to release the stream.</param>
    /// <returns><see langword="false"/> when the session is unknown or already has a connected reader.</returns>
    public bool TryAttach(Guid sessionId, out StreamLease? lease)
    {
        lease = null;
        if (!_streams.TryGetValue(sessionId, out SessionStream? stream))
        {
            return false;
        }

        if (Interlocked.CompareExchange(ref stream.Connected, 1, 0) != 0)
        {
            return false;
        }

        lease = new StreamLease(stream, timeProvider);
        return true;
    }

    /// <summary>
    /// Removes streams that no client claimed or reclaimed within the time-to-live.
    /// </summary>
    /// <param name="timeToLive">The maximum age of an unattached stream.</param>
    /// <returns>The number of removed streams.</returns>
    public int RemoveExpired(TimeSpan timeToLive)
    {
        DateTimeOffset threshold = timeProvider.GetUtcNow() - timeToLive;
        int removed = 0;
        foreach (KeyValuePair<Guid, SessionStream> entry in _streams)
        {
            bool expired = Volatile.Read(ref entry.Value.Connected) == 0 && entry.Value.LastActivity < threshold;
            if (expired && _streams.TryRemove(entry.Key, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Gets the number of tracked streams.</summary>
    public int Count => _streams.Count;

    internal sealed class SessionStream(DateTimeOffset createdAt)
    {
        public Channel<SessionStreamEvent> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<SessionStreamEvent>(
            new UnboundedChannelOptions { SingleReader = true });

        public DateTimeOffset LastActivity { get; set; } = createdAt;

        public int Connected;
    }

    /// <summary>
    /// Grants exclusive read access to a session stream until it is disposed.
    /// </summary>
    public sealed class StreamLease : IDisposable
    {
        private readonly SessionStream _stream;
        private readonly TimeProvider _timeProvider;

        internal StreamLease(SessionStream stream, TimeProvider timeProvider)
        {
            _stream = stream;
            _timeProvider = timeProvider;
        }

        /// <summary>Gets the reader of the stream events.</summary>
        public ChannelReader<SessionStreamEvent> Reader => _stream.Channel.Reader;

        /// <inheritdoc />
        public void Dispose()
        {
            _stream.LastActivity = _timeProvider.GetUtcNow();
            Volatile.Write(ref _stream.Connected, 0);
        }
    }
}
