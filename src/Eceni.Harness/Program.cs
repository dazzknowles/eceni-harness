namespace Eceni.Harness;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        return HarnessCli.Run(arguments, Console.Out, Console.Error);
    }
}
