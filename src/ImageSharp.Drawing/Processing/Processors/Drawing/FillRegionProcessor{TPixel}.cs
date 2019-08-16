// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Utils;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing
{
    /// <summary>
    /// Using a brush and a shape fills shape with contents of brush the
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    /// <seealso cref="ImageProcessor{TPixel}" />
    internal class FillRegionProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly FillRegionProcessor definition;

        public FillRegionProcessor(FillRegionProcessor definition)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            GraphicsOptions options = this.definition.Options;
            IBrush brush = this.definition.Brush;
            Region region = this.definition.Region;
            Rectangle rect = region.Bounds;

            // Align start/end positions.
            int minX = Math.Max(0, rect.Left);
            int maxX = Math.Min(source.Width, rect.Right);
            int minY = Math.Max(0, rect.Top);
            int maxY = Math.Min(source.Height, rect.Bottom);
            if (minX >= maxX)
            {
                return; // no effect inside image;
            }

            if (minY >= maxY)
            {
                return; // no effect inside image;
            }

            int maxIntersections = region.MaxIntersections;
            float subpixelCount = 4;

            // we need to offset the pixel grid to account for when we outline a path.
            // basically if the line is [1,2] => [3,2] then when outlining at 1 we end up with a region of [0.5,1.5],[1.5, 1.5],[3.5,2.5],[2.5,2.5]
            // and this can cause missed fills when not using antialiasing.so we offset the pixel grid by 0.5 in the x & y direction thus causing the#
            // region to align with the pixel grid.
            float offset = 0.5f;
            if (options.Antialias)
            {
                offset = 0f; // we are antialiasing skip offsetting as real antialiasing should take care of offset.
                subpixelCount = options.AntialiasSubpixelDepth;
                if (subpixelCount < 4)
                {
                    subpixelCount = 4;
                }
            }

            using (BrushApplicator<TPixel> applicator = brush.CreateApplicator(source, rect, options))
            {
                int scanlineWidth = maxX - minX;
                using (IMemoryOwner<float> bBuffer = source.MemoryAllocator.Allocate<float>(maxIntersections))
                using (IMemoryOwner<float> bScanline = source.MemoryAllocator.Allocate<float>(scanlineWidth))
                {
                    bool scanlineDirty = true;
                    float subpixelFraction = 1f / subpixelCount;
                    float subpixelFractionPoint = subpixelFraction / subpixelCount;

                    Span<float> buffer = bBuffer.GetSpan();
                    Span<float> scanline = bScanline.GetSpan();

                    bool isSolidBrushWithoutBlending = this.IsSolidBrushWithoutBlending(out SolidBrush solidBrush);
                    TPixel solidBrushColor = isSolidBrushWithoutBlending ? solidBrush.Color.ToPixel<TPixel>() : default;

                    for (int y = minY; y < maxY; y++)
                    {
                        if (scanlineDirty)
                        {
                            scanline.Clear();
                            scanlineDirty = false;
                        }

                        float yPlusOne = y + 1;
                        for (float subPixel = y; subPixel < yPlusOne; subPixel += subpixelFraction)
                        {
                            int pointsFound = region.Scan(subPixel + offset, buffer, configuration);
                            if (pointsFound == 0)
                            {
                                // nothing on this line, skip
                                continue;
                            }

                            QuickSort.Sort(buffer.Slice(0, pointsFound));

                            for (int point = 0; point < pointsFound && point < buffer.Length - 1; point += 2)
                            {
                                // points will be paired up
                                float scanStart = buffer[point] - minX;
                                float scanEnd = buffer[point + 1] - minX;
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
                            if (!options.Antialias)
                            {
                                bool hasOnes = false;
                                bool hasZeros = false;
                                for (int x = 0; x < scanlineWidth; x++)
                                {
                                    if (scanline[x] >= 0.5)
                                    {
                                        scanline[x] = 1;
                                        hasOnes = true;
                                    }
                                    else
                                    {
                                        scanline[x] = 0;
                                        hasZeros = true;
                                    }
                                }

                                if (isSolidBrushWithoutBlending && hasOnes != hasZeros)
                                {
                                    if (hasOnes)
                                    {
                                        source.GetPixelRowSpan(y).Slice(minX, scanlineWidth).Fill(solidBrushColor);
                                    }

                                    continue;
                                }
                            }

                            applicator.Apply(scanline, minX, y);
                        }
                    }
                }
            }
        }

        private bool IsSolidBrushWithoutBlending(out SolidBrush solidBrush)
        {
            solidBrush = this.definition.Brush as SolidBrush;

            if (solidBrush == null)
            {
                return false;
            }

            return this.definition.Options.IsOpaqueColorWithoutBlending(solidBrush.Color);
        }
    }
}
