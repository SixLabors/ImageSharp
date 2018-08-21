// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Utils;
using SixLabors.Memory;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing.Processors.Text
{
    /// <summary>
    /// Using the brush as a source of pixels colors blends the brush color with source.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DrawTextProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private CachingGlyphRenderer textRenderer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawTextProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="text">The text we want to render</param>
        /// <param name="font">The font we want to render with</param>
        /// <param name="brush">The brush to source pixel colors from.</param>
        /// <param name="pen">The pen to outline text with.</param>
        /// <param name="location">The location on the image to start drawign the text from.</param>
        public DrawTextProcessor(TextGraphicsOptions options, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, PointF location)
        {
            Guard.NotNull(text, nameof(text));
            Guard.NotNull(font, nameof(font));

            if (brush is null && pen is null)
            {
                throw new ArgumentNullException($"Expected a {nameof(brush)} or {nameof(pen)}. Both were null");
            }

            this.Options = options;
            this.Text = text;
            this.Font = font;
            this.Location = location;
            this.Brush = brush;
            this.Pen = pen;
        }

        /// <summary>
        /// Gets the brush.
        /// </summary>
        public IBrush<TPixel> Brush { get; }

        /// <summary>
        /// Gets the options
        /// </summary>
        public TextGraphicsOptions Options { get; }

        /// <summary>
        /// Gets  the text
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the pen used for outlining the text, if Null then we will not outline
        /// </summary>
        public IPen<TPixel> Pen { get; }

        /// <summary>
        /// Gets the font used to render the text.
        /// </summary>
        public Font Font { get; }

        /// <summary>
        /// Gets the location to draw the text at.
        /// </summary>
        public PointF Location { get; }

        protected override void BeforeImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            base.BeforeImageApply(source, sourceRectangle);

            // do everythign at the image level as we are deligating the processing down to other processors
            var style = new RendererOptions(this.Font, this.Options.DpiX, this.Options.DpiY, this.Location)
            {
                ApplyKerning = this.Options.ApplyKerning,
                TabWidth = this.Options.TabWidth,
                WrappingWidth = this.Options.WrapTextWidth,
                HorizontalAlignment = this.Options.HorizontalAlignment,
                VerticalAlignment = this.Options.VerticalAlignment
            };

            this.textRenderer = new CachingGlyphRenderer(source.GetMemoryAllocator(), this.Text.Length, this.Pen, this.Brush != null);
            this.textRenderer.Options = (GraphicsOptions)this.Options;
            TextRenderer.RenderTextTo(this.textRenderer, this.Text, style);
        }

        protected override void AfterImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            base.AfterImageApply(source, sourceRectangle);
            this.textRenderer?.Dispose();
            this.textRenderer = null;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            // this is a no-op as we have processes all as an image, we should be able to pass out of before email apply a skip frames outcome
            Draw(this.textRenderer.FillOperations, this.Brush);
            Draw(this.textRenderer.OutlineOperations, this.Pen?.StrokeFill);

            void Draw(List<DrawingOperation> operations, IBrush<TPixel> brush)
            {
                if (operations?.Count > 0)
                {
                    using (BrushApplicator<TPixel> app = brush.CreateApplicator(source, sourceRectangle, this.textRenderer.Options))
                    {
                        foreach (DrawingOperation operation in operations)
                        {
                            Buffer2D<float> buffer = operation.Map;
                            int startY = operation.Location.Y;
                            int startX = operation.Location.X;
                            int offSetSpan = 0;
                            if (startX < 0)
                            {
                                offSetSpan = -startX;
                                startX = 0;
                            }

                            int fistRow = 0;
                            if (startY < 0)
                            {
                                fistRow = -startY;
                            }

                            int end = operation.Map.Height;

                            int maxHeight = source.Height - startY;
                            end = Math.Min(end, maxHeight);

                            for (int row = fistRow; row < end; row++)
                            {
                                int y = startY + row;
                                Span<float> span = buffer.GetRowSpan(row).Slice(offSetSpan);
                                app.Apply(span, startX, y);
                            }
                        }
                    }
                }
            }
        }

        private struct DrawingOperation
        {
            public Buffer2D<float> Map { get; set; }

            public Point Location { get; set; }
        }

        private class CachingGlyphRenderer : IGlyphRenderer, IDisposable
        {
            private PathBuilder builder;

            private Point currentRenderPosition = default;
            private GlyphRendererParameters currentGlyphRenderParams = default;
            private int offset = 0;
            private PointF currentPoint = default(PointF);

            private readonly Dictionary<GlyphRendererParameters, GlyphRenderData> glyphData = new Dictionary<GlyphRendererParameters, GlyphRenderData>();

            private bool renderOutline = false;
            private bool renderFill = false;
            private bool raterizationRequired = false;

            public CachingGlyphRenderer(MemoryAllocator memoryAllocator, int size, IPen pen, bool renderFill)
            {
                this.MemoryAllocator = memoryAllocator;
                this.Pen = pen;
                this.renderFill = renderFill;
                this.renderOutline = pen != null;
                this.offset = 2;
                if (this.renderFill)
                {
                    this.FillOperations = new List<DrawingOperation>(size);
                }

                if (this.renderOutline)
                {
                    this.offset = (int)MathF.Ceiling((pen.StrokeWidth * 2) + 2);
                    this.OutlineOperations = new List<DrawingOperation>(size);
                }

                this.builder = new PathBuilder();
            }

            public List<DrawingOperation> FillOperations { get; }

            public List<DrawingOperation> OutlineOperations { get; }

            public MemoryAllocator MemoryAllocator { get; internal set; }

            public IPen Pen { get; internal set; }

            public GraphicsOptions Options { get; internal set; }

            public void BeginFigure()
            {
                this.builder.StartFigure();
            }

            public bool BeginGlyph(RectangleF bounds, GlyphRendererParameters paramters)
            {
                this.currentRenderPosition = Point.Truncate(bounds.Location);

                // we have offset our rendering origion a little bit down to prevent edge cropping, move the draw origin up to compensate
                this.currentRenderPosition = new Point(this.currentRenderPosition.X - this.offset, this.currentRenderPosition.Y - this.offset);
                this.currentGlyphRenderParams = paramters;
                if (this.glyphData.ContainsKey(paramters))
                {
                    // we have already drawn the glyph vectors skip trying again
                    this.raterizationRequired = false;
                    return false;
                }

                // we check to see if we have a render cache and if we do then we render else
                this.builder.Clear();

                // ensure all glyphs render around [zero, zero]  so offset negative root positions so when we draw the glyph we can offet it back
                this.builder.SetOrigin(new PointF(-(int)bounds.X + this.offset, -(int)bounds.Y + this.offset));

                this.raterizationRequired = true;
                return true;
            }

            public void BeginText(RectangleF bounds)
            {
                // not concerned about this one
                this.OutlineOperations?.Clear();
                this.FillOperations?.Clear();
            }

            public void CubicBezierTo(PointF secondControlPoint, PointF thirdControlPoint, PointF point)
            {
                this.builder.AddBezier(this.currentPoint, secondControlPoint, thirdControlPoint, point);
                this.currentPoint = point;
            }

            public void Dispose()
            {
                foreach (KeyValuePair<GlyphRendererParameters, GlyphRenderData> kv in this.glyphData)
                {
                    kv.Value.Dispose();
                }

                this.glyphData.Clear();
            }

            public void EndFigure()
            {
                this.builder.CloseFigure();
            }

            public void EndGlyph()
            {
                GlyphRenderData renderData = default;

                // has the glyoh been rendedered already????
                if (this.raterizationRequired)
                {
                    IPath path = this.builder.Build();

                    if (this.renderFill)
                    {
                        renderData.FillMap = this.Render(path);
                    }

                    if (this.renderOutline)
                    {
                        if (this.Pen.StrokePattern.Length == 0)
                        {
                            path = path.GenerateOutline(this.Pen.StrokeWidth);
                        }
                        else
                        {
                            path = path.GenerateOutline(this.Pen.StrokeWidth, this.Pen.StrokePattern);
                        }

                        renderData.OutlineMap = this.Render(path);
                    }

                    this.glyphData[this.currentGlyphRenderParams] = renderData;
                }
                else
                {
                    renderData = this.glyphData[this.currentGlyphRenderParams];
                }

                if (this.renderFill)
                {
                    this.FillOperations.Add(new DrawingOperation
                    {
                        Location = this.currentRenderPosition,
                        Map = renderData.FillMap
                    });
                }

                if (this.renderOutline)
                {
                    this.OutlineOperations.Add(new DrawingOperation
                    {
                        Location = this.currentRenderPosition,
                        Map = renderData.OutlineMap
                    });
                }
            }

            private Buffer2D<float> Render(IPath path)
            {
                Size size = Rectangle.Ceiling(path.Bounds).Size;
                size = new Size(size.Width + (this.offset * 2), size.Height + (this.offset * 2));

                float subpixelCount = 4;
                float offset = 0.5f;
                if (this.Options.Antialias)
                {
                    offset = 0f; // we are antialising skip offsetting as real antalising should take care of offset.
                    subpixelCount = this.Options.AntialiasSubpixelDepth;
                    if (subpixelCount < 4)
                    {
                        subpixelCount = 4;
                    }
                }

                // take the path inside the path builder, scan thing and generate a Buffer2d representing the glyph and cache it.
                Buffer2D<float> fullBuffer = this.MemoryAllocator.Allocate2D<float>(size.Width + 1, size.Height + 1, AllocationOptions.Clean);

                using (IMemoryOwner<float> bufferBacking = this.MemoryAllocator.Allocate<float>(path.MaxIntersections))
                using (IMemoryOwner<PointF> rowIntersectionBuffer = this.MemoryAllocator.Allocate<PointF>(size.Width))
                {
                    float subpixelFraction = 1f / subpixelCount;
                    float subpixelFractionPoint = subpixelFraction / subpixelCount;

                    for (int y = 0; y <= size.Height; y++)
                    {
                        Span<float> scanline = fullBuffer.GetRowSpan(y);
                        bool scanlineDirty = false;
                        float yPlusOne = y + 1;

                        for (float subPixel = (float)y; subPixel < yPlusOne; subPixel += subpixelFraction)
                        {
                            var start = new PointF(path.Bounds.Left - 1, subPixel);
                            var end = new PointF(path.Bounds.Right + 1, subPixel);
                            Span<PointF> intersectionSpan = rowIntersectionBuffer.GetSpan();
                            Span<float> buffer = bufferBacking.GetSpan();
                            int pointsFound = path.FindIntersections(start, end, intersectionSpan);

                            if (pointsFound == 0)
                            {
                                // nothing on this line skip
                                continue;
                            }

                            for (int i = 0; i < pointsFound && i < intersectionSpan.Length; i++)
                            {
                                buffer[i] = intersectionSpan[i].X;
                            }

                            QuickSort.Sort(buffer.Slice(0, pointsFound));

                            for (int point = 0; point < pointsFound; point += 2)
                            {
                                // points will be paired up
                                float scanStart = buffer[point];
                                float scanEnd = buffer[point + 1];
                                int startX = (int)MathF.Floor(scanStart + offset);
                                int endX = (int)MathF.Floor(scanEnd + offset);

                                if (startX >= 0 && startX < scanline.Length)
                                {
                                    for (float x = scanStart; x < startX + 1; x += subpixelFraction)
                                    {
                                        scanline[startX] += subpixelFractionPoint;
                                        scanlineDirty = true;
                                    }
                                }

                                if (endX >= 0 && endX < scanline.Length)
                                {
                                    for (float x = endX; x < scanEnd; x += subpixelFraction)
                                    {
                                        scanline[endX] += subpixelFractionPoint;
                                        scanlineDirty = true;
                                    }
                                }

                                int nextX = startX + 1;
                                endX = Math.Min(endX, scanline.Length); // reduce to end to the right edge
                                nextX = Math.Max(nextX, 0);
                                for (int x = nextX; x < endX; x++)
                                {
                                    scanline[x] += subpixelFraction;
                                    scanlineDirty = true;
                                }
                            }
                        }

                        if (scanlineDirty)
                        {
                            if (!this.Options.Antialias)
                            {
                                for (int x = 0; x < size.Width; x++)
                                {
                                    if (scanline[x] >= 0.5)
                                    {
                                        scanline[x] = 1;
                                    }
                                    else
                                    {
                                        scanline[x] = 0;
                                    }
                                }
                            }
                        }
                    }
                }

                return fullBuffer;
            }

            public void EndText()
            {
            }

            public void LineTo(PointF point)
            {
                this.builder.AddLine(this.currentPoint, point);
                this.currentPoint = point;
            }

            public void MoveTo(PointF point)
            {
                this.builder.StartFigure();
                this.currentPoint = point;
            }

            public void QuadraticBezierTo(PointF secondControlPoint, PointF point)
            {
                this.builder.AddBezier(this.currentPoint, secondControlPoint, point);
                this.currentPoint = point;
            }

            private struct GlyphRenderData : IDisposable
            {
                public Buffer2D<float> FillMap;

                public Buffer2D<float> OutlineMap;

                public void Dispose()
                {
                    this.FillMap?.Dispose();
                    this.OutlineMap?.Dispose();
                }
            }
        }
    }
}