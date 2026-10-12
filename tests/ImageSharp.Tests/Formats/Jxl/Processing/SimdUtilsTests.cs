// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class SimdUtilsTests
{
    [Fact]
    public void TestInterleave2()
    {
        Span<float> mem = [0.0f, 0.0f];

        Vector<float> vec1 = JxlSimdUtils.Iota(0f);
        Vector<float> vec2 = JxlSimdUtils.Iota(128.0f);

        JxlSimdUtils.StoreInterleaved(vec1, vec2, ref MemoryMarshal.GetReference(mem));

        for (int j = 0; j < 2; j++)
        {
            Assert.Equal(mem[j], j * 128.0f);
        }
    }

    [Fact]
    public void TestInterleave4()
    {
        Span<float> mem = [0.0f, 0.0f, 0.0f, 0.0f];

        Vector<float> vec1 = JxlSimdUtils.Iota(0f);
        Vector<float> vec2 = JxlSimdUtils.Iota(128.0f);
        Vector<float> vec3 = JxlSimdUtils.Iota(256.0f);
        Vector<float> vec4 = JxlSimdUtils.Iota(384.0f);

        JxlSimdUtils.StoreInterleaved(vec1, vec2, vec3, vec4, ref MemoryMarshal.GetReference(mem));

        for (int j = 0; j < 4; j++)
        {
            Assert.Equal(mem[j], j * 128.0f);
        }
    }

    [Fact]
    public void TestInterleave8()
    {
        Span<float> mem = [0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f];

        Vector<float> vec1 = JxlSimdUtils.Iota(0f);
        Vector<float> vec2 = JxlSimdUtils.Iota(1 * 128.0f);
        Vector<float> vec3 = JxlSimdUtils.Iota(2 * 128.0f);
        Vector<float> vec4 = JxlSimdUtils.Iota(3 * 128.0f);
        Vector<float> vec5 = JxlSimdUtils.Iota(4 * 128.0f);
        Vector<float> vec6 = JxlSimdUtils.Iota(5 * 128.0f);
        Vector<float> vec7 = JxlSimdUtils.Iota(6 * 128.0f);
        Vector<float> vec8 = JxlSimdUtils.Iota(7 * 128.0f);

        JxlSimdUtils.StoreInterleaved(vec1, vec2, vec3, vec4, vec5, vec6, vec7, vec8, ref MemoryMarshal.GetReference(mem));

        for (int j = 0; j < 8; j++)
        {
            Assert.Equal(mem[j], j * 128.0f);
        }
    }
}
