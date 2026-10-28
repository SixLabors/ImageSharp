// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using static System.Net.Mime.MediaTypeNames;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Converts channels to different pixel formats and undoes
/// orientation.
/// </summary>
internal static class JxlExternalImageDecoder
{
    public static void FloatToU32(Span<float> input, Span<uint> output, int num, float mul, int bitsPerSample)
    {
        Vector<float> one = Vector<float>.One;
        Vector<float> scale = Vector.Create(mul);

        for (int x = 0; x <= num - Vector<float>.Count; x += Vector<float>.Count)
        {
            Vector<float> v = Vector.Create<float>(input[x..]);
            v = Vector.ClampNative(v, Vector<float>.Zero, one);

            Vector<int> i = Vector.ConvertToInt32Native(v * scale);
            i.As<int, uint>().CopyTo(output[x..]);
        }
    }

    public static JxlPlane<T> UndoOrientation<T>(Configuration configuration, JxlExifOrientation undoOrientation, JxlPlane<T> image)
        where T : unmanaged
    {
        JxlPlane<T> output;
        int xSize = image.XSize;
        int ySize = image.YSize;

        if (undoOrientation == JxlExifOrientation.FlipHorizontal)
        {
            output = JxlPlane<T>.Create(configuration, xSize, ySize);

            for (int y = 0; y < ySize; y++)
            {
                Span<T> rowIn = image.GetRow(y);
                Span<T> rowOut = output.GetRow(y);

                for (int x = 0; x < xSize; ++x)
                {
                    rowOut[xSize - x - 1] = rowIn[x];
                }
            }
        }
        else if (undoOrientation == JxlExifOrientation.Rotate180)
        {
            output = JxlPlane<T>.Create(configuration, xSize, ySize);

            for (int y = 0; y < ySize; y++)
            {
                Span<T> rowIn = image.GetRow(y);
                Span<T> rowOut = output.GetRow(ySize - y - 1);

                for (int x = 0; x < xSize; ++x)
                {
                    rowOut[xSize - x - 1] = rowIn[x];
                }
            }
        }
        // TODO
    }
}
