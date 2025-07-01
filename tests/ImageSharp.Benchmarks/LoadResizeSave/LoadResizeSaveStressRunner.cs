// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImageMagick;
using PhotoSauce.MagicScaler;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Tests;
using SkiaSharp;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpSize = SixLabors.ImageSharp.Size;
using NetVipsImage = NetVips.Image;
using SystemDrawingImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave;

[Flags]
public enum JpegKind
{
    Baseline = 1,
    Progressive = 2,
    Any = Baseline | Progressive
}

public class LoadResizeSaveStressRunner
{
    private const int Quality = 75;

    // Set the quality for ImageSharp
    private readonly JpegEncoder imageSharpJpegEncoder = new() { Quality = Quality };
    private readonly ImageCodecInfo systemDrawingJpegCodec =
        ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

    public string[] Images { get; private set; }

    public double TotalProcessedMegapixels { get; private set; }

    public ImageSharpSize LastProcessedImageSize { get; private set; }

    private string outputDirectory;

    public int ImageCount { get; set; } = int.MaxValue;

    public int MaxDegreeOfParallelism { get; set; } = -1;

    public JpegKind Filter { get; set; } = JpegKind.Any;

    public int ThumbnailSize { get; set; } = 150;

    private static readonly string[] ProgressiveFiles =
    {
        "ancyloscelis-apiformis-m-paraguay-face_2014-08-08-095255-zs-pmax_15046500892_o.jpg",
        "acanthopus-excellens-f-face-brasil_2014-08-06-132105-zs-pmax_14792513890_o.jpg",
        "bee-ceratina-monster-f-ukraine-face_2014-08-09-123342-zs-pmax_15068816101_o.jpg",
        "bombus-eximias-f-tawain-face_2014-08-10-094449-zs-pmax_15155452565_o.jpg",
        "ceratina-14507h1-m-vietnam-face_2014-08-09-163218-zs-pmax_15096718245_o.jpg",
        "ceratina-buscki-f-panama-face_2014-11-25-140413-zs-pmax_15923736081_o.jpg",
        "ceratina-tricolor-f-panama-face2_2014-08-29-160402-zs-pmax_14906318297_o.jpg",
        "ceratina-tricolor-f-panama-face_2014-08-29-160001-zs-pmax_14906300608_o.jpg",
        "ceratina-tricolor-m-panama-face_2014-08-29-162821-zs-pmax_15069878876_o.jpg",
        "coelioxys-cayennensis-f-argentina-face_2014-08-09-171932-zs-pmax_14914109737_o.jpg",
        "ctenocolletes-smaragdinus-f-australia-face_2014-08-08-134825-zs-pmax_14865269708_o.jpg",
        "diphaglossa-gayi-f-face-chile_2014-08-04-180547-zs-pmax_14918891472_o.jpg",
        "hylaeus-nubilosus-f-australia-face_2014-08-14-121100-zs-pmax_15049602149_o.jpg",
        "hypanthidioides-arenaria-f-face-brazil_2014-08-06-061201-zs-pmax_14770371360_o.jpg",
        "megachile-chalicodoma-species-f-morocco-face_2014-08-14-124840-zs-pmax_15217084686_o.jpg",
        "megachile-species-f-15266b06-face-kenya_2014-08-06-161044-zs-pmax_14994381392_o.jpg",
        "megalopta-genalis-m-face-panama-barocolorado_2014-09-19-164939-zs-pmax_15121397069_o.jpg",
        "melitta-haemorrhoidalis-m--england-face_2014-11-02-014026-zs-pmax-recovered_15782113675_o.jpg",
        "nomia-heart-antennae-m-15266b02-face-kenya_2014-08-04-195216-zs-pmax_14922843736_o.jpg",
        "nomia-species-m-oman-face_2014-08-09-192602-zs-pmax_15128732411_o.jpg",
        "nomia-spiney-m-vietnam-face_2014-08-09-213126-zs-pmax_15191389705_o.jpg",
        "ochreriades-fasciata-m-face-israel_2014-08-06-084407-zs-pmax_14965515571_o.jpg",
        "osmia-brevicornisf-jaw-kyrgystan_2014-08-08-103333-zs-pmax_14865267787_o.jpg",
        "pachyanthidium-aff-benguelense-f-6711f07-face_2014-08-07-112830-zs-pmax_15018069042_o.jpg",
        "pachymelus-bicolor-m-face-madagascar_2014-08-06-134930-zs-pmax_14801667477_o.jpg",
        "psaenythia-species-m-argentina-face_2014-08-07-163754-zs-pmax_15007018976_o.jpg",
        "stingless-bee-1-f-face-peru_2014-07-30-123322-zs-pmax_15633797167_o.jpg",
        "triepeolus-simplex-m-face-md-kent-county_2014-07-22-100937-zs-pmax_14805405233_o.jpg",
        "washed-megachile-f-face-chile_2014-08-06-103414-zs-pmax_14977843152_o.jpg",
        "xylocopa-balck-violetwing-f-kyrgystan-angle_2014-08-09-182433-zs-pmax_15123416061_o.jpg",
        "xylocopa-india-yellow-m-india-face_2014-08-10-111701-zs-pmax_15166559172_o.jpg",
    };

