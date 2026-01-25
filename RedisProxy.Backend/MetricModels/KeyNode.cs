namespace RedisProxy.Backend.MetricModels;

public class KeyNode
{
    public string Name { get; set; } = "root";
    public long Value { get; set; } // Hit count
    public List<KeyNode> Children { get; set; } = new();

    public KeyNode? FindChild(string name) 
        => Children.FirstOrDefault(c => c.Name == name);
}