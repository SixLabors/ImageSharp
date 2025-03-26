// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Image encoder for writing an image to a stream as a QOi image
/// </summary>
internal class QoiEncoderCore
{
    /// <summary>
    /// The encoder with options
    /// </summary>
    private readonly QoiEncoder encoder;

    /// <summary>
    /// Used the manage memory allocations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The configuration instance for the encoding operation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="QoiEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder with options.</param>
    /// <param name="configuration">The configuration of the Encoder.</param>
    public QoiEncoderCore(QoiEncoder encoder, Configuration configuration)
    {
        this.encoder = encoder;
        this.configuration = configuration;
        this.memoryAllocator = configuration.MemoryAllocator;
    }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        this.WriteHeader(image, stream);
        this.WritePixels(image, stream, cancellationToken);
        WriteEndOfStream(stream);
        stream.Flush();
    }

    private void WriteHeader(Image image, Stream stream)
    {
        // Get metadata
        Span<byte> width = stackalloc byte[4];
        Span<byte> height = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(width, (uint)image.Width);
        BinaryPrimitives.WriteUInt32BigEndian(height, (uint)image.Height);
        QoiChannels qoiChannels = this.encoder.Channels ?? QoiChannels.Rgba;
        QoiColorSpace qoiColorSpace = this.encoder.ColorSpace ?? QoiColorSpace.SrgbWithLinearAlpha;

        // Write header to the stream
        stream.Write(QoiConstants.Magic);
        stream.Write(width);
        stream.Write(height);
        stream.WriteByte((byte)qoiChannels);
        stream.WriteByte((byte)qoiColorSpace);
    }

    private void WritePixels<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Start image encoding
        using IMemoryOwner<Rgba32> previouslySeenPixelsBuffer = this.memoryAllocator.Allocate<Rgba32>(64, AllocationOptions.Clean);
        Span<Rgba32> previouslySeenPixels = previouslySeenPixelsBuffer.GetSpan();
        Rgba32 previousPixel = new(0, 0, 0, 255);
        Rgba32 currentRgba32 = default;

        ImageFrame<TPixel>? clonedFrame = null;
        try
        {
            if (EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(this.encoder.TransparentColorMode))
            {
                clonedFrame = image.Frames.RootFrame.Clone();
                EncodingUtilities.ReplaceTransparentPixels(clonedFrame, Color.Transparent);
            }

            ImageFrame<TPixel> encodingFrame = clonedFrame ?? image.Frames.RootFrame;
            Buffer2D<TPixel> pixels = encodingFrame.PixelBuffer;

            using IMemoryOwner<Rgba32> rgbaRowBuffer = this.memoryAllocator.Allocate<Rgba32>(pixels.Width);
            Span<Rgba32> rgbaRow = rgbaRowBuffer.GetSpan();
            Configuration configuration = this.configuration;
            for (int i = 0; i < pixels.Height; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Span<TPixel> row = pixels.DangerousGetRowSpan(i);
                PixelOperations<TPixel>.Instance.ToRgba32(this.configuration, row, rgbaRow);
                for (int j = 0; j < row.Length && i < pixels.Height; j++)
                {
                    // We get the RGBA value from pixels
                    currentRgba32 = rgbaRow[j];

                    // First, we check if the current pixel is equal to the previous one
                    // If so, we do a QOI_OP_RUN
                    if (currentRgba32.Equals(previousPixel))
                    {
                        /* It looks like this isn't an error, but this makes possible that
                         * files start with a QOI_OP_RUN if their first pixel is a fully opaque
                         * black. However, the decoder of this project takes that into consideration
                         *
                         * To further details, see https://github.com/phoboslab/qoi/issues/258,
                         * and we should discuss what to do about this approach and
                         * if it's correct
                         */
                        int repetitions = 0;
                        do
                        {
                            repetitions++;
                            j++;
                            if (j == row.Length)
                            {
                                j = 0;
                                i++;
                                if (i == pixels.Height)
                                {
                                    break;
                                }

                                row = pixels.DangerousGetRowSpan(i);
                                PixelOperations<TPixel>.Instance.ToRgba32(configuration, row, rgbaRow);
                            }

                            currentRgba32 = rgbaRow[j];
                        }
                        while (currentRgba32.Equals(previousPixel) && repetitions < 62);

                        j--;
                        stream.WriteByte((byte)((int)QoiChunk.QoiOpRun | (repetitions - 1)));

                        /* If it's a QOI_OP_RUN, we don't overwrite the previous pixel since
                         * it will be taken and compared on the next iteration
                         */
                        continue;
                    }

                    // else, we check if it exists in the previously seen pixels
                    // If so, we do a QOI_OP_INDEX
                    int pixelArrayPosition = GetArrayPosition(currentRgba32);
                    if (previouslySeenPixels[pixelArrayPosition].Equals(currentRgba32))
                    {
                        stream.WriteByte((byte)pixelArrayPosition);
                    }
                    else
                    {
                        // else, we check if the difference is less than -2..1
                        // Since it wasn't found on the previously seen pixels, we save it
                        previouslySeenPixels[pixelArrayPosition] = currentRgba32;

                        int diffRed = currentRgba32.R - previousPixel.R;
                        int diffGreen = currentRgba32.G - previousPixel.G;
                        int diffBlue = currentRgba32.B - previousPixel.B;

                        // If so, we do a QOI_OP_DIFF
                        if (diffRed is >= -2 and <= 1 &&
                            diffGreen is >= -2 and <= 1 &&
                            diffBlue is >= -2 and <= 1 &&
                            currentRgba32.A == previousPixel.A)
                        {
                            // Bottom limit is -2, so we add 2 to make it equal to 0
                            int dr = diffRed + 2;
                            int dg = diffGreen + 2;
                            int db = diffBlue + 2;
                            byte valueToWrite = (byte)((int)QoiChunk.QoiOpDiff | (dr << 4) | (dg << 2) | db);
                            stream.WriteByte(valueToWrite);
                        }
                        else
                        {
                            // else, we check if the green difference is less than -32..31 and the rest -8..7
                            // If so, we do a QOI_OP_LUMA
                            int diffRedGreen = diffRed - diffGreen;
                            int diffBlueGreen = diffBlue - diffGreen;
                            if (diffGreen is >= -32 and <= 31 &&
                                diffRedGreen is >= -8 and <= 7 &&
                                diffBlueGreen is >= -8 and <= 7 &&
                                currentRgba32.A == previousPixel.A)
                            {
                                int dr_dg = diffRedGreen + 8;
                                int db_dg = diffBlueGreen + 8;
                                byte byteToWrite1 = (byte)((int)QoiChunk.QoiOpLuma | (diffGreen + 32));
                                byte byteToWrite2 = (byte)((dr_dg << 4) | db_dg);
                                stream.WriteByte(byteToWrite1);
                                stream.WriteByte(byteToWrite2);
                            }
                            else
                            {
                                // else, we check if the alpha is equal to the previous pixel
                                // If so, we do a QOI_OP_RGB
                                if (currentRgba32.A == previousPixel.A)
                                {
                                    stream.WriteByte((byte)QoiChunk.QoiOpRgb);
                                    stream.WriteByte(currentRgba32.R);
                                    stream.WriteByte(currentRgba32.G);
                                    stream.WriteByte(currentRgba32.B);
                                }
                                else
                                {
                                    // else, we do a QOI_OP_RGBA
                                    stream.WriteByte((byte)QoiChunk.QoiOpRgba);
                                    stream.WriteByte(currentRgba32.R);
                                    stream.WriteByte(currentRgba32.G);
                                    stream.WriteByte(currentRgba32.B);
                                    stream.WriteByte(currentRgba32.A);
                                }
                            }
                        }
                    }

                    previousPixel = currentRgba32;
                }
            }
        }
        finally
        {
            clonedFrame?.Dispose();
        }
    }

    private static void WriteEndOfStream(Stream stream)
    {
        // Write bytes to end stream
        for (int i = 0; i < 7; i++)
        {
            stream.WriteByte(0);
        }

        stream.WriteByte(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetArrayPosition(Rgba32 pixel)
        => Numerics.Modulo64((pixel.R * 3) + (pixel.G * 5) + (pixel.B * 7) + (pixel.A * 11));
}
