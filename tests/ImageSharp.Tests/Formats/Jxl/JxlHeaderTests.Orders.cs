// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public partial class JxlHeaderTests
{
    /// <summary>
    /// Verifies native component permutations, retained low-frequency prefixes and consumption of unused encoded families.
    /// </summary>
    /// <param name="usedStrategies">The strategy mask used by the independent native decoder.</param>
    /// <param name="retainLargeFamily">Whether the sixteen-by-sixteen family must also be retained.</param>
    [Theory]
    [InlineData(4096U, false)]
    [InlineData(4112U, true)]
    public void ReadCoefficientOrdersMatchesNative(uint usedStrategies, bool retainLargeFamily)
    {
        // Obtain a real frame owner through the stored Square DC-global fields. The separate order fixture starts
        // with a raw codestream signature, followed only by the independently encoded coefficient-order syntax.
        using MemoryStream frameInput = new(TestFile.Create(TestImages.Jxl.Square).Bytes, false);
        using BufferedReadStream frameBuffered = new(Configuration.Default, frameInput);
        JxlBitReader frameReader = new(new JxlCodestreamReader(frameBuffered));
        JxlHeader image = JxlHeader.Read(ref frameReader, Configuration.Default.MemoryAllocator, SegmentIntegrityHandling.Strict, CancellationToken.None);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref frameReader, image, false, SegmentIntegrityHandling.Strict);
        using JxlFrameSections sections = JxlFrameSections.Read(ref frameReader, Configuration.Default.MemoryAllocator, frame.SectionCount, default);
        frameReader.BeginSection(sections.Sizes.Span[0]);
        Vector3 dcFactors = JxlDecoderCore.ReadDcQuantization(ref frameReader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref frameReader, image, frame, Configuration.Default.MemoryAllocator, dcFactors, []);
        using MemoryStream orderInput = new(TestFile.Create(TestImages.Jxl.VarDctCustomOrders).Bytes, false);
        using BufferedReadStream orderBuffered = new(Configuration.Default, orderInput);
        JxlBitReader reader = new(new JxlCodestreamReader(orderBuffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        long start = reader.Position;
        int[] offsets = new int[JxlCoefficientOrder.CoefficientOrderCount];
        using IMemoryOwner<uint> owner = varDct.ReadCoefficientOrders(ref reader, Configuration.Default.MemoryAllocator, usedStrategies, offsets, default);
        ReadOnlySpan<uint> orders = owner.Memory.Span;

        // Original libjxl and jxl-rs both consume all 72 payload bits for either mask. Family two is still present
        // in the stream when only strategy twelve (family one) is retained; skipping its entropy would stop early.
        Assert.Equal(start + 72, reader.Position);
        ReadOnlySpan<uint> natural8 =
        [
            0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
            12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63
        ];

        // The native encoder swaps consecutive entries after the fixed LF prefix, with a different position per component.
        // For family one the prefix contains one coefficient. Preserve every other native natural-order entry exactly.
        for (int component = 0; component < 3; component++)
        {
            uint[] expected = natural8.ToArray();
            (expected[1 + component], expected[2 + component]) = (expected[2 + component], expected[1 + component]);
            int index = JxlCoefficientOrder.GetCoefficientOrderIndex(12, component);
            Assert.Equal((ReadOnlySpan<uint>)expected, orders.Slice(offsets[index], expected.Length));
        }

        ReadOnlySpan<string> largeHashes =
        [
            "911D5CCE9B992850AFC62D12F677B709A23DE448D7CB167FAEEE9E669180BFCD",
            "543FFD7698378644503284FBD585C57E1168502DC3F15218F6FB784B10EBC316",
            "67AF275F5AE5B67A71E21E039DE263BD7B84E7AA7CA4FFFA667CB090A1943B63"
        ];

        ReadOnlySpan<uint> largePrefix = [0, 1, 16, 17, 32, 2, 3, 18];
        for (int component = 0; component < 3; component++)
        {
            int index = JxlCoefficientOrder.GetCoefficientOrderIndex(4, component);
            if (retainLargeFamily)
            {
                ReadOnlySpan<uint> actual = orders.Slice(offsets[index], JxlCoefficientOrder.GetCoefficientOrderSize(4));

                // Native sixteen-by-sixteen orders have four fixed LF entries, not one. Hashes cover each complete
                // 256-entry array returned by both references; explicit prefix and swap checks state their meaning.
                Assert.Equal(largePrefix[..4], actual[..4]);
                Assert.Equal(largePrefix[5 + component], actual[4 + component]);
                Assert.Equal(largePrefix[4 + component], actual[5 + component]);
                AssertCoefficientOrderHash(actual, largeHashes[component]);
            }
            else
            {
                Assert.Equal(-1, offsets[index]);
            }
        }
    }

    /// <summary>
    /// Verifies that transposed rectangular strategies use the same independently recorded natural coefficient order.
    /// </summary>
    /// <param name="strategy">The raw eight-by-sixteen or sixteen-by-eight transform strategy.</param>
    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    public void NaturalRectangularCoefficientOrderMatchesNative(byte strategy)
    {
        // This 128-entry order was generated by libjxl ComputeNaturalCoeffOrder and is also stored in jxl-rs tests.
        // Rectangular traversal preserves both LF entries first; the remaining frequencies follow the normalized rectangle.
        ReadOnlySpan<uint> expected =
        [
            0, 1, 16, 2, 3, 17, 32, 18, 4, 5, 19, 33, 48, 34, 20, 6,
            7, 21, 35, 49, 64, 50, 36, 22, 8, 9, 23, 37, 51, 65, 80, 66,
            52, 38, 24, 10, 11, 25, 39, 53, 67, 81, 96, 82, 68, 54, 40, 26,
            12, 13, 27, 41, 55, 69, 83, 97, 112, 98, 84, 70, 56, 42, 28, 14,
            15, 29, 43, 57, 71, 85, 99, 113, 114, 100, 86, 72, 58, 44, 30, 31,
            45, 59, 73, 87, 101, 115, 116, 102, 88, 74, 60, 46, 47, 61, 75, 89,
            103, 117, 118, 104, 90, 76, 62, 63, 77, 91, 105, 119, 120, 106, 92, 78,
            79, 93, 107, 121, 122, 108, 94, 95, 109, 123, 124, 110, 111, 125, 126, 127
        ];

        uint[] actual = new uint[expected.Length + 2];
        actual.AsSpan().Fill(uint.MaxValue);
        JxlCoefficientOrder.WriteNaturalCoefficientOrder(strategy, actual.AsSpan(1, expected.Length), default);

        Assert.Equal(expected.Length, JxlCoefficientOrder.GetCoefficientOrderSize(strategy));
        Assert.Equal(expected, actual.AsSpan(1, expected.Length));
        Assert.Equal(uint.MaxValue, actual[0]);
        Assert.Equal(uint.MaxValue, actual[^1]);
    }

    /// <summary>
    /// Compares a complete native coefficient-order digest with explicit little-endian integer serialization.
    /// </summary>
    /// <param name="order">The complete reconstructed order.</param>
    /// <param name="expected">The SHA-256 digest of the independently decoded native array.</param>
    private static void AssertCoefficientOrderHash(ReadOnlySpan<uint> order, string expected)
    {
        byte[] bytes = new byte[order.Length * sizeof(uint)];
        for (int i = 0; i < order.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint), sizeof(uint)), order[i]);
        }

        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
