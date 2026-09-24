namespace DBDiver.Models;

public class GraphLayoutSettings
{
    public double RepulsionStrength { get; set; } = 20000;
    public double SpringStrength { get; set; } = 0.05;
    public double SpringLength { get; set; } = 260;
    public double Damping { get; set; } = 0.85;
    public double MinSeparation { get; set; } = 220;
    public int MaxIterations { get; set; } = 500;
    public double HubSpacing { get; set; } = 900;
    public double SemanticBuffer { get; set; } = 80;
}