    public void Init()
    {
        if (RuntimeInformation.OSArchitecture is Architecture.X86 or Architecture.X64)
        {
            // Workaround ImageMagick issue
            OpenCL.IsEnabled = false;
        }

        string imageDirectory = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, "MemoryStress");
        if (!Directory.Exists(imageDirectory) || !Directory.EnumerateFiles(imageDirectory).Any())
        {
            throw new DirectoryNotFoundException($"Copy stress images to: {imageDirectory}");
        }

        // Get at most this.ImageCount images from there
        bool FilterFunc(string f) => this.Filter.HasFlag(GetJpegType(f));

        this.Images = Directory.EnumerateFiles(imageDirectory).Where(FilterFunc).Take(this.ImageCount).ToArray();

        // Create the output directory next to the images directory
        this.outputDirectory = TestEnvironment.CreateOutputDirectory("MemoryStress");

        static JpegKind GetJpegType(string f) =>
            ProgressiveFiles.Any(p => f.EndsWith(p, StringComparison.OrdinalIgnoreCase))
                ? JpegKind.Progressive
                : JpegKind.Baseline;
    }

    public void ForEachImageParallel(Action<string> action) => Parallel.ForEach(
        this.Images,
        new ParallelOptions { MaxDegreeOfParallelism = this.MaxDegreeOfParallelism },
        action);

    public Task ForEachImageParallelAsync(Func<string, Task> action)
    {
        int maxDegreeOfParallelism = this.MaxDegreeOfParallelism > 0
            ? this.MaxDegreeOfParallelism
            : Environment.ProcessorCount;
        int partitionSize = (int)Math.Ceiling((double)this.Images.Length / maxDegreeOfParallelism);

        List<Task> tasks = [];
        for (int i = 0; i < this.Images.Length; i += partitionSize)
        {
            int end = Math.Min(i + partitionSize, this.Images.Length);
            Task task = RunPartition(i, end);
            tasks.Add(task);
        }

        return Task.WhenAll(tasks);

        Task RunPartition(int start, int end) => Task.Run(async () =>
            {
                for (int i = start; i < end; i++)
                {
                    await action(this.Images[i]);
                }
            });
    }

    private void LogImageProcessed(int width, int height)
    {
        this.LastProcessedImageSize = new ImageSharpSize(width, height);
        double pixels = width * (double)height;
        this.TotalProcessedMegapixels += pixels / 1_000_000.0;
    }

    private string OutputPath(string inputPath, [CallerMemberName] string postfix = null) =>
        Path.Combine(
            this.outputDirectory,
            Path.GetFileNameWithoutExtension(inputPath) + "-" + postfix + Path.GetExtension(inputPath));

    private (int Width, int Height) ScaledSize(int inWidth, int inHeight, int outSize)
    {
        int width, height;
        if (inWidth > inHeight)
        {
            width = outSize;
            height = (int)Math.Round(inHeight * outSize / (double)inWidth);
        }
        else
        {
            width = (int)Math.Round(inWidth * outSize / (double)inHeight);
            height = outSize;
        }

        return (width, height);
    }

    public void SystemDrawingResize(string input)
    {
        using SystemDrawingImage image = SystemDrawingImage.FromFile(input, true);
        this.LogImageProcessed(image.Width, image.Height);

        (int width, int height) = this.ScaledSize(image.Width, image.Height, this.ThumbnailSize);
        Bitmap resized = new(width, height);
        using Graphics graphics = Graphics.FromImage(resized);
        using ImageAttributes attributes = new();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.AssumeLinear;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(image, System.Drawing.Rectangle.FromLTRB(0, 0, resized.Width, resized.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

        // Save the results
        using EncoderParameters encoderParams = new(1);
        using EncoderParameter qualityParam = new(Encoder.Quality, (long)Quality);
        encoderParams.Param[0] = qualityParam;
        resized.Save(this.OutputPath(input), this.systemDrawingJpegCodec, encoderParams);
    }

    public void ImageSharpResize(string input)
    {
        using FileStream inputStream = File.Open(input, FileMode.Open);
        using FileStream outputStream = File.Open(this.OutputPath(input), FileMode.Create);

        // Resize it to fit a 150x150 square
        DecoderOptions options = new()
        {
            TargetSize = new ImageSharpSize(this.ThumbnailSize, this.ThumbnailSize)
        };

        using ImageSharpImage image = JpegDecoder.Instance.Decode(options, inputStream);
        this.LogImageProcessed(image.Width, image.Height);

        // Reduce the size of the file
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;

        // Save the results
        image.Save(outputStream, this.imageSharpJpegEncoder);
    }

    public async Task ImageSharpResizeAsync(string input)
    {
        await using FileStream output = File.Open(this.OutputPath(input), FileMode.Create);

        // Resize it to fit a 150x150 square.
        DecoderOptions options = new()
        {
            TargetSize = new ImageSharpSize(this.ThumbnailSize, this.ThumbnailSize)
        };

        using ImageSharpImage image = await ImageSharpImage.LoadAsync(options, input);
        this.LogImageProcessed(image.Width, image.Height);

        // Reduce the size of the file
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;

        // Save the results
        await image.SaveAsync(output, this.imageSharpJpegEncoder);
    }

    public void MagickResize(string input)
    {
        using MagickImage image = new(input);
        this.LogImageProcessed(image.Width, image.Height);

        // Resize it to fit a 150x150 square
        image.Resize(this.ThumbnailSize, this.ThumbnailSize);

        // Reduce the size of the file
        image.Strip();

        // Set the quality
        image.Quality = Quality;

        // Save the results
        image.Write(this.OutputPath(input));
    }

    public void MagicScalerResize(string input)
    {
        ProcessImageSettings settings = new()
        {
            Width = this.ThumbnailSize,
            Height = this.ThumbnailSize,
            ResizeMode = CropScaleMode.Max,
            EncoderOptions = new JpegEncoderOptions(Quality, ChromaSubsampleMode.Subsample420, true)
        };

        // TODO: Is there a way to capture input dimensions for IncreaseTotalMegapixels?
        using FileStream output = new(this.OutputPath(input), FileMode.Create);
        MagicImageProcessor.ProcessImage(input, output, settings);
    }

    public void SkiaCanvasResize(string input)
    {
        using SKBitmap original = SKBitmap.Decode(input);
        this.LogImageProcessed(original.Width, original.Height);
        (int width, int height) = this.ScaledSize(original.Width, original.Height, this.ThumbnailSize);
        using SKSurface surface = SKSurface.Create(new SKImageInfo(width, height, original.ColorType, original.AlphaType));
        using SKPaint paint = new() { FilterQuality = SKFilterQuality.High };
        SKCanvas canvas = surface.Canvas;
        canvas.Scale((float)width / original.Width);
        canvas.DrawBitmap(original, 0, 0, paint);
        canvas.Flush();

        using FileStream output = File.OpenWrite(this.OutputPath(input));
        surface.Snapshot()
            .Encode(SKEncodedImageFormat.Jpeg, Quality)
            .SaveTo(output);
    }

    public void SkiaBitmapResize(string input)
    {
        using SKBitmap original = SKBitmap.Decode(input);
        this.LogImageProcessed(original.Width, original.Height);
        (int width, int height) = this.ScaledSize(original.Width, original.Height, this.ThumbnailSize);
        using SKBitmap resized = original.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
        if (resized == null)
        {
            return;
        }

        using SKImage image = SKImage.FromBitmap(resized);
        using FileStream output = File.OpenWrite(this.OutputPath(input));
        image.Encode(SKEncodedImageFormat.Jpeg, Quality)
            .SaveTo(output);
    }

    public void SkiaBitmapDecodeToTargetSize(string input)
    {
        using SKCodec codec = SKCodec.Create(input);

        SKImageInfo info = codec.Info;
        this.LogImageProcessed(info.Width, info.Height);
        (int width, int height) = this.ScaledSize(info.Width, info.Height, this.ThumbnailSize);
        SKSizeI supportedScale = codec.GetScaledDimensions((float)width / info.Width);

        using SKBitmap original = SKBitmap.Decode(codec, new SKImageInfo(supportedScale.Width, supportedScale.Height));
        using SKBitmap resized = original.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
        if (resized == null)
        {
            return;
        }

        using SKImage image = SKImage.FromBitmap(resized);

        using FileStream output = File.OpenWrite(this.OutputPath(input, nameof(this.SkiaBitmapDecodeToTargetSize)));
        image.Encode(SKEncodedImageFormat.Jpeg, Quality)
            .SaveTo(output);
    }

    public void NetVipsResize(string input)
    {
        // Thumbnail to fit a 150x150 square
        using NetVipsImage thumb = NetVipsImage.Thumbnail(input, this.ThumbnailSize, this.ThumbnailSize);

        // Save the results
        thumb.Jpegsave(this.OutputPath(input), q: Quality, keep: NetVips.Enums.ForeignKeep.None);
    }
}
