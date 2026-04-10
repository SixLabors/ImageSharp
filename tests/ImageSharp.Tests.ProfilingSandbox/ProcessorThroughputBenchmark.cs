// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using CommandLine;
using CommandLine.Text;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox;

public sealed class ProcessorThroughputBenchmark
{
    private readonly CommandLineOptions options;
    private readonly Configuration configuration;
    private ulong totalPixelsInUnit;

    private ProcessorThroughputBenchmark(CommandLineOptions options)
    {
        this.options = options;
        this.configuration = Configuration.Default.Clone();
        this.configuration.MaxDegreeOfParallelism = options.ProcessorParallelism > 0
            ? options.ProcessorParallelism
            : Environment.ProcessorCount;
    }

    public static Task RunAsync(string[] args)
    {
        CommandLineOptions options = null;
        if (args.Length > 0)
        {
            options = CommandLineOptions.Parse(args);
            if (options == null)
            {
                return Task.CompletedTask;
            }
        }

        options ??= new CommandLineOptions();
        return new ProcessorThroughputBenchmark(options.Normalize())
            .RunAsync();
    }

    private async Task RunAsync()
    {
        SemaphoreSlim semaphore = new(this.options.ConcurrentRequests);
        Console.WriteLine(this.options.Method);
        Func<int> action = this.options.Method switch
        {
            Method.Crop => this.Crop,
            Method.Edges => this.DetectEdges,
            Method.DrawImage => this.DrawImage,
            Method.BinaryThreshold => this.BinaryThreshold,
            Method.Histogram => this.Histogram,
            Method.OilPaint => this.OilPaint,
            _ => throw new NotImplementedException(),
        };

        Console.WriteLine(this.options);
        Console.WriteLine($"Running {this.options.Method} for {this.options.Seconds} seconds ...");
        TimeSpan runFor = TimeSpan.FromSeconds(this.options.Seconds);

        // inFlight starts at 1 to represent the dispatch loop itself
        int inFlight = 1;
        TaskCompletionSource drainTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < runFor && !drainTcs.Task.IsCompleted)
        {
            await semaphore.WaitAsync();

            if (stopwatch.Elapsed >= runFor)
            {
                semaphore.Release();
                break;
            }

            Interlocked.Increment(ref inFlight);

            _ = ProcessImage();

            async Task ProcessImage()
            {
                try
                {
                    if (stopwatch.Elapsed >= runFor || drainTcs.Task.IsCompleted)
                    {
                        return;
                    }

                    await Task.Yield(); // "emulate IO", i.e., make sure the processing code is async
                    ulong pixels = (ulong)action();
                    Interlocked.Add(ref this.totalPixelsInUnit, pixels);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    drainTcs.TrySetException(ex);
                }
                finally
                {
                    semaphore.Release();
                    if (Interlocked.Decrement(ref inFlight) == 0)
                    {
                        drainTcs.TrySetResult();
                    }
                }
            }
        }

        // Release the dispatch loop's own count; if no work is in flight, this completes immediately
        if (Interlocked.Decrement(ref inFlight) == 0)
        {
            drainTcs.TrySetResult();
        }

        await drainTcs.Task;
        stopwatch.Stop();

        double totalMegaPixels = this.totalPixelsInUnit / 1_000_000.0;
        double totalSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
        double megapixelsPerSec = totalMegaPixels / totalSeconds;
        Console.WriteLine($"TotalSeconds: {totalSeconds:F2}");
        Console.WriteLine($"MegaPixelsPerSec: {megapixelsPerSec:F2}");
    }

    private int OilPaint()
    {
        using Image<Rgba32> image = new(this.options.Width, this.options.Height);
        image.Mutate(this.configuration, x => x.OilPaint());
        return image.Width * image.Height;
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

    private int DrawImage()
    {
        using Image<Rgba32> image = new(this.options.Width, this.options.Height);
        using Image<Rgba32> foreground = new(this.options.Width, this.options.Height);
        image.Mutate(c => c.DrawImage(foreground, 0.5f));
        return image.Width * image.Height;
    }

    private int BinaryThreshold()
    {
        using Image<Rgba32> image = new(this.options.Width, this.options.Height);
        image.Mutate(c => c.BinaryThreshold(0.5f));
        return image.Width * image.Height;
    }

    private int Histogram()
    {
        using Image<Rgba32> image = new(this.options.Width, this.options.Height);
        image.Mutate(c => c.HistogramEqualization());
        return image.Width * image.Height;
    }

    private enum Method
    {
        Edges,
        Crop,
        DrawImage,
        BinaryThreshold,
        Histogram,
        OilPaint
    }

    private sealed class CommandLineOptions
    {
        [Option('m', "method", Required = false, Default = Method.Edges, HelpText = "The stress test method to run (Edges, Crop)")]
        public Method Method { get; set; } = Method.Edges;

        [Option('p', "processor-parallelism", Required = false, Default = -1, HelpText = "Level of parallelism for the image processor")]
        public int ProcessorParallelism { get; set; } = -1;

        [Option('c', "concurrent-requests", Required = false, Default = -1, HelpText = "Number of concurrent in-flight requests")]
        public int ConcurrentRequests { get; set; } = -1;

        [Option('w', "width", Required = false, Default = 4000, HelpText = "Width of the test image")]
        public int Width { get; set; } = 4000;

        [Option('h', "height", Required = false, Default = 4000, HelpText = "Height of the test image")]
        public int Height { get; set; } = 4000;

        [Option('s', "seconds", Required = false, Default = 5, HelpText = "Duration of the stress test in seconds")]
        public int Seconds { get; set; } = 5;

        public override string ToString() => string.Join(
            "|",
            $"method: {this.Method}",
            $"processor-parallelism: {this.ProcessorParallelism}",
            $"concurrent-requests: {this.ConcurrentRequests}",
            $"width: {this.Width}",
            $"height: {this.Height}",
            $"seconds: {this.Seconds}");

        public CommandLineOptions Normalize()
        {
            if (this.ProcessorParallelism < 0)
            {
                this.ProcessorParallelism = Environment.ProcessorCount;
            }

            if (this.ConcurrentRequests < 0)
            {
                this.ConcurrentRequests = Environment.ProcessorCount;
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
