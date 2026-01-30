using RedisProxy.Backend.MetricModels;
using RedisProxy.Backend.Services;

namespace TestProject1;

public class AdvisoryServiceTests
{
    private readonly AdvisoryService _service = new();

    [Theory]
    [InlineData("KEYS")]
    [InlineData("FLUSHALL")]
    [InlineData("SAVE")]
    public void Analyze_Detects_BlockingCommands(string command)
    {
        var log = new RequestLog { Command = command, Key = "*" };

        var results = _service.AnalyzeLog(log, "");

        Assert.Contains(results, r => r.Level == "Critical" && r.Message.Contains("blocks the single Redis thread"));
    }

    [Fact]
    public void Analyze_Detects_NetworkHog()
    {
        var log = new RequestLog { Command = "SET", PayloadSize = 50001 };

        var results = _service.AnalyzeLog(log, "SET key value");

        Assert.Contains(results, r => r.Level == "Warning" && r.Message.Contains("Large payload detected"));
    }

    [Fact]
    public void Analyze_Detects_LatencySpike()
    {
        var log = new RequestLog { Command = "GET", LatencyMs = 51.0 };

        var results = _service.AnalyzeLog(log, "GET key");

        Assert.Contains(results, r => r.Level == "Warning" && r.Message.Contains("Slow query detected"));
    }

    [Fact]
    public void Analyze_Detects_Select_Database_AntiPattern()
    {
        var log = new RequestLog { Command = "SELECT", Key = "1" };

        var results = _service.AnalyzeLog(log, "SELECT 1");

        Assert.Contains(results, r => r.Level == "Warning" && r.Message.Contains("Using multiple logical databases"));
    }
    
    [Fact]
    public void Analyze_Ignores_Select_Zero()
    {
        var log = new RequestLog { Command = "SELECT", Key = "0" };

        var results = _service.AnalyzeLog(log, "SELECT 0");

        Assert.Empty(results);
    }

    [Fact]
    public void Analyze_Detects_Missing_TTL()
    {
        var log = new RequestLog { Command = "SET", Key = "user:1" };
        string rawContent = "*3\r\n$3\r\nSET\r\n$6\r\nuser:1\r\n$5\r\nvalue\r\n";

        var results = _service.AnalyzeLog(log, rawContent);

        Assert.Contains(results, r => r.Level == "Warning" && r.Message.Contains("Memory Risk"));
    }

    [Fact]
    public void Analyze_Accepts_SET_With_TTL()
    {
        var log = new RequestLog { Command = "SET", Key = "user:1" };
        string rawContent = "*5\r\n$3\r\nSET\r\n$6\r\nuser:1\r\n$5\r\nvalue\r\n$2\r\nEX\r\n$2\r\n60\r\n";

        var results = _service.AnalyzeLog(log, rawContent);

        Assert.DoesNotContain(results, r => r.Message.Contains("Memory Risk"));
    }

    [Fact]
    public void Analyze_Detects_Json_Blob()
    {
        var log = new RequestLog { Command = "SET" };
        string rawContent = "SET user:1 {\"name\":\"john\"}";

        var results = _service.AnalyzeLog(log, rawContent);

        Assert.Contains(results, r => r.Level == "Info" && r.Command == "JSON");
    }
}