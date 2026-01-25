namespace RedisProxy.Backend.DTOs;

public class ReplayRequestDTO
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string TargetHost { get; set; } = "localhost";
    public int TargetPort { get; set; } = 6379;
    public double Speed { get; set; } = 1.0;    
}
