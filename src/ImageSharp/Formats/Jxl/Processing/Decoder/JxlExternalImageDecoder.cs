// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

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

        for (int x = 0; x < num; x += Vector<float>.Count)
        {
            Vector<float> v = Vector.Create<float>(input[x..]);
            v = Vector.ClampNative(v, Vector<float>.Zero, one);

            Vector<int> i = Vector.ConvertToInt32Native(v * scale);
            i.As<int, uint>().CopyTo(output[x..]);
        }
    }
}
