using System.Text;

namespace RedisProxy.Backend.RespParser;

public class RespParser : IRespParser
{
    
    // Returns a tuple: (Command, Key)
    // Example: ("SET", "thesis")
    public (string? Command, string? Key) ParseRequest(byte[] buffer, int bytesRead)
    {
        try
        {
            // 1. Basic Validation: Redis arrays start with '*'
            if (bytesRead < 3 || buffer[0] != (byte)'*') 
                return (null, null);

            string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var lines = text.Split("\r\n");

            // lines[0] = "*3" (Array Length)
            // lines[1] = "$3" (Length of Command)
            // lines[2] = "SET" (The Command)
            // lines[3] = "$6" (Length of Key)
            // lines[4] = "thesis" (The Key)

            if (lines.Length < 3) 
                return (null, null);

            // Extract Command (Always the first item in the array)
            string command = lines[2].ToUpper();

            // Extract Key (Usually the second item, if it exists)
            string? key = null;
            if (lines.Length >= 5 && lines[0].StartsWith("*") && int.Parse(lines[0].Substring(1)) >= 2)
            {
                key = lines[4];
            }

            return (command, key);
        }
        catch
        {
            // If parsing fails (e.g. partial packet), just ignore it. 
            // We don't want to crash the proxy.
            return (null, null);
        }
    }
}