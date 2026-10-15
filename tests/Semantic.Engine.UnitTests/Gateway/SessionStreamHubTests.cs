using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Semantic.Engine.Gateway.Endpoints;
using Semantic.Engine.Gateway.Streaming;
using Xunit;

namespace Semantic.Engine.UnitTests.Gateway;

public sealed class SessionStreamHubTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Publish_BeforeAttach_BuffersEvents()
    {
        SessionStreamHub hub = new(_time);
        Guid sessionId = Guid.NewGuid();
        hub.Open(sessionId);

        hub.Publish(sessionId, new SessionStreamEvent { Name = SessionStreamEvent.TokenEvent });
        hub.TryAttach(sessionId, out SessionStreamHub.StreamLease? lease);

        using (lease)
        {
            SessionStreamEvent received = await lease!.Reader.ReadAsync();
            received.Name
                .Should()
                .Be(SessionStreamEvent.TokenEvent, "because events produced before the client connects must not be lost");
        }
    }

    [Fact]
    public void Publish_UnknownSession_IsDropped()
    {
        SessionStreamHub hub = new(_time);

        hub.Publish(Guid.NewGuid(), new SessionStreamEvent { Name = "token" });

        hub.Count
            .Should()
            .Be(0, "because events of sessions that this Gateway does not serve must not allocate streams");
    }

    [Fact]
    public void TryAttach_SecondReader_IsRejectedUntilLeaseDisposed()
    {
        SessionStreamHub hub = new(_time);
        Guid sessionId = Guid.NewGuid();
        hub.Open(sessionId);

        hub.TryAttach(sessionId, out SessionStreamHub.StreamLease? first);
        bool secondWhileHeld = hub.TryAttach(sessionId, out _);
        first!.Dispose();
        bool secondAfterRelease = hub.TryAttach(sessionId, out SessionStreamHub.StreamLease? second);
        second?.Dispose();

        secondWhileHeld
            .Should()
            .BeFalse("because one session stream accepts a single reader");
        secondAfterRelease
            .Should()
            .BeTrue("because the stream can be reclaimed after the first reader disconnected");
    }

    [Fact]
    public void RemoveExpired_UnclaimedStreamOlderThanTtl_IsRemoved()
    {
        SessionStreamHub hub = new(_time);
        Guid sessionId = Guid.NewGuid();
        hub.Open(sessionId);

        _time.Advance(TimeSpan.FromMinutes(11));
        int removed = hub.RemoveExpired(TimeSpan.FromMinutes(10));

        removed
            .Should()
            .Be(1, "because buffered events of an abandoned session must not accumulate forever");
    }

    [Fact]
    public void RemoveExpired_ConnectedStream_IsKept()
    {
        SessionStreamHub hub = new(_time);
        Guid sessionId = Guid.NewGuid();
        hub.Open(sessionId);
        hub.TryAttach(sessionId, out SessionStreamHub.StreamLease? lease);

        _time.Advance(TimeSpan.FromHours(1));
        int removed = hub.RemoveExpired(TimeSpan.FromMinutes(10));
        lease!.Dispose();

        removed
            .Should()
            .Be(0, "because a stream with a connected client is in use");
    }

    [Fact]
    public void FormatEvent_ProducesSseWireFormat()
    {
        string wire = AgentEndpoints.FormatEvent(new SessionStreamEvent { Name = "token", DataJson = """{"text":"a\nb"}""" });

        wire
            .Should()
            .Be("event: token\ndata: {\"text\":\"a\\nb\"}\n\n", "because SSE events are terminated by a blank line and data must stay on one line");
    }
}
