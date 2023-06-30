// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Image encoder for writing an image to a stream as a QOi image
/// </summary>
public class QoiEncoderCore : IImageEncoderInternals
{
    private readonly QoiEncoder encoder;
    /// <summary>
    /// Initializes a new instance of the <see cref="QoiEncoderCore"/> class.
    /// </summary>
    public QoiEncoderCore(QoiEncoder encoder) => this.encoder = encoder;

    /// <inheritdoc />
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        this.WriteHeader(image, stream);
        WritePixels(image, stream);
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

    private static void WritePixels<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Start image encoding
        Rgba32[] previouslySeenPixels = new Rgba32[64];
        Rgba32 previousPixel = new(0, 0, 0, 255);
        Rgba32 currentRgba32 = default;
        Buffer2D<TPixel> pixels = image.Frames[0].PixelBuffer;

        for (int i = 0; i < pixels.Height; i++)
        {
            for (int j = 0; j < pixels.Width && i < pixels.Height; j++)
            {
                // We get the RGBA value from pixels
                Span<TPixel> row = pixels.DangerousGetRowSpan(i);
                TPixel currentPixel = pixels[j, i];
                currentPixel.ToRgba32(ref currentRgba32);

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
                    byte repetitions = 0;
                    do
                    {
                        repetitions++;
                        j++;
                        if (j == pixels.Width)
                        {
                            j = 0;
                            i++;
                            if (i == pixels.Height)
                            {
                                break;
                            }
                            row = pixels.DangerousGetRowSpan(i);
                        }


                        currentPixel = row[j];
                        currentPixel.ToRgba32(ref currentRgba32);
                    }
                    while (currentRgba32.Equals(previousPixel) && repetitions < 62);

                    j--;
                    stream.WriteByte((byte)((byte)QoiChunk.QoiOpRun | (repetitions - 1)));

                    /* If it's a QOI_OP_RUN, we don't overwrite the previous pixel since
                     * it will be taken and compared on the next iteration
                     */
                    continue;
                }

                // else, we check if it exists in the previously seen pixels
                // If so, we do a QOI_OP_INDEX
                int pixelArrayPosition = GetArrayPosition(currentRgba32);
                if (previouslySeenPixels[pixelArrayPosition].Equals(currentPixel))
                {
                    stream.WriteByte((byte)pixelArrayPosition);
                }
                else
                {
                    // else, we check if the difference is less than -2..1
                    // Since it wasn't found on the previously seen pixels, we save it
                    previouslySeenPixels[pixelArrayPosition] = currentRgba32;

                    sbyte diffRed = (sbyte)(currentRgba32.R - previousPixel.R),
                        diffGreen = (sbyte)(currentRgba32.G - previousPixel.G),
                        diffBlue = (sbyte)(currentRgba32.B - previousPixel.B);

                    // If so, we do a QOI_OP_DIFF
                    if (diffRed is > -3 and < 2 &&
                        diffGreen is > -3 and < 2 &&
                        diffBlue is > -3 and < 2 &&
                        currentRgba32.A == previousPixel.A)
                    {
                        // Bottom limit is -2, so we add 2 to make it equal to 0
                        byte dr = (byte)(diffRed + 2),
                            dg = (byte)(diffGreen + 2),
                            db = (byte)(diffBlue + 2),
                            valueToWrite = (byte)((byte)QoiChunk.QoiOpDiff | (dr << 4) | (dg << 2) | db);
                        stream.WriteByte(valueToWrite);
                    }
                    else
                    {
                        // else, we check if the green difference is less than -32..31 and the rest -8..7
                        // If so, we do a QOI_OP_LUMA
                        sbyte diffRedGreen = (sbyte)(diffRed - diffGreen),
                            diffBlueGreen = (sbyte)(diffBlue - diffGreen);
                        if (diffGreen is > -33 and < 8 &&
                            diffRedGreen is > -9 and < 8 &&
                            diffBlueGreen is > -9 and < 8 &&
                            currentRgba32.A == previousPixel.A)
                        {
                            byte dr_dg = (byte)(diffRedGreen + 8),
                                db_dg = (byte)(diffBlueGreen + 8),
                                byteToWrite1 = (byte)((byte)QoiChunk.QoiOpLuma | (diffGreen + 32)),
                                byteToWrite2 = (byte)((dr_dg << 4) | db_dg);
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
