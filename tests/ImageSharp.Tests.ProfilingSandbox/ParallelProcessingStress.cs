// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommandLine;
using CommandLine.Text;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox;

public sealed partial class ParallelProcessingStress
{
    private CommandLineOptions options;
    private Configuration configuration;
    private ulong totalKiloPixels;

    public static void Run(string[] args)
    {
        CommandLineOptions options = null;
        if (args.Length > 0)
        {
            options = CommandLineOptions.Parse(args);
            if (options == null)
            {
                return;
            }
        }

        options ??= new CommandLineOptions();
        ParallelProcessingStress stress = new(options.Normalize());
        stress.Run();
    }

    private ParallelProcessingStress(CommandLineOptions options)
    {
        this.options = options;
        this.configuration = Configuration.Default.Clone();
        this.configuration.MaxDegreeOfParallelism = options.ProcessorParallelism > 0
            ? options.ProcessorParallelism
            : Environment.ProcessorCount;
    }

    private Stats Run()
    {
        ParallelOptions systemOptions = new() { MaxDegreeOfParallelism = this.options.SystemParallelism };
        Func<int> action = this.options.Method switch
        {
            Method.Crop => this.Crop,
            _ => this.DetectEdges,
        };
        Console.WriteLine($"Running {this.options.Method} for {this.options.Seconds} seconds ...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan runFor = TimeSpan.FromSeconds(this.options.Seconds);
        Parallel.ForEach(InfiniteSequence(), systemOptions, (_, state) =>
        {
            ulong kiloPixels = (ulong)action() / 1000;
            Interlocked.Add(ref this.totalKiloPixels, kiloPixels);

            if (stopwatch.Elapsed >= runFor)
            {
                state.Stop();
            }
        });
        stopwatch.Stop();

        double totalMegaPixels = this.totalKiloPixels / 1000.0;
        Stats stats = new(stopwatch.ElapsedMilliseconds, totalMegaPixels, systemOptions.MaxDegreeOfParallelism);
        Console.WriteLine(stats.GetMarkdown());
        return stats;
    }

    private static IEnumerable<long> InfiniteSequence()
    {
        long i = 0;
        while (true)
        {
            yield return i++;
        }
    }

    private int DetectEdges()
    {
        using Image<Rgba32> image = new(this.options.Width, this.options.Height);
        image.Mutate(this.configuration, x => x.DetectEdges());
        return image.Width * image.Height;
    }

    private int Crop()
    {
        using Image<Rgba32> image = new(this.options.Width, this.options.Height);
        Rectangle bounds = image.Bounds;
        bounds = new Rectangle(1, 1, bounds.Width - 2, bounds.Height - 2);
        image.Clone(this.configuration, x => x.Crop(bounds)).Dispose();
        return image.Width * image.Height;
    }

    private sealed record Stats
    {
        public double TotalSeconds { get; }

        public double TotalMegapixels { get; }

        public double MegapixelsPerSec { get; }

        public double MegapixelsPerSecPerCpu { get; }

        public Stats(long elapsedMilliseconds, double totalMegapixels, int cpuCount)
        {
            this.TotalMegapixels = totalMegapixels;
            this.TotalSeconds = elapsedMilliseconds / 1000.0;
            this.MegapixelsPerSec = totalMegapixels / this.TotalSeconds;
            this.MegapixelsPerSecPerCpu = this.MegapixelsPerSec / cpuCount;
        }

        public string GetMarkdown()
        {
            StringBuilder bld = new();
            bld.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {nameof(this.TotalSeconds)} | {nameof(this.MegapixelsPerSec)} | {nameof(this.MegapixelsPerSecPerCpu)} |");
            bld.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {L(nameof(this.TotalSeconds))} | {L(nameof(this.MegapixelsPerSec))} | {L(nameof(this.MegapixelsPerSecPerCpu))} |");

            bld.Append("| ");
            bld.AppendFormat(CultureInfo.InvariantCulture, F(nameof(this.TotalSeconds)), this.TotalSeconds);
            bld.Append(" | ");
            bld.AppendFormat(CultureInfo.InvariantCulture, F(nameof(this.MegapixelsPerSec)), this.MegapixelsPerSec);
            bld.Append(" | ");
            bld.AppendFormat(CultureInfo.InvariantCulture, F(nameof(this.MegapixelsPerSecPerCpu)), this.MegapixelsPerSecPerCpu);
            bld.AppendLine(" |");

            return bld.ToString();

            static string L(string header) => new('-', header.Length);
            static string F(string column) => $"{{0,{column.Length}:f3}}";
        }
    }

    public enum Method
    {
        Edges,
        Crop
    }

    private sealed class CommandLineOptions
    {
        [Option('m', "method", Required = false, Default = Method.Edges, HelpText = "The stress test method to run (Edges, Crop)")]
        public Method Method { get; set; } = Method.Edges;

        [Option('p', "processor-parallelism", Required = false, Default = -1, HelpText = "Level of parallelism for the image processor")]
        public int ProcessorParallelism { get; set; } = -1;

        [Option('t', "system-parallelism", Required = false, Default = -1, HelpText = "Level of parallelism for the outer loop")]
        public int SystemParallelism { get; set; } = -1;

        [Option('w', "width", Required = false, Default = 4000, HelpText = "Width of the test image")]
        public int Width { get; set; } = 4000;

        [Option('h', "height", Required = false, Default = 4000, HelpText = "Height of the test image")]
        public int Height { get; set; } = 4000;

        [Option('s', "seconds", Required = false, Default = 5, HelpText = "Duration of the stress test in seconds")]
        public int Seconds { get; set; } = 5;

        public override string ToString() => string.Join(
            Environment.NewLine,
            $"method: {this.Method}",
            $"processor-parallelism: {this.ProcessorParallelism}",
            $"system-parallelism: {this.SystemParallelism}",
            $"width: {this.Width}",
            $"height: {this.Height}",
            $"seconds: {this.Seconds}");

        public CommandLineOptions Normalize()
        {
            if (this.ProcessorParallelism < 0)
            {
                this.ProcessorParallelism = Environment.ProcessorCount;
            }

            if (this.SystemParallelism < 0)
            {
                this.SystemParallelism = Environment.ProcessorCount;
            }

            return this;
        }

        public static CommandLineOptions Parse(string[] args)
        {
            CommandLineOptions result = null;
            using Parser parser = new(settings => settings.CaseInsensitiveEnumValues = true);
            ParserResult<CommandLineOptions> parserResult = parser.ParseArguments<CommandLineOptions>(args).WithParsed(o =>
            {
                result = o;
            });

            if (result == null)
            {
                Console.WriteLine(HelpText.RenderUsageText(parserResult));
            }

            return result;
        }
    }
}
