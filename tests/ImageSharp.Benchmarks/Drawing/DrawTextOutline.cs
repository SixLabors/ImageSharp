// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Drawing;
using System.Drawing.Drawing2D;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Linq;
using SixLabors.ImageSharp.Processing.Processors.Text;

namespace SixLabors.ImageSharp.Benchmarks
{

    [MemoryDiagnoser]
    public class DrawTextOutline : BenchmarkBase
    {

        [Params(10, 100)]
        public int TextIterations { get; set; }
        public string TextPhrase { get; set; } = "Hello World";
        public string TextToRender => string.Join(" ", Enumerable.Repeat(TextPhrase, TextIterations));


        [Benchmark(Baseline = true, Description = "System.Drawing Draw Text Outline")]
        public void DrawTextSystemDrawing()
        {
            using (Bitmap destination = new Bitmap(800, 800))
            {

                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.Default;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Pen pen = new Pen(System.Drawing.Color.HotPink, 10);
                    var font = new Font("Arial", 12, GraphicsUnit.Point);
                    var gp = new GraphicsPath();
                    gp.AddString(TextToRender, font.FontFamily, (int)font.Style, font.Size, new RectangleF(10, 10, 780, 780), new StringFormat());
                    graphics.DrawPath(pen, gp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Draw Text Outline - Cached Glyphs")]
        public void DrawTextCore()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                var font = SixLabors.Fonts.SystemFonts.CreateFont("Arial", 12);
                image.Mutate(x => x.ApplyProcessor(new DrawTextProcessor<Rgba32>(new TextGraphicsOptions(true) { WrapTextWidth = 780 }, TextToRender, font, null, Processing.Pens.Solid(Rgba32.HotPink, 10), new SixLabors.Primitives.PointF(10, 10))));
            }
        }

        [Benchmark(Description = "ImageSharp Draw Text Outline - Nieve")]
        public void DrawTextCoreOld()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                var font = SixLabors.Fonts.SystemFonts.CreateFont("Arial", 12);
                image.Mutate(x => DrawTextOldVersion(x, new TextGraphicsOptions(true) { WrapTextWidth = 780 }, TextToRender, font, null, Processing.Pens.Solid(Rgba32.HotPink, 10), new SixLabors.Primitives.PointF(10, 10)));
            }

            IImageProcessingContext<TPixel> DrawTextOldVersion<TPixel>(IImageProcessingContext<TPixel> source, TextGraphicsOptions options, string text, SixLabors.Fonts.Font font, IBrush<TPixel> brush, IPen<TPixel> pen, SixLabors.Primitives.PointF location)
                where TPixel : struct, IPixel<TPixel>
            {
                var style = new SixLabors.Fonts.RendererOptions(font, options.DpiX, options.DpiY, location)
                {
                    ApplyKerning = options.ApplyKerning,
                    TabWidth = options.TabWidth,
                    WrappingWidth = options.WrapTextWidth,
                    HorizontalAlignment = options.HorizontalAlignment,
                    VerticalAlignment = options.VerticalAlignment
                };

                Shapes.IPathCollection glyphs = Shapes.TextBuilder.GenerateGlyphs(text, style);

                var pathOptions = (GraphicsOptions)options;
                if (brush != null)
                {
                    source.Fill(pathOptions, brush, glyphs);
                }

                if (pen != null)
                {
                    source.Draw(pathOptions, pen, glyphs);
                }

                return source;
            }
        }
    }
}