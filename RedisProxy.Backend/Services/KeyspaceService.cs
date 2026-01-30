using RedisProxy.Backend.Data;
using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Services;

public interface IKeyspaceService
{
    Task<KeyNode> GetKeyspaceSnapshotAsync(DateTime since);
    
    KeyNode BuildTree(IEnumerable<string> keys); 
}

public class KeyspaceService(DatabaseService db) : IKeyspaceService
{
    public async Task<KeyNode> GetKeyspaceSnapshotAsync(DateTime since)
    {
        var keys = await db.GetKeysSinceAsync(since);

        return BuildTree(keys);
    }

    public KeyNode BuildTree(IEnumerable<string> keys)
    {
        var root = new NodeData();

        foreach (var key in keys)
        {
            var parts = key.Split(new[] { ':', '/', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var currentLevel = root;

            foreach (var part in parts)
            {
                string segment = NormalizeSegment(part);

                if (!currentLevel.Children.TryGetValue(segment, out var node))
                {
                    node = new NodeData { Segment = segment };
                    currentLevel.Children[segment] = node;
                }
                
                node.Count++;
                currentLevel = node;
            }
        }

        return new KeyNode
        {
            Name = "root",
            Value = root.Children.Values.Sum(x => x.Count),
            Children = ConvertToNodes(root.Children)
        };
    }

    private class NodeData
    {
        public string Segment = "";
        public long Count;
        public Dictionary<string, NodeData> Children = new();
    }

    private string NormalizeSegment(string segment)
    {
        if (long.TryParse(segment, out _) || Guid.TryParse(segment, out _)) return "{id}";
        if (segment.Length > 20) return "{token}";
        return segment;
    }

    private List<KeyNode> ConvertToNodes(Dictionary<string, NodeData> source)
    {
        return source.Values
            .Select(n => new KeyNode
            {
                Name = n.Segment,
                Value = n.Count,
                Children = ConvertToNodes(n.Children)
            })
            .OrderByDescending(n => n.Value)
            .ToList();
    }
}