// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
    public class DrawText : BenchmarkBase
    {
        [Params(10, 100)]
        public int TextIterations { get; set; }
        public string TextPhrase { get; set; } = "Hello World";
        public string TextToRender => string.Join(" ", Enumerable.Repeat(this.TextPhrase, this.TextIterations));


        [Benchmark(Baseline = true, Description = "System.Drawing Draw Text")]
        public void DrawTextSystemDrawing()
        {
            using (var destination = new Bitmap(800, 800))
            using (var graphics = Graphics.FromImage(destination))
            {
                graphics.InterpolationMode = InterpolationMode.Default;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var font = new Font("Arial", 12, GraphicsUnit.Point))
                {
                    graphics.DrawString(TextToRender, font, System.Drawing.Brushes.HotPink, new RectangleF(10, 10, 780, 780));
                }
            }
        }

        [Benchmark(Description = "ImageSharp Draw Text - Cached Glyphs")]
        public void DrawTextCore()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                var font = SixLabors.Fonts.SystemFonts.CreateFont("Arial", 12);
                image.Mutate(x => x.ApplyProcessor(new DrawTextProcessor(new TextGraphicsOptions(true) { WrapTextWidth = 780 }, TextToRender, font, Processing.Brushes.Solid(Rgba32.HotPink), null, new SixLabors.Primitives.PointF(10, 10))));
            }
        }

        [Benchmark(Description = "ImageSharp Draw Text - Nieve")]
        public void DrawTextCoreOld()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                var font = SixLabors.Fonts.SystemFonts.CreateFont("Arial", 12);
                image.Mutate(x => DrawTextOldVersion(x, new TextGraphicsOptions(true) { WrapTextWidth = 780 }, TextToRender, font, Processing.Brushes.Solid(Rgba32.HotPink), null, new SixLabors.Primitives.PointF(10, 10)));
            }

            IImageProcessingContext DrawTextOldVersion(
                IImageProcessingContext source,
                TextGraphicsOptions options,
                string text,
                SixLabors.Fonts.Font font,
                IBrush brush,
                IPen pen,
                SixLabors.Primitives.PointF location)
            {
                float dpiX = 72;
                float dpiY = 72;

                var style = new SixLabors.Fonts.RendererOptions(font, dpiX, dpiY, location)
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