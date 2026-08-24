// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;

/// <summary>
/// Applies normative AV1 horizontal super-resolution upscaling to a reconstructed still-image frame.
/// </summary>
internal class Av1SuperResolutionDecoder
{
    /// <summary>
    /// The sequence-level bit-depth and color-plane configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The coded and upscaled frame dimensions.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The reconstructed planes updated with the upscaled samples.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SuperResolutionDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining bit depth and color layout.</param>
    /// <param name="frameHeader">The frame header defining coded and upscaled dimensions.</param>
    /// <param name="frameBuffer">The CDEF-filtered frame samples to upscale.</param>
    public Av1SuperResolutionDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameBuffer = frameBuffer;
    }

    /// <summary>
    /// Upscales each color plane horizontally and updates the visible frame dimensions.
    /// </summary>
    public void DecodeFrame()
    {
        ObuFrameSize frameSize = this.frameHeader.FrameSize;
        int codedWidth = frameSize.FrameWidth;
        int upscaledWidth = frameSize.SuperResolutionUpscaledWidth;
        if (codedWidth != upscaledWidth)
        {
            ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
            for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
                int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
                this.UpscalePlane(plane, subsamplingX, subsamplingY, codedWidth, upscaledWidth, frameSize.FrameHeight);
            }
        }

        // Plane-region consumers must observe the reconstructed frame rather than the sequence maxima
        // used to size the reusable backing allocations.
        this.frameBuffer.Width = upscaledWidth;
        this.frameBuffer.Height = frameSize.FrameHeight;
    }

    /// <summary>
    /// Upscales every row of one color plane with the normative fixed filter.
    /// </summary>
    /// <param name="plane">The color plane to upscale.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="codedLumaWidth">The coded luma width before upscaling.</param>
    /// <param name="upscaledLumaWidth">The luma width after upscaling.</param>
    /// <param name="lumaHeight">The unchanged luma height.</param>
    private void UpscalePlane(
        Av1Plane plane,
        int subsamplingX,
        int subsamplingY,
        int codedLumaWidth,
        int upscaledLumaWidth,
        int lumaHeight)
    {
        int codedWidth = Av1Math.DivideLog2Ceiling(codedLumaWidth, subsamplingX);
        int upscaledWidth = Av1Math.DivideLog2Ceiling(upscaledLumaWidth, subsamplingX);
        int reconstructedWidth = this.frameHeader.ModeInfoColumnCount << (Av1Constants.ModeInfoSizeLog2 - subsamplingX);
        int height = Av1Math.DivideLog2Ceiling(lumaHeight, subsamplingY);
        int step = Av1SuperResolutionKernels.GetConvolveStep(codedWidth, upscaledWidth);
        int initialSubpixel = Av1SuperResolutionKernels.GetInitialSubpixel(codedWidth, upscaledWidth, step);
        ushort[] sourceRow = new ushort[reconstructedWidth + (Av1SuperResolutionKernels.SourceBorder * 2)];
        ushort[] outputRow = new ushort[upscaledWidth];

        Span<byte> lowBitDepthPlane = default;
        Span<ushort> highBitDepthPlane = default;
        int stride;
        if (this.frameBuffer.BytesPerSample == 2)
        {
            Span<short> signedPlane = this.frameBuffer.DeriveBlockPointer16(
                plane,
                Point.Empty,
                subsamplingX,
                subsamplingY,
                out stride);

            highBitDepthPlane = MemoryMarshal.Cast<short, ushort>(signedPlane);
        }
        else
        {
            lowBitDepthPlane = this.frameBuffer.DeriveBlockPointer(
                plane,
                Point.Empty,
                subsamplingX,
                subsamplingY,
                out stride);
        }

        int sourceStart = Av1SuperResolutionKernels.SourceBorder;
        int bitDepth = this.frameBuffer.BitDepth.GetBitCount();

        // libaom partitions the same continuous phase progression by tile column but does not pad
        // internal boundaries. Filtering the complete row therefore produces the identical samples.
        for (int row = 0; row < height; row++)
        {
            Span<ushort> reconstructedSamples = sourceRow.AsSpan(sourceStart, reconstructedWidth);
            int planeOffset = stride + (row * stride);
            if (this.frameBuffer.BytesPerSample == 2)
            {
                highBitDepthPlane.Slice(planeOffset, reconstructedWidth).CopyTo(reconstructedSamples);
            }
            else
            {
                Span<byte> input = lowBitDepthPlane.Slice(planeOffset, reconstructedWidth);
                for (int column = 0; column < reconstructedWidth; column++)
                {
                    reconstructedSamples[column] = input[column];
                }
            }

            // Phase derives from the exact coded width, but taps consume reconstruction through the
            // enclosing 8-sample mode-info edge. Only samples beyond that aligned edge are replicated.
            sourceRow.AsSpan(0, sourceStart).Fill(reconstructedSamples[0]);
            sourceRow.AsSpan(sourceStart + reconstructedWidth, sourceStart).Fill(reconstructedSamples[^1]);
            Av1SuperResolutionKernels.UpscaleRow(sourceRow, outputRow, step, initialSubpixel, bitDepth);

            if (this.frameBuffer.BytesPerSample == 2)
            {
                outputRow.CopyTo(highBitDepthPlane.Slice(planeOffset, upscaledWidth));
            }
            else
            {
                Span<byte> output = lowBitDepthPlane.Slice(planeOffset, upscaledWidth);
                for (int column = 0; column < upscaledWidth; column++)
                {
                    output[column] = (byte)outputRow[column];
                }
            }
        }
    }
}
