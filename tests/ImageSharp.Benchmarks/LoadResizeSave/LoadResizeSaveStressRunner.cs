// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImageMagick;
using PhotoSauce.MagicScaler;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;
using SkiaSharp;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpSize = SixLabors.ImageSharp.Size;
using NetVipsImage = NetVips.Image;
using SystemDrawingImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave
{
    public enum JpegKind
    {
        Baseline = 1,
        Progressive = 2,
        Any = Baseline | Progressive
    }

    public class LoadResizeSaveStressRunner
    {
        private const int Quality = 75;
        private const string ImageSharp = nameof(ImageSharp);
        private const string SystemDrawing = nameof(SystemDrawing);
        private const string MagickNET = nameof(MagickNET);
        private const string NetVips = nameof(NetVips);
        private const string MagicScaler = nameof(MagicScaler);
        private const string SkiaSharpCanvas = nameof(SkiaSharpCanvas);
        private const string SkiaSharpBitmap = nameof(SkiaSharpBitmap);

        // Set the quality for ImagSharp
        private readonly JpegEncoder imageSharpJpegEncoder = new () { Quality = Quality };
        private readonly ImageCodecInfo systemDrawingJpegCodec =
            ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

        public string[] Images { get; private set; }

        public double TotalProcessedMegapixels { get; private set; }

        private string outputDirectory;

        public int ImageCount { get; set; } = int.MaxValue;

        public int MaxDegreeOfParallelism { get; set; } = -1;

        public JpegKind Filter { get; set; }

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

        private void IncreaseTotalMegapixels(int width, int height)
        {
            double pixels = width * (double)height;
            this.TotalProcessedMegapixels += pixels / 1_000_000.0;
        }

        private string OutputPath(string inputPath, string postfix) =>
            Path.Combine(
                this.outputDirectory,
                Path.GetFileNameWithoutExtension(inputPath) + "-" + postfix + Path.GetExtension(inputPath));

        private (int width, int height) ScaledSize(int inWidth, int inHeight, int outSize)
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
            using var image = SystemDrawingImage.FromFile(input, true);
            this.IncreaseTotalMegapixels(image.Width, image.Height);

            (int width, int height) scaled = this.ScaledSize(image.Width, image.Height, ThumbnailSize);
            var resized = new Bitmap(scaled.width, scaled.height);
            using var graphics = Graphics.FromImage(resized);
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.AssumeLinear;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, System.Drawing.Rectangle.FromLTRB(0, 0, resized.Width, resized.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

            // Save the results
            using var encoderParams = new EncoderParameters(1);
            using var qualityParam = new EncoderParameter(Encoder.Quality, (long)Quality);
            encoderParams.Param[0] = qualityParam;
            resized.Save(this.OutputPath(input, SystemDrawing), this.systemDrawingJpegCodec, encoderParams);
        }

        public void ImageSharpResize(string input)
        {
            using FileStream output = File.Open(this.OutputPath(input, ImageSharp), FileMode.Create);

            // Resize it to fit a 150x150 square
            using var image = ImageSharpImage.Load(input);
            this.IncreaseTotalMegapixels(image.Width, image.Height);

            image.Mutate(i => i.Resize(new ResizeOptions
            {
                Size = new ImageSharpSize(ThumbnailSize, ThumbnailSize),
                Mode = ResizeMode.Max
            }));

            // Reduce the size of the file
            image.Metadata.ExifProfile = null;

            // Save the results
            image.Save(output, this.imageSharpJpegEncoder);
        }

        public void MagickResize(string input)
        {
            using var image = new MagickImage(input);
            this.IncreaseTotalMegapixels(image.Width, image.Height);

            // Resize it to fit a 150x150 square
            image.Resize(ThumbnailSize, ThumbnailSize);

            // Reduce the size of the file
            image.Strip();

            // Set the quality
            image.Quality = Quality;

            // Save the results
            image.Write(this.OutputPath(input, MagickNET));
        }

        public void MagicScalerResize(string input)
        {
            var settings = new ProcessImageSettings()
            {
                Width = ThumbnailSize,
                Height = ThumbnailSize,
                ResizeMode = CropScaleMode.Max,
                SaveFormat = FileFormat.Jpeg,
                JpegQuality = Quality,
                JpegSubsampleMode = ChromaSubsampleMode.Subsample420
            };

            // TODO: Is there a way to capture input dimensions for IncreaseTotalMegapixels?
            using var output = new FileStream(this.OutputPath(input, MagicScaler), FileMode.Create);
            MagicImageProcessor.ProcessImage(input, output, settings);
        }

        public void SkiaCanvasResize(string input)
        {
            using var original = SKBitmap.Decode(input);
            this.IncreaseTotalMegapixels(original.Width, original.Height);
            (int width, int height) scaled = this.ScaledSize(original.Width, original.Height, ThumbnailSize);
            using var surface = SKSurface.Create(new SKImageInfo(scaled.width, scaled.height, original.ColorType, original.AlphaType));
            using var paint = new SKPaint() { FilterQuality = SKFilterQuality.High };
            SKCanvas canvas = surface.Canvas;
            canvas.Scale((float)scaled.width / original.Width);
            canvas.DrawBitmap(original, 0, 0, paint);
            canvas.Flush();

            using FileStream output = File.OpenWrite(this.OutputPath(input, SkiaSharpCanvas));
            surface.Snapshot()
                .Encode(SKEncodedImageFormat.Jpeg, Quality)
                .SaveTo(output);
        }

        public void SkiaBitmapResize(string input)
        {
            using var original = SKBitmap.Decode(input);
            this.IncreaseTotalMegapixels(original.Width, original.Height);
            (int width, int height) scaled = this.ScaledSize(original.Width, original.Height, ThumbnailSize);
            using var resized = original.Resize(new SKImageInfo(scaled.width, scaled.height), SKFilterQuality.High);
            if (resized == null)
            {
                return;
            }

            using var image = SKImage.FromBitmap(resized);
            using FileStream output = File.OpenWrite(this.OutputPath(input, SkiaSharpBitmap));
            image.Encode(SKEncodedImageFormat.Jpeg, Quality)
                .SaveTo(output);
        }

        public void NetVipsResize(string input)
        {
            // Thumbnail to fit a 150x150 square
            using var thumb = NetVipsImage.Thumbnail(input, ThumbnailSize, ThumbnailSize);

            // Save the results
            thumb.Jpegsave(this.OutputPath(input, NetVips), q: Quality, strip: true);
        }
    }
}
