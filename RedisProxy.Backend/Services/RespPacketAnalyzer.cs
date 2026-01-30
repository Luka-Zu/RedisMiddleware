using System.Buffers.Text;

namespace RedisProxy.Backend.Services;

public static class RespPacketAnalyzer
{
    public static int EstimateRespPayloadSize(ReadOnlySpan<byte> span)
    {
        int maxDeclaredSize = 0;
        int i = 0;

        while (i < span.Length)
        {
            int dollarIndex = span.Slice(i).IndexOf((byte)'$');
            if (dollarIndex == -1) break;

            i += dollarIndex + 1; // Move past the '$'

            if (Utf8Parser.TryParse(span.Slice(i), out int size, out int bytesConsumed))
            {
                if (size > maxDeclaredSize) maxDeclaredSize = size;
                i += bytesConsumed;
            }
        }
        
        return Math.Max(maxDeclaredSize, span.Length);
    }

    public static (bool IsSuccess, bool IsHit) AnalyzeResponse(ReadOnlySpan<byte> data, string requestCommand)
    {
        if (data.Length == 0) return (true, true);

        bool isSuccess = data[0] != (byte)'-'; // '-' indicates Error
        bool isHit = true;

        if (requestCommand == "GET" && data.Length >= 3)
        {
            if (data[0] == (byte)'$' && data[1] == (byte)'-' && data[2] == (byte)'1')
            {
                isHit = false;
            }
        }

        return (isSuccess, isHit);
    }
}