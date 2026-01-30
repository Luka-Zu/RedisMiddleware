using System.Text;
using RedisProxy.Backend.Workers;

namespace TestProject1;

public class RespPacketAnalyzerTests
{
    [Theory]
    [InlineData("*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n", 33)] // Standard SET
    [InlineData("$50\r\n", 50)] // Header declaring 50 bytes
    [InlineData("PING\r\n", 6)] // Simple command, no bulk strings
    public void EstimatePayloadSize_CalculatesCorrectly(string input, int expectedMinSize)
    {
        // Arrange
        byte[] bytes = Encoding.UTF8.GetBytes(input);

        // Act
        int result = RespPacketAnalyzer.EstimateRespPayloadSize(bytes);

        // Assert
        // The result should be AT LEAST the declared size, or the buffer length
        Assert.True(result >= expectedMinSize);
    }

    [Fact]
    public void AnalyzeResponse_Detects_CacheMiss()
    {
        // Arrange: Redis returns "$-1" for a missing key
        string redisResponse = "$-1\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(redisResponse);

        // Act
        var result = RespPacketAnalyzer.AnalyzeResponse(bytes, "GET");

        // Assert
        Assert.True(result.IsSuccess); // It is a valid response (not an error)
        Assert.False(result.IsHit);    // But it is a MISS
    }

    [Fact]
    public void AnalyzeResponse_Detects_RedisError()
    {
        // Arrange: Redis returns "-WRONGTYPE..."
        string redisResponse = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(redisResponse);

        // Act
        var result = RespPacketAnalyzer.AnalyzeResponse(bytes, "GET");

        // Assert
        Assert.False(result.IsSuccess); // It failed
        Assert.True(result.IsHit);      // Hit/Miss is irrelevant on error, default true
    }

    [Fact]
    public void AnalyzeResponse_Detects_CacheHit()
    {
        // Arrange: Valid bulk string response
        string redisResponse = "$5\r\nHello\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(redisResponse);

        // Act
        var result = RespPacketAnalyzer.AnalyzeResponse(bytes, "GET");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.IsHit);
    }
}