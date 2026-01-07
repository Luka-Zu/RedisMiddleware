namespace RedisProxy.Backend.MetricModels;

public class CommandStat
{
    public string Command { get; set; } = string.Empty;
    public int Count { get; set; }
}