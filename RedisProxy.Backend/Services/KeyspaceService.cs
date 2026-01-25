using System.Collections.Concurrent;
using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Services;

public interface IKeyspaceService
{
    void AnalyzeKey(string key);
    KeyNode GetSnapshot();
}

public class KeyspaceService : IKeyspaceService
{
    private readonly ConcurrentDictionary<string, NodeData> _root = new();

    private class NodeData
    {
        public long Count;
        public ConcurrentDictionary<string, NodeData> Children = new();
    }

    public void AnalyzeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        var parts = key.Split(new[] { ':', '/', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var currentLevel = _root;

        foreach (var part in parts)
        {
            string segment = NormalizeSegment(part);
            
            var node = currentLevel.GetOrAdd(segment, _ => new NodeData());
            
            Interlocked.Increment(ref node.Count);
            
            currentLevel = node.Children;
        }
    }

    private string NormalizeSegment(string segment)
    {
        if (long.TryParse(segment, out _) || Guid.TryParse(segment, out _))
        {
            return "{id}";
        }
        if (segment.Length > 20) 
        {
            return "{token}";
        }
        return segment;
    }

    public KeyNode GetSnapshot()
    {
        return new KeyNode
        {
            Name = "root",
            Children = ConvertToNodes(_root),
            Value = _root.Values.Sum(x => x.Count)
        };
    }

    private List<KeyNode> ConvertToNodes(ConcurrentDictionary<string, NodeData> source)
    {
        var list = new List<KeyNode>();
        foreach (var kvp in source)
        {
            list.Add(new KeyNode
            {
                Name = kvp.Key,
                Value = kvp.Value.Count, // Get current count
                Children = ConvertToNodes(kvp.Value.Children)
            });
        }
        return list.OrderByDescending(n => n.Value).ToList();
    }
}