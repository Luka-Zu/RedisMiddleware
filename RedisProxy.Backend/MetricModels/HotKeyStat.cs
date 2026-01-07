namespace RedisProxy.Backend.MetricModels;

public class HotKeyStat
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}