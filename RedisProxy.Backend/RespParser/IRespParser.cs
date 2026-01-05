namespace RedisProxy.Backend.RespParser;

public interface IRespParser
{
    (string? Command, string? Key) ParseRequest(byte[] buffer, int bytesRead);
}