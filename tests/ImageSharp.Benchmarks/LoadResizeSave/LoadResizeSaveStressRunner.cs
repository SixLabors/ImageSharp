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
    public class LoadResizeSaveStressRunner
    {
        private const int ThumbnailSize = 150;
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
            this.Images = Directory.EnumerateFiles(imageDirectory).Take(this.ImageCount).ToArray();

            // Create the output directory next to the images directory
            this.outputDirectory = TestEnvironment.CreateOutputDirectory("MemoryStress");
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
