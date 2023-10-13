// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox;

internal class ImageStressRunner
{
    private const int EofHitCountThreshold = 4;

    private readonly FileInfo originalImageFile;
    private readonly DirectoryInfo stressFolder;
    private readonly byte[] buffer;
    private readonly StreamWriter logger;
    private readonly string logFile;

    public ImageStressRunner(string originalImageFileName, string stressFolder)
        : this(new FileInfo(originalImageFileName), Directory.CreateDirectory(stressFolder))
    {
    }

    public ImageStressRunner(FileInfo originalImageFile, DirectoryInfo stressFolder)
    {
        this.originalImageFile = originalImageFile;
        this.buffer = new byte[81920];
        this.stressFolder = stressFolder;
        this.logFile = Path.Combine(stressFolder.FullName, $"_{originalImageFile.Name}.log");
        this.logger = new StreamWriter(this.logFile);
        this.Print($"Logging to {this.logFile}");
    }

    public int StartPosition { get; set; }

    public async Task RunAsync()
    {
        using FileStream fs = this.originalImageFile.OpenRead();
        ImageSharp.Formats.IImageFormat format = await Image.DetectFormatAsync(fs);
        ImageSharp.Formats.IImageFormatDetector formatDetector = Configuration.Default.ImageFormatsManager.FormatDetectors.FirstOrDefault(fd => fd.GetType().Name.ToLower().Contains(format.Name.ToLower()));
        this.Print($"Total: {fs.Length}");
        int start = Math.Max(this.StartPosition, formatDetector.HeaderSize);
        for (long partLength = start; partLength < fs.Length; partLength++)
        {
            await this.RunPart(fs, partLength);
        }

        bool empty = this.logger.BaseStream.Length == 0;
        this.logger.Close();
        if (empty)
        {
            File.Delete(this.logFile);
        }
    }

    private void Print(string message)
    {
        //Console.WriteLine(message);
    }

    private async Task RunPart(FileStream source, long partLength)
    {
        if ((partLength % 100) == 0)
        {
            this.Print($" -{partLength}- ");
        }

        source.Seek(0, SeekOrigin.Begin);

        string fn = Path.Combine(
            this.stressFolder.FullName,
            $"{Path.GetFileNameWithoutExtension(this.originalImageFile.Name)}-{partLength:D10}{Path.GetExtension(this.originalImageFile.Name)}");
        using FileStream destination = File.Create(fn);
        await this.Copy(source, destination, partLength);

        destination.Seek(0, SeekOrigin.Begin);

        using BufferedReadStream guardStream = new(Configuration.Default, destination);
        bool deleteFile = true;
        try
        {
            Task loadTask = Task.Factory.StartNew(() => Image.Load(guardStream).Dispose(), TaskCreationOptions.LongRunning);
            Task timeoutTask = Task.Delay(10_000);
            Task completed = await Task.WhenAny(loadTask, timeoutTask);
            if (completed == loadTask)
            {
                await loadTask;
                this.Print($"[{guardStream.EofHitCount}]!");
            }
            else
            {
                await this.logger.WriteLineAsync($"{fn} timed out!");
                deleteFile = false;
                this.Print($"[{guardStream.EofHitCount}]T");
            }
        }
        catch (Exception ex) when (ex is ImageFormatException or NotSupportedException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
        {
            this.Print($"[{guardStream.EofHitCount}]x");
        }
        catch (Exception ex)
        {
            await this.logger.WriteLineAsync($"{fn} failed with {ex.GetType().Name}");
            await this.logger.WriteLineAsync(ex.ToString());
            deleteFile = false;
            this.Print($"[{guardStream.EofHitCount}]?");
        }
        finally
        {
            if (guardStream.EofHitCount > EofHitCountThreshold)
            {
                deleteFile = false;
                await this.logger.WriteLineAsync($"{fn} hit EOF {guardStream.EofHitCount} times!");
            }

            guardStream.Close();
            destination.Close();
            if (deleteFile)
            {
                File.Delete(fn);
            }
        }

        await this.logger.FlushAsync();
    }

    private async ValueTask Copy(FileStream source, FileStream destination, long partLength)
    {
        int bytesRead;
        for (long pos = 0; pos < partLength; pos += bytesRead)
        {
            int toRead = (int)Math.Min(this.buffer.Length, partLength - pos);
            bytesRead = await source.ReadAsync(this.buffer.AsMemory(0, toRead)).ConfigureAwait(false);
            Debug.Assert(bytesRead > 0, "bug");
            await destination.WriteAsync(new ReadOnlyMemory<byte>(this.buffer, 0, bytesRead)).ConfigureAwait(false);
        }
    }
}

internal class ImageStress
{
    public static async Task RunAsync()
    {
        //string fn = GetPath(TestImages.Png.Rgb24BppTrans);
        //string fn = GetPath(TestImages.Png.Ducky);
        //string fn = GetPath(TestImages.Jpeg.Baseline.Bad.BadRST);
        //string fn = GetPath(TestImages.Qoi.TestCard);
        //string fn = GetPath(TestImages.Pbm.RgbPlain);
        //ImageStressRunner stress = new(fn, stressFolder)
        //{
        //    StartPosition = 15_000
        //};
        //await stress.RunAsync();

        string[] allDone = File.ReadAllLines(@"c:\_dev\sl\ImageSharp\tests\Images\ActualOutput\_ImageStress.logs\__AllDone.txt");
        string[] extensions = Configuration.Default.ImageFormats.Where(f => f.Name is not "TIFF" and not "PBM").SelectMany(f => f.FileExtensions).Select(x => $".{x.ToLower()}").ToArray();

        DirectoryInfo rootDir = new(TestEnvironment.InputImagesDirectoryFullPath);
        FileInfo[] images = rootDir.EnumerateFiles("*.*", new EnumerationOptions()
        {
            RecurseSubdirectories = true
        })
            .Where(f => extensions.Any(e => e.Equals(f.Extension, StringComparison.OrdinalIgnoreCase)))
            .Where(f => !allDone.Contains(f.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        string stressFolder = TestEnvironment.CreateOutputDirectory("_ImageStress2");
        await Parallel.ForEachAsync(images, async (imgFile, _) =>
        {
            await Console.Out.WriteLineAsync($"{imgFile.Name}...");
            ImageStressRunner stress = new(imgFile, new DirectoryInfo(stressFolder));
            await stress.RunAsync();
            await Console.Out.WriteLineAsync($"{imgFile.Name} DONE.");
        });
        await Console.Out.WriteLineAsync("--- ALL DONE ---");
        Console.ReadLine();
    }

    private static string GetPath(string testImage) => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, testImage);
}
