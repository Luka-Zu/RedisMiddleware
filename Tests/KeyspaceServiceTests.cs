using RedisProxy.Backend.Services;

namespace TestProject1;

public class KeyspaceServiceTests
{
    private readonly KeyspaceService _service;

    public KeyspaceServiceTests()
    {
        _service = new KeyspaceService(null!);
    }

    [Fact]
    public void BuildTree_Returns_Empty_Root_When_No_Keys()
    {
        var keys = new List<string>();

        var result = _service.BuildTree(keys);

        Assert.Equal("root", result.Name);
        Assert.Equal(0, result.Value);
        Assert.Empty(result.Children);
    }

    [Fact]
    public void BuildTree_Aggregates_Simple_Hierarchy()
    {
        var keys = new List<string>
        {
            "user:profile",
            "user:settings"
        };

        var result = _service.BuildTree(keys);

        Assert.Single(result.Children);
        var userNode = result.Children[0];
        Assert.Equal("user", userNode.Name);
        Assert.Equal(2, userNode.Value);

        Assert.Equal(2, userNode.Children.Count);
        Assert.Contains(userNode.Children, c => c.Name == "profile");
        Assert.Contains(userNode.Children, c => c.Name == "settings");
    }

    [Fact]
    public void BuildTree_Normalizes_Numeric_Ids()
    {
        var keys = new List<string>
        {
            "order:100",
            "order:101",
            "order:102"
        };

        var result = _service.BuildTree(keys);

        var orderNode = result.Children.First(c => c.Name == "order");
        
        Assert.Single(orderNode.Children);
        var idNode = orderNode.Children[0];
        
        Assert.Equal("{id}", idNode.Name);
        Assert.Equal(3, idNode.Value);
    }

    [Fact]
    public void BuildTree_Normalizes_Guids()
    {
        var keys = new List<string>
        {
            "session:550e8400-e29b-41d4-a716-446655440000",
            "session:550e8400-e29b-41d4-a716-446655440001"
        };

        var result = _service.BuildTree(keys);

        var sessionNode = result.Children.First();
        var idNode = sessionNode.Children.First();

        Assert.Equal("{id}", idNode.Name);
        Assert.Equal(2, idNode.Value);
    }

    [Fact]
    public void BuildTree_Normalizes_Long_Tokens()
    {
        var longToken = new string('x', 25);
        var keys = new List<string>
        {
            $"auth:{longToken}"
        };

        var result = _service.BuildTree(keys);

        var authNode = result.Children.First();
        var tokenNode = authNode.Children.First();

        Assert.Equal("{token}", tokenNode.Name);
    }

    [Fact]
    public void BuildTree_Handles_Multiple_Delimiters()
    {
        var keys = new List<string>
        {
            "group:a",
            "group/b",
            "group_c"
        };

        var result = _service.BuildTree(keys);

        var groupNode = result.Children.First(c => c.Name == "group");
        Assert.Equal(3, groupNode.Value);
        Assert.Equal(3, groupNode.Children.Count);
    }

    [Fact]
    public void BuildTree_Calculates_Recursive_Counts()
    {
        var keys = new List<string>
        {
            "a:b:c",
            "a:b:d"
        };

        var result = _service.BuildTree(keys);

        var nodeA = result.Children.First(n => n.Name == "a");
        var nodeB = nodeA.Children.First(n => n.Name == "b");

        Assert.Equal(2, nodeA.Value);
        Assert.Equal(2, nodeB.Value);
        Assert.Equal(2, nodeB.Children.Count); 
    }
}