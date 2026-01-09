namespace dx_dispatch_challenge;

internal class Program
{
    private int _tickMinutes = 30;
    private int _horizonMinutes = 1440;

    // Choose a deterministic "AtRisk" threshold (minutes)
    private const int _atRiskThresholdMinutes = 60;


    public static void Main(string[] args)
    {
        Console.ReadKey();
    }
}