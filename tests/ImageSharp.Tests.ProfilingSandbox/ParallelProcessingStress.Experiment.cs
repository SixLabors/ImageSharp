// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using CommandLine;
using CommandLine.Text;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox;

public partial class ParallelProcessingStress
{
    public static void RunExperiment(string[] args)
    {
        ExperimentOptions options = null;
        using Parser parser = new(settings => settings.CaseInsensitiveEnumValues = true);
        ParserResult<ExperimentOptions> result = parser.ParseArguments<ExperimentOptions>(args).WithParsed(o => options = o);
        if (options == null)
        {
            Console.WriteLine(HelpText.RenderUsageText(result));
            return;
        }

        RunExperiment(options.Method, options.Seconds, options.IterationCount);
    }

    public static void RunExperiment(Method method, int seconds = 5, int times = 5)
    {
        // Warmup
        Console.WriteLine("Warming up...");
        CommandLineOptions warmupOptions = new() { Method = method, Seconds = 1 };
        warmupOptions.Normalize();
        new ParallelProcessingStress(warmupOptions).Run();

        // Outer loop: run inner loop for each parallelism level
        List<(int Parallelism, double AvgMpxPerSecPerCpu)> results = new();

        foreach (int parallelism in ParallelismLevels())
        {
            Console.WriteLine($"\nRunning {method} with ProcessorParallelism={parallelism} ({times}x {seconds}s)...");

            double totalMpxPerSecPerCpu = 0;
            for (int i = 0; i < times; i++)
            {
                CommandLineOptions options = new() { Method = method, ProcessorParallelism = parallelism, Seconds = seconds };
                options.Normalize();
                Stats stats = new ParallelProcessingStress(options).Run();
                totalMpxPerSecPerCpu += stats.MegapixelsPerSecPerCpu;
            }

            results.Add((parallelism, totalMpxPerSecPerCpu / times));
        }

        // Print results as markdown table
        Console.WriteLine();
        Console.WriteLine("| ProcessorParallelism | MegapixelsPerSecPerCpu |");
        Console.WriteLine("|---------------------:|-----------------------:|");
        foreach ((int parallelism, double avg) in results)
        {
            Console.WriteLine($"| {parallelism,20} | {avg,22:f3} |");
        }
    }

    private sealed class ExperimentOptions
    {
        [Option('m', "method", Required = false, Default = Method.Edges, HelpText = "The stress test method to run (Edges, Crop)")]
        public Method Method { get; set; } = Method.Edges;

        [Option('s', "seconds", Required = false, Default = 5, HelpText = "Duration of each run in seconds")]
        public int Seconds { get; set; } = 5;

        [Option('i', "iterations", Required = false, Default = 5, HelpText = "Number of runs per parallelism level")]
        public int IterationCount { get; set; } = 5;
    }

    private static IEnumerable<int> ParallelismLevels()
    {
        int cpuCount = Environment.ProcessorCount;
        for (int p = 1; p <= cpuCount; p *= 2)
        {
            yield return p;
        }

        // When cpuCount is not a power of two, append it as the final step
        if ((cpuCount & (cpuCount - 1)) != 0)
        {
            yield return cpuCount;
        }
    }
}
