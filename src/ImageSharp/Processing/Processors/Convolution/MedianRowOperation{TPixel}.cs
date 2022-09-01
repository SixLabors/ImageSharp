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
            int boundsX = this.bounds.X;
            int boundsWidth = this.bounds.Width;
            int kernelCount = this.kernelSize * this.kernelSize;
            Span<Vector4> kernelBuffer = span.Slice(0, kernelCount);
            Span<Vector4> channelVectorBuffer = span.Slice(kernelCount, kernelCount);
            Span<Vector4> targetBuffer = span.Slice(kernelCount << 1, boundsWidth);

            // Stack 4 channels of floats in the space of Vector4's.
            Span<float> channelBuffer = MemoryMarshal.Cast<Vector4, float>(channelVectorBuffer);
            Span<float> xChannel = channelBuffer.Slice(0, kernelCount);
            Span<float> yChannel = channelBuffer.Slice(this.yChannelStart, kernelCount);
            Span<float> zChannel = channelBuffer.Slice(this.zChannelStart, kernelCount);

            Span<int> xOffsets = this.map.GetColumnOffsetSpan();
            Span<int> yOffsets = this.map.GetRowOffsetSpan();
            int baseXOffsetIndex = 0;
            int baseYOffsetIndex = (y - this.bounds.Top) * this.kernelSize;

            if (this.preserveAlpha)
            {
                for (int x = 0; x < boundsWidth; x++)
                {
                    int index = 0;
                    for (int kY = 0; kY < this.kernelSize; kY++)
                    {
                        int currentY = yOffsets[baseYOffsetIndex + kY];
                        Span<TPixel> row = this.sourcePixels.DangerousGetRowSpan(currentY);
                        for (int kX = 0; kX < this.kernelSize; kX++)
                        {
                            int currentX = xOffsets[baseXOffsetIndex + kX];
                            TPixel pixel = row[currentX];
                            kernelBuffer[index] = pixel.ToVector4();
                            index++;
                        }
                    }

                    targetBuffer[x] = this.FindMedian3(kernelBuffer, xChannel, yChannel, zChannel, kernelCount);
                    baseXOffsetIndex += this.kernelSize;
                }
            }
            else
            {
                Span<float> wChannel = channelBuffer.Slice(this.wChannelStart, kernelCount);
                for (int x = 0; x < boundsWidth; x++)
                {
                    int index = 0;
                    for (int kY = 0; kY < this.kernelSize; kY++)
                    {
                        int j = yOffsets[baseYOffsetIndex + kY];
                        Span<TPixel> row = this.sourcePixels.DangerousGetRowSpan(j);
                        for (int kX = 0; kX < this.kernelSize; kX++)
                        {
                            int currentX = xOffsets[baseXOffsetIndex + kX];
                            TPixel pixel = row[currentX];
                            kernelBuffer[index] = pixel.ToVector4();
                            index++;
                        }
                    }

                    targetBuffer[x] = this.FindMedian4(kernelBuffer, xChannel, yChannel, zChannel, wChannel, kernelCount);
                    baseXOffsetIndex += this.kernelSize;
                }
            }

            Span<TPixel> targetRowSpan = this.targetPixels.DangerousGetRowSpan(y).Slice(boundsX, boundsWidth);
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

            // Taking the W value from the source pixels, where the middle index in the kernelSpan is by definition the resulting pixel.
            // This will preserve the alpha value.
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
