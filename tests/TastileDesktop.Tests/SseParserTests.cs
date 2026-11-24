using System.Text;
using TastileDesktop.Models;

namespace TastileDesktop.Tests;

public sealed class SseParserTests
{
    private static async Task<List<SseEvent>> ParseAsync(string payload)
    {
        var events = new List<SseEvent>();
        await foreach (var sse in SseParser.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(payload))))
        {
            events.Add(sse);
        }
        return events;
    }

    [Fact]
    public async Task ReadAsync_ParsesChangeEventWithIdAndData()
    {
        var events = await ParseAsync(
            "event: change\n" +
            "id: cursor-123\n" +
            "data: {\"entityKind\":0,\"entityId\":\"t1\",\"changeKind\":0}\n" +
            "\n");

        var sse = Assert.Single(events);
        Assert.Equal("change", sse.Event);
        Assert.Equal("cursor-123", sse.Id);
        Assert.Contains("\"entityKind\":0", sse.Data);
        Assert.Null(sse.RetryMs);
    }

    [Fact]
    public async Task ReadAsync_SkipsHeartbeatComments()
    {
        var events = await ParseAsync(": ping\n\nretry: 5000\n\n");

        var sse = Assert.Single(events);
        Assert.Equal(string.Empty, sse.Event);
        Assert.Equal(5000, sse.RetryMs);
    }

    [Fact]
    public async Task ReadAsync_JoinsMultilineData()
    {
        var events = await ParseAsync("data: line1\ndata: line2\n\n");

        var sse = Assert.Single(events);
        Assert.Equal("line1\nline2", sse.Data);
    }

    [Fact]
    public async Task ReadAsync_ParsesTerminalEvents()
    {
        var events = await ParseAsync(
            "event: cursor-expired\ndata: {}\n\n" +
            "event: overflow\ndata: {}\n\n" +
            "event: rejected\ndata: {}\n\n");

        Assert.Equal(["cursor-expired", "overflow", "rejected"], events.Select(e => e.Event).ToArray());
    }

    [Fact]
    public async Task ReadAsync_HandlesCrlfAndTrailingFrameWithoutBlankLine()
    {
        var events = await ParseAsync("event: change\r\nid: c1\r\ndata: {}\r\n");

        var sse = Assert.Single(events);
        Assert.Equal("change", sse.Event);
        Assert.Equal("c1", sse.Id);
    }

    [Fact]
    public async Task ReadAsync_IgnoresFieldlessBlocks()
    {
        var events = await ParseAsync("\n\n\nevent: change\ndata: {}\n\n");

        var sse = Assert.Single(events);
        Assert.Equal("change", sse.Event);
    }
}
