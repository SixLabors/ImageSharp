// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox;

internal class EofReadCountingStream : Stream
{
    private readonly FileStream innerStream;

    public EofReadCountingStream(FileStream innerStream) => this.innerStream = innerStream;

    public int EofHitCount { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => this.innerStream.Length;

    public override long Position
    {
        get => this.innerStream.Position;
        set => this.innerStream.Position = value;
    }

    public override void Flush() => this.innerStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => this.innerStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = this.innerStream.Read(buffer, offset, count);
        this.CheckEof(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = this.innerStream.Read(buffer);
        this.CheckEof(read);
        return read;
    }

    public override int ReadByte()
    {
        int val = base.ReadByte();
        if (val < 0)
        {
            this.EofHitCount++;
        }

        return val;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await this.innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        this.CheckEof(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await this.innerStream.ReadAsync(buffer, cancellationToken);
        this.CheckEof(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => this.innerStream.Seek(offset, origin);

    public override void SetLength(long value) => this.innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => this.innerStream.Write(buffer, offset, count);

    private void CheckEof(int read)
    {
        if (read == 0)
        {
            this.EofHitCount++;
        }
    }
}

internal class ImageStressRunner
{
    private readonly FileInfo originalImageFile;
    private readonly DirectoryInfo stressFolder;
    private readonly byte[] buffer;
    private readonly StreamWriter logger;

    public ImageStressRunner(string originalImageFileName, string stressFolder)
        : this(new FileInfo(originalImageFileName), Directory.CreateDirectory(stressFolder))
    {
    }

    public ImageStressRunner(FileInfo originalImageFile, DirectoryInfo stressFolder)
    {
        this.originalImageFile = originalImageFile;
        this.buffer = new byte[81920];
        this.stressFolder = stressFolder;
        string logFile = Path.Combine(stressFolder.FullName, $"_{originalImageFile.Name}.log");
        this.logger = new StreamWriter(logFile);
        Console.WriteLine($"Logging to {logFile}");
    }

    public async Task RunAsync()
    {
        using FileStream fs = this.originalImageFile.OpenRead();
        ImageSharp.Formats.IImageFormat format = await Image.DetectFormatAsync(fs);
        ImageSharp.Formats.IImageFormatDetector formatDetector = Configuration.Default.ImageFormatsManager.FormatDetectors.FirstOrDefault(fd => fd.GetType().Name.ToLower().Contains(format.Name.ToLower()));
        await Console.Out.WriteLineAsync($"Total: {fs.Length}");
        for (long partLength = formatDetector.HeaderSize; partLength < fs.Length; partLength++)
        {
            await this.RunPart(fs, partLength);
        }
        this.logger.Close();
    }

    private async Task RunPart(FileStream source, long partLength)
    {
        if ((partLength % 50) == 0)
        {
            Console.Write($" {partLength} ");
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
                Console.Write($"[{guardStream.EofHitCount}]!");
            }
            else
            {
                await this.logger.WriteLineAsync($"{fn} timed out!");
                deleteFile = false;
                Console.Write($"[{guardStream.EofHitCount}]T");
            }
        }
        catch (InvalidImageContentException ex)
        {
            Console.Write($"[{guardStream.EofHitCount}]x");
        }
        catch (Exception ex)
        {
            await this.logger.WriteLineAsync($"{fn} failed with {ex.GetType().Name}");
            await this.logger.WriteLineAsync(ex.ToString());
            deleteFile = false;
            Console.Write($"[{guardStream.EofHitCount}]?");
        }
        finally
        {
            if (guardStream.EofHitCount > 2)
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
    public static void Run(string[] args)
    {
        //string fn = GetPath(TestImages.Png.Rgb24BppTrans);
        //string fn = GetPath(TestImages.Png.Ducky);
        //string fn = GetPath(TestImages.Jpeg.Baseline.Bad.BadRST);
        //string fn = GetPath(TestImages.Qoi.TestCard);
        string fn = GetPath(TestImages.Pbm.RgbPlain);
        string stressFolder = TestEnvironment.CreateOutputDirectory("_ImageStress");
        ImageStressRunner stress = new(fn, stressFolder);
        stress.RunAsync().GetAwaiter().GetResult();
    }

    private static string GetPath(string testImage) => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, testImage);
}
