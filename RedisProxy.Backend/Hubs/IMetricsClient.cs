using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Hubs;

public interface IMetricsClient
{
    Task ReceiveServerUpdate(object data);
    Task ReceiveRequestLogUpdate(object data);
    Task ReceiveAdvisories(IEnumerable<Advisory> advisories);
    Task ReceiveKeyspaceUpdate(KeyNode root);
}