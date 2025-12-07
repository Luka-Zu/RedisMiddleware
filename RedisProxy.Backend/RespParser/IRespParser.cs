namespace RedisProxy.Backend.RespParser;

public interface IRespParser
{
    // The method signature remains the same, just inside an interface
    (string? Command, string? Key) ParseRequest(byte[] buffer, int bytesRead);
}