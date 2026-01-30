using System.Text;
using RedisProxy.Backend.Workers;

namespace TestProject1;

public class RespPacketAnalyzerTests
{
    [Theory]
    [InlineData("*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n", 33)]
    [InlineData("$50\r\n", 50)]
    [InlineData("PING\r\n", 6)]
    public void EstimatePayloadSize_CalculatesCorrectly(string input, int expectedMinSize)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);

        int result = RespPacketAnalyzer.EstimateRespPayloadSize(bytes);

        Assert.True(result >= expectedMinSize);
    }

    [Fact]
    public void AnalyzeResponse_Detects_CacheMiss()
    {
        string redisResponse = "$-1\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(redisResponse);

        var result = RespPacketAnalyzer.AnalyzeResponse(bytes, "GET");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsHit);
    }

    [Fact]
    public void AnalyzeResponse_Detects_RedisError()
    {
        string redisResponse = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(redisResponse);

        var result = RespPacketAnalyzer.AnalyzeResponse(bytes, "GET");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsHit);
    }

    [Fact]
    public void AnalyzeResponse_Detects_CacheHit()
    {
        string redisResponse = "$5\r\nHello\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(redisResponse);

        var result = RespPacketAnalyzer.AnalyzeResponse(bytes, "GET");

        Assert.True(result.IsSuccess);
        Assert.True(result.IsHit);
    }
}