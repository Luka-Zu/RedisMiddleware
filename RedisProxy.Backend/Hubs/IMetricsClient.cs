namespace RedisProxy.Backend.Hubs;

public interface IMetricsClient
{
    Task ReceiveServerUpdate(object data);
    Task ReceiveRequestLogUpdate(object data);
}