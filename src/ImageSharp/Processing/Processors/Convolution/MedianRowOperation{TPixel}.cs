// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies an median filter.
    /// </summary>
    internal readonly struct MedianRowOperation<TPixel> : IRowOperation<Vector4>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly int yChannelStart;
        private readonly int zChannelStart;
        private readonly int wChannelStart;
        private readonly Configuration configuration;
        private readonly Rectangle bounds;
        private readonly Buffer2D<TPixel> targetPixels;
        private readonly Buffer2D<TPixel> sourcePixels;
        private readonly KernelSamplingMap map;
        private readonly int kernelSize;
        private readonly bool preserveAlpha;

        public MedianRowOperation(Rectangle bounds, Buffer2D<TPixel> targetPixels, Buffer2D<TPixel> sourcePixels, KernelSamplingMap map, int kernelSize, Configuration configuration, bool preserveAlpha)
        {
            this.bounds = bounds;
            this.configuration = configuration;
            this.targetPixels = targetPixels;
            this.sourcePixels = sourcePixels;
            this.map = map;
            this.kernelSize = kernelSize;
            this.preserveAlpha = preserveAlpha;
            int kernelCount = this.kernelSize * this.kernelSize;
            this.yChannelStart = kernelCount;
            this.zChannelStart = this.yChannelStart + kernelCount;
            this.wChannelStart = this.zChannelStart + kernelCount;
        }

        public void Invoke(int y, Span<Vector4> span)
        {
            // Span has kernelSize^2 followed by bound width.
            int boundsLeft = this.bounds.Left;
            int boundsWidth = this.bounds.Width;
            int boundsRight = this.bounds.Right;
            int kernelCount = this.kernelSize * this.kernelSize;
            Span<Vector4> kernelBuffer = span.Slice(0, kernelCount);
            Span<Vector4> channelVectorBuffer = span.Slice(kernelCount, kernelCount);
            Span<Vector4> targetBuffer = span.Slice(kernelCount << 1, boundsWidth);

            // Stack 4 channels of floats in the space of Vector4's.
            Span<float> channelBuffer = MemoryMarshal.Cast<Vector4, float>(channelVectorBuffer);
            var xChannel = channelBuffer.Slice(0, kernelCount);
            var yChannel = channelBuffer.Slice(this.yChannelStart, kernelCount);
            var zChannel = channelBuffer.Slice(this.zChannelStart, kernelCount);

            var xOffsets = this.map.GetColumnOffsetSpan();
            var yOffsets = this.map.GetRowOffsetSpan();
            var baseXOffsetIndex = 0;
            var baseYOffsetIndex = (y - this.bounds.Top) * this.kernelSize;

            if (this.preserveAlpha)
            {
                for (var x = boundsLeft; x < boundsRight; x++)
                {
                    var index = 0;
                    for (var w = 0; w < this.kernelSize; w++)
                    {
                        var j = yOffsets[baseYOffsetIndex + w];
                        var row = this.sourcePixels.DangerousGetRowSpan(j);
                        for (var z = 0; z < this.kernelSize; z++)
                        {
                            var k = xOffsets[baseXOffsetIndex + z];
                            var pixel = row[k];
                            kernelBuffer[index + z] = pixel.ToVector4();
                        }

                        index += this.kernelSize;
                    }

                    targetBuffer[x - boundsLeft] = this.FindMedian3(kernelBuffer, xChannel, yChannel, zChannel, kernelCount);
                    baseXOffsetIndex += this.kernelSize;
                }
            }
            else
            {
                var wChannel = channelBuffer.Slice(this.wChannelStart, kernelCount);
                for (var x = boundsLeft; x < boundsRight; x++)
                {
                    var index = 0;
                    for (var w = 0; w < this.kernelSize; w++)
                    {
                        var j = yOffsets[baseYOffsetIndex + w];
                        var row = this.sourcePixels.DangerousGetRowSpan(j);
                        for (var z = 0; z < this.kernelSize; z++)
                        {
                            var k = xOffsets[baseXOffsetIndex + z];
                            var pixel = row[k];
                            kernelBuffer[index + z] = pixel.ToVector4();
                        }

                        index += this.kernelSize;
                    }

                    targetBuffer[x - boundsLeft] = this.FindMedian4(kernelBuffer, xChannel, yChannel, zChannel, wChannel, kernelCount);
                    baseXOffsetIndex += this.kernelSize;
                }
            }

            Span<TPixel> targetRowSpan = this.targetPixels.DangerousGetRowSpan(y).Slice(boundsLeft, boundsWidth);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetBuffer, targetRowSpan);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 FindMedian3(Span<Vector4> kernelSpan, Span<float> xChannel, Span<float> yChannel, Span<float> zChannel, int stride)
        {
            int halfLength = (kernelSpan.Length + 1) >> 1;

            // Split color channels
            for (int i = 0; i < xChannel.Length; i++)
            {
                xChannel[i] = kernelSpan[i].X;
                yChannel[i] = kernelSpan[i].Y;
                zChannel[i] = kernelSpan[i].Z;
            }

            // Sort each channel serarately.
            xChannel.Sort();
            yChannel.Sort();
            zChannel.Sort();

            return new Vector4(xChannel[halfLength], yChannel[halfLength], zChannel[halfLength], kernelSpan[halfLength].W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 FindMedian4(Span<Vector4> kernelSpan, Span<float> xChannel, Span<float> yChannel, Span<float> zChannel, Span<float> wChannel, int stride)
        {
            int halfLength = (kernelSpan.Length + 1) >> 1;

            // Split color channels
            for (int i = 0; i < xChannel.Length; i++)
            {
                xChannel[i] = kernelSpan[i].X;
                yChannel[i] = kernelSpan[i].Y;
                zChannel[i] = kernelSpan[i].Z;
                wChannel[i] = kernelSpan[i].W;
            }

            // Sort each channel serarately.
            xChannel.Sort();
            yChannel.Sort();
            zChannel.Sort();
            wChannel.Sort();

            return new Vector4(xChannel[halfLength], yChannel[halfLength], zChannel[halfLength], wChannel[halfLength]);
        }
    }
}
