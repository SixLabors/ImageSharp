// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class EntropyCoderTests
{
    [Fact]
    public void PackUnpack()
    {
        for (int i = -31; i < 32; i++)
        {
            uint packed = JxlPackSigned.PackUnsigned(i);

            Assert.True(packed < 63u);

            int unpacked = JxlPackSigned.UnpackSigned(packed);

            Assert.Equal(i, unpacked);
        }
    }

    private sealed class MockBitReader : JxlBitReader
    {
        public ulong NBits { get; set; }

        public ulong Bits { get; set; }

        public MockBitReader(Stream stream)
            : base(stream)
        {
        }

        public override uint PeekBits32(uint n)
        {
            Assert.Equal(this.NBits, n);
            return (uint)this.Bits;
        }

        public override uint ReadBits32(uint n)
        {
            Assert.Equal(this.NBits, n);
            return (uint)this.Bits;
        }
    }

    private static void HybridUintRoundtrip(
        JxlAnsHybridUIntConfiguration config,
        uint limit = 1u << 24)
    {
        Rng rng = new(0);

        const int numIntegers = 1 << 20;

        uint[] integers = new uint[numIntegers];
        uint[] token = new uint[numIntegers];
        uint[] nbits = new uint[numIntegers];
        uint[] bits = new uint[numIntegers];

        for (int i = 0; i < numIntegers; i++)
        {
            integers[i] = (uint)rng.UniformU(0, limit + 1);
            config.Encode(
                integers[i],
                out token[i],
                out nbits[i],
                out bits[i]);
        }

        for (int i = 0; i < numIntegers; i++)
        {
            MockBitReader br = new(Stream.Null)
            {
                NBits = nbits[i],
                Bits = bits[i]
            };

            Assert.Equal(
                integers[i],
                JxlAnsSymbolReader.ReadHybridUintConfig(
                    config,
                    token[i],
                    ref br));
        }
    }

    [Fact]
    public void Test000() => HybridUintRoundtrip(new JxlAnsHybridUIntConfiguration(0, 0, 0));

    [Fact]
    public void Test411() => HybridUintRoundtrip(new JxlAnsHybridUIntConfiguration(4, 1, 1));

    [Fact]
    public void Test420() => HybridUintRoundtrip(new JxlAnsHybridUIntConfiguration(4, 2, 0));

    [Fact]
    public void Test421() => HybridUintRoundtrip(
            new JxlAnsHybridUIntConfiguration(4, 2, 1),
            256);
}
