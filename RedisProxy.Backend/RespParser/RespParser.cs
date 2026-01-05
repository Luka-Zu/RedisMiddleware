using System.Text;

namespace RedisProxy.Backend.RespParser;

public class RespParser : IRespParser
{
    
    public (string? Command, string? Key) ParseRequest(byte[] buffer, int bytesRead)
    {
        try
        {
            if (bytesRead < 3 || buffer[0] != (byte)'*') 
                return (null, null);

            string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var lines = text.Split("\r\n");


            if (lines.Length < 3) 
                return (null, null);

            string command = lines[2].ToUpper();

            string? key = null;
            if (lines.Length >= 5 && lines[0].StartsWith("*") && int.Parse(lines[0].Substring(1)) >= 2)
            {
                key = lines[4];
            }

            return (command, key);
        }
        catch
        {
            return (null, null);
        }
    }
}