// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommandLine;
using CommandLine.Text;
using SixLabors.ImageSharp.Benchmarks.LoadResizeSave;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox;

// See ImageSharp.Benchmarks/LoadResizeSave/README.md
internal class LoadResizeSaveParallelMemoryStress
{
    private LoadResizeSaveParallelMemoryStress()
    {
        this.Benchmarks = new LoadResizeSaveStressRunner
        {
            Filter = JpegKind.Baseline,
        };
        this.Benchmarks.Init();
    }

    private int gcFrequency;

    private int leakFrequency;

    private int imageCounter;

    public LoadResizeSaveStressRunner Benchmarks { get; }

    public static void Run(string[] args)
    {
        Console.WriteLine($"Running: {typeof(LoadResizeSaveParallelMemoryStress).Assembly.Location}");
        Console.WriteLine($"64 bit: {Environment.Is64BitProcess}");
        CommandLineOptions options = args.Length > 0 ? CommandLineOptions.Parse(args) : null;

        LoadResizeSaveParallelMemoryStress lrs = new();
        if (options != null)
        {
            lrs.Benchmarks.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
        }

        Console.WriteLine($"\nEnvironment.ProcessorCount={Environment.ProcessorCount}");
        Stopwatch timer;

        if (options == null || !(options.ImageSharp || options.AsyncImageSharp))
        {
            RunBenchmarkSwitcher(lrs, out timer);
        }
        else
        {
            Console.WriteLine("Running ImageSharp with options:");
            Console.WriteLine(options.ToString());

            if (!options.KeepDefaultAllocator)
            {
                Configuration.Default.MemoryAllocator = options.CreateMemoryAllocator();
            }

            lrs.leakFrequency = options.LeakFrequency;
            lrs.gcFrequency = options.GcFrequency;

            timer = Stopwatch.StartNew();
            try
            {
                for (int i = 0; i < options.RepeatCount; i++)
                {
                    if (options.AsyncImageSharp)
                    {
                        lrs.ImageSharpBenchmarkParallelAsync();
                    }
                    else
                    {
                        lrs.ImageSharpBenchmarkParallel();
                    }

                    Console.WriteLine("OK");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            timer.Stop();

            if (options.ReleaseRetainedResourcesAtEnd)
            {
                Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            }

            int finalGcCount = -Math.Min(0, options.GcFrequency);

            if (finalGcCount > 0)
            {
                Console.WriteLine($"TotalOutstandingHandles: {UnmanagedMemoryHandle.TotalOutstandingHandles}");
                Console.WriteLine($"GC x {finalGcCount}, with 3 seconds wait.");
                for (int i = 0; i < finalGcCount; i++)
                {
                    Thread.Sleep(3000);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        Stats stats = new(timer, lrs.Benchmarks.TotalProcessedMegapixels);
        Console.WriteLine($"Total Megapixels: {stats.TotalMegapixels}, TotalOomRetries: {UnmanagedMemoryHandle.TotalOomRetries}, TotalOutstandingHandles: {UnmanagedMemoryHandle.TotalOutstandingHandles}, Total Gen2 GC count: {GC.CollectionCount(2)}");
        Console.WriteLine(stats.GetMarkdown());
        if (options?.FileOutput != null)
        {
            PrintFileOutput(options.FileOutput, stats);
        }

        if (options != null && options.PauseAtEnd)
        {
            Console.WriteLine("Press ENTER");
            Console.ReadLine();
        }
    }

    private static void PrintFileOutput(string fileOutput, Stats stats)
    {
        string[] ss = fileOutput.Split(';');
        string fileName = ss[0];
        string content = ss[1]
            .Replace("TotalSeconds", stats.TotalSeconds.ToString(CultureInfo.InvariantCulture))
            .Replace("EOL", Environment.NewLine);
        File.AppendAllText(fileName, content);
    }

    private static void RunBenchmarkSwitcher(LoadResizeSaveParallelMemoryStress lrs, out Stopwatch timer)
    {
        Console.WriteLine(@"Choose a library for image resizing stress test:

1. System.Drawing
2. ImageSharp
3. MagicScaler
4. SkiaSharp
5. SkiaSharp - Decode to target size
6. NetVips
7. ImageMagick
");

        ConsoleKey key = Console.ReadKey().Key;
        if (key < ConsoleKey.D1 || key > ConsoleKey.D6)
        {
            Console.WriteLine("Unrecognized command.");
            Environment.Exit(-1);
        }

        timer = Stopwatch.StartNew();

        switch (key)
        {
            case ConsoleKey.D1:
                lrs.SystemDrawingBenchmarkParallel();
                break;
            case ConsoleKey.D2:
                lrs.ImageSharpBenchmarkParallel();
                break;
            case ConsoleKey.D3:
                lrs.MagicScalerBenchmarkParallel();
                break;
            case ConsoleKey.D4:
                lrs.SkiaBitmapBenchmarkParallel();
                break;
            case ConsoleKey.D5:
                lrs.SkiaBitmapDecodeToTargetSizeBenchmarkParallel();
                break;
            case ConsoleKey.D6:
                lrs.NetVipsBenchmarkParallel();
                break;
            case ConsoleKey.D7:
                lrs.MagickBenchmarkParallel();
                break;
        }

        timer.Stop();
    }

    private struct Stats
    {
        public double TotalSeconds { get; }

        public double TotalMegapixels { get; }

        public double MegapixelsPerSec { get; }

        public double MegapixelsPerSecPerCpu { get; }

        public Stats(Stopwatch sw, double totalMegapixels)
        {
            this.TotalMegapixels = totalMegapixels;
            this.TotalSeconds = sw.ElapsedMilliseconds / 1000.0;
            this.MegapixelsPerSec = totalMegapixels / this.TotalSeconds;
            this.MegapixelsPerSecPerCpu = this.MegapixelsPerSec / Environment.ProcessorCount;
        }

        public string GetMarkdown()
        {
            StringBuilder bld = new();
            bld.AppendLine($"| {nameof(this.TotalSeconds)} | {nameof(this.MegapixelsPerSec)} | {nameof(this.MegapixelsPerSecPerCpu)} |");
            bld.AppendLine(
                $"| {L(nameof(this.TotalSeconds))} | {L(nameof(this.MegapixelsPerSec))} | {L(nameof(this.MegapixelsPerSecPerCpu))} |");

            bld.Append("| ");
            bld.AppendFormat(F(nameof(this.TotalSeconds)), this.TotalSeconds);
            bld.Append(" | ");
            bld.AppendFormat(F(nameof(this.MegapixelsPerSec)), this.MegapixelsPerSec);
            bld.Append(" | ");
            bld.AppendFormat(F(nameof(this.MegapixelsPerSecPerCpu)), this.MegapixelsPerSecPerCpu);
            bld.AppendLine(" |");

            return bld.ToString();

            static string L(string header) => new('-', header.Length);
            static string F(string column) => $"{{0,{column.Length}:f3}}";
        }
    }

    private class CommandLineOptions
    {
        [Option('a', "async-imagesharp", Required = false, Default = false, HelpText = "Async ImageSharp without benchmark switching")]
        public bool AsyncImageSharp { get; set; }

        [Option('i', "imagesharp", Required = false, Default = false, HelpText = "Test ImageSharp without benchmark switching")]
        public bool ImageSharp { get; set; }

        [Option('d', "default-allocator", Required = false, Default = false, HelpText = "Keep default MemoryAllocator and ignore all settings")]
        public bool KeepDefaultAllocator { get; set; }

        [Option('m', "max-contiguous", Required = false, Default = 4, HelpText = "Maximum size of contiguous pool buffers in MegaBytes")]
        public int MaxContiguousPoolBufferMegaBytes { get; set; } = 4;

        [Option('s', "poolsize", Required = false, Default = 4096, HelpText = "The size of the pool in MegaBytes")]
        public int MaxPoolSizeMegaBytes { get; set; } = 4096;

        [Option('u', "max-nonpool", Required = false, Default = 32, HelpText = "Maximum size of non-pooled contiguous blocks in MegaBytes")]
        public int MaxCapacityOfNonPoolBuffersMegaBytes { get; set; } = 32;

        [Option('p', "parallelism", Required = false, Default = -1, HelpText = "Level of parallelism")]
        public int MaxDegreeOfParallelism { get; set; } = -1;

        [Option('r', "repeat-count", Required = false, Default = 1, HelpText = "Times to run the whole benchmark")]
        public int RepeatCount { get; set; } = 1;

        [Option('g', "gc-frequency", Required = false, Default = 0, HelpText = "Positive number: call GC every 'g'-th resize. Negative number: call GC '-g' times in the end.")]
        public int GcFrequency { get; set; }

        [Option('e', "release-at-end", Required = false, Default = false, HelpText = "Specify to run ReleaseRetainedResources() after execution")]
        public bool ReleaseRetainedResourcesAtEnd { get; set; }

        [Option('w', "pause", Required = false, Default = false, HelpText = "Specify to pause and wait for user input after the execution")]
        public bool PauseAtEnd { get; set; }

        [Option('f', "file", Required = false, Default = null, HelpText = "Specify to print the execution time to a file. Format: '<file>;<formatstr>' see the code for formatstr semantics.")]
        public string FileOutput { get; set; }

        [Option('t', "trim-period", Required = false, Default = null, HelpText = "Trim period for the pool in seconds")]
        public int? TrimTimeSeconds { get; set; }

        [Option('l', "leak-frequency", Required = false, Default = 0, HelpText = "Inject leaking memory allocations after every 'l'-th resize to stress test finalizer behavior.")]
        public int LeakFrequency { get; set; }

        public static CommandLineOptions Parse(string[] args)
        {
            CommandLineOptions result = null;
            ParserResult<CommandLineOptions> parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(o =>
            {
                result = o;
            });

            if (result == null)
            {
                Console.WriteLine(HelpText.RenderUsageText(parserResult));
            }

            return result;
        }

        public override string ToString() =>
            $"p({this.MaxDegreeOfParallelism})_i({this.ImageSharp})_a({this.AsyncImageSharp})_d({this.KeepDefaultAllocator})_m({this.MaxContiguousPoolBufferMegaBytes})_s({this.MaxPoolSizeMegaBytes})_u({this.MaxCapacityOfNonPoolBuffersMegaBytes})_r({this.RepeatCount})_g({this.GcFrequency})_e({this.ReleaseRetainedResourcesAtEnd})_l({this.LeakFrequency})";

        public MemoryAllocator CreateMemoryAllocator()
        {
            if (this.TrimTimeSeconds.HasValue)
            {
                return new UniformUnmanagedMemoryPoolMemoryAllocator(
                    1024 * 1024,
                    (int)B(this.MaxContiguousPoolBufferMegaBytes),
                    B(this.MaxPoolSizeMegaBytes),
                    (int)B(this.MaxCapacityOfNonPoolBuffersMegaBytes),
                    new UniformUnmanagedMemoryPool.TrimSettings
                    {
                        TrimPeriodMilliseconds = this.TrimTimeSeconds.Value * 1000
                    });
            }
            else
            {
                return new UniformUnmanagedMemoryPoolMemoryAllocator(
                    1024 * 1024,
                    (int)B(this.MaxContiguousPoolBufferMegaBytes),
                    B(this.MaxPoolSizeMegaBytes),
                    (int)B(this.MaxCapacityOfNonPoolBuffersMegaBytes));
            }
        }

        private static long B(double megaBytes) => (long)(megaBytes * 1024 * 1024);
    }

    private void ForEachImage(Action<string> action) => this.Benchmarks.ForEachImageParallel(action);

    private void SystemDrawingBenchmarkParallel() => this.ForEachImage(this.Benchmarks.SystemDrawingResize);

    private void ImageSharpBenchmarkParallel() =>
        this.ForEachImage(f =>
        {
            int cnt = Interlocked.Increment(ref this.imageCounter);
            this.Benchmarks.ImageSharpResize(f);
            if (this.leakFrequency > 0 && cnt % this.leakFrequency == 0)
            {
                _ = Configuration.Default.MemoryAllocator.Allocate<byte>(1 << 16);
                Size size = this.Benchmarks.LastProcessedImageSize;
                _ = new Image<ImageSharp.PixelFormats.Rgba64>(size.Width, size.Height);
            }

            if (this.gcFrequency > 0 && cnt % this.gcFrequency == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        });

    private void ImageSharpBenchmarkParallelAsync() =>
        this.Benchmarks.ForEachImageParallelAsync(f => this.Benchmarks.ImageSharpResizeAsync(f))
            .GetAwaiter()
            .GetResult();

    private void MagickBenchmarkParallel() => this.ForEachImage(this.Benchmarks.MagickResize);

    private void MagicScalerBenchmarkParallel() => this.ForEachImage(this.Benchmarks.MagicScalerResize);

    private void SkiaBitmapBenchmarkParallel() => this.ForEachImage(this.Benchmarks.SkiaBitmapResize);

    private void SkiaBitmapDecodeToTargetSizeBenchmarkParallel() => this.ForEachImage(this.Benchmarks.SkiaBitmapDecodeToTargetSize);

    private void NetVipsBenchmarkParallel() => this.ForEachImage(this.Benchmarks.NetVipsResize);
}
