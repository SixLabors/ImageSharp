// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Text;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

public class JpegFixture : MeasureFixture
{
    public JpegFixture(ITestOutputHelper output)
        : base(output)
    {
    }

    // ReSharper disable once InconsistentNaming
    public static float[] Create8x8FloatData()
    {
        float[] result = new float[64];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                result[(i * 8) + j] = (i * 10) + j;
            }
        }

        return result;
    }

    // ReSharper disable once InconsistentNaming
    public static int[] Create8x8IntData()
    {
        int[] result = new int[64];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                result[(i * 8) + j] = (i * 10) + j;
            }
        }

        return result;
    }

    // ReSharper disable once InconsistentNaming
    public static short[] Create8x8ShortData()
    {
        short[] result = new short[64];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                short val = (short)((i * 10) + j);
                if ((i + j) % 2 == 0)
                {
                    val *= -1;
                }

                result[(i * 8) + j] = val;
            }
        }

        return result;
    }

    // ReSharper disable once InconsistentNaming
    public static int[] Create8x8RandomIntData(int minValue, int maxValue, int seed = 42)
    {
        Random rnd = new Random(seed);
        int[] result = new int[64];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                result[(i * 8) + j] = rnd.Next(minValue, maxValue);
            }
        }

        return result;
    }

    public static float[] Create8x8RandomFloatData(float minValue, float maxValue, int seed = 42, int xBorder = 8, int yBorder = 8)
    {
        Random rnd = new Random(seed);
        float[] result = new float[64];
        for (int y = 0; y < yBorder; y++)
        {
            int y8 = y * 8;
            for (int x = 0; x < xBorder; x++)
            {
                double val = rnd.NextDouble();
                val *= maxValue - minValue;
                val += minValue;

                result[y8 + x] = (float)val;
            }
        }

        return result;
    }

    internal static Block8x8F CreateRandomFloatBlock(float minValue, float maxValue, int seed = 42, int xBorder = 8, int yBorder = 8) =>
        Block8x8F.Load(Create8x8RandomFloatData(minValue, maxValue, seed, xBorder, yBorder));

    internal void Print8x8Data<T>(T[] data) => this.Print8x8Data(new Span<T>(data));

    internal void Print8x8Data<T>(Span<T> data)
    {
        StringBuilder bld = new StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                bld.Append($"{data[(i * 8) + j],3} ");
            }

            bld.AppendLine();
        }

        this.Output.WriteLine(bld.ToString());
    }

    internal void PrintLinearData<T>(T[] data) => this.PrintLinearData(new Span<T>(data), data.Length);

    internal void PrintLinearData<T>(Span<T> data, int count = -1)
    {
        if (count < 0)
        {
            count = data.Length;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append($"{data[i],3} ");
        }

        this.Output.WriteLine(sb.ToString());
    }

    protected void Print(string msg)
    {
        Debug.WriteLine(msg);
        this.Output.WriteLine(msg);
    }

    internal void CompareBlocks(Block8x8 a, Block8x8 b, int tolerance) =>
        this.CompareBlocks(a.AsFloatBlock(), b.AsFloatBlock(), tolerance + 1e-5f);

    internal void CompareBlocks(Block8x8F a, Block8x8F b, float tolerance)
        => this.CompareBlocks(a.ToArray(), b.ToArray(), tolerance);

    internal void CompareBlocks(Span<float> a, Span<float> b, float tolerance)
    {
        ApproximateFloatComparer comparer = new ApproximateFloatComparer(tolerance);
        double totalDifference = 0.0;

        bool failed = false;

        for (int i = 0; i < Block8x8F.Size; i++)
        {
            float expected = a[i];
            float actual = b[i];
            totalDifference += Math.Abs(expected - actual);

            if (!comparer.Equals(expected, actual))
            {
                failed = true;
                this.Output.WriteLine($"Difference too large at index {i}");
            }
        }

        this.Output.WriteLine("TOTAL DIFF: " + totalDifference);
        Assert.False(failed);
    }

    internal static bool CompareBlocks(Block8x8 a, Block8x8 b, int tolerance, out int diff)
    {
        bool res = CompareBlocks(a.AsFloatBlock(), b.AsFloatBlock(), tolerance + 1e-5f, out float fdiff);
        diff = (int)fdiff;
        return res;
    }

    internal static bool CompareBlocks(Block8x8F a, Block8x8F b, float tolerance, out float diff) =>
        CompareBlocks(a.ToArray(), b.ToArray(), tolerance, out diff);

    internal static bool CompareBlocks(Span<float> a, Span<float> b, float tolerance, out float diff)
    {
        ApproximateFloatComparer comparer = new ApproximateFloatComparer(tolerance);
        bool failed = false;

        diff = 0;

        for (int i = 0; i < 64; i++)
        {
            float expected = a[i];
            float actual = b[i];
            diff += Math.Abs(expected - actual);

            if (!comparer.Equals(expected, actual))
            {
                failed = true;
            }
        }

        return !failed;
    }

    internal static JpegDecoderCore ParseJpegStream(string testFileName, bool metaDataOnly = false)
    {
        byte[] bytes = TestFile.Create(testFileName).Bytes;
        using MemoryStream ms = new(bytes);
        JpegDecoderOptions decoderOptions = new();
        Configuration configuration = decoderOptions.GeneralOptions.Configuration;
        JpegDecoderCore decoder = new(decoderOptions);
        if (metaDataOnly)
        {
            decoder.Identify(configuration, ms, default);
        }
        else
        {
            using Image<Rgba32> image = decoder.Decode<Rgba32>(configuration, ms, default);
        }

        return decoder;
    }
}
