// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class AnsCommonTests
{
    /// <summary>
    /// Checks hybrid-integer expansion against fixed tokens and bit payloads.
    /// </summary>
    /// <param name="mostSignificantBits">The number of high value bits carried by the token.</param>
    /// <param name="leastSignificantBits">The number of low value bits carried by the token.</param>
    /// <param name="expected">The decoded UInt32 value.</param>
    [Theory]
    [InlineData(2, 0, 37)]
    [InlineData(1, 1, 42)]
    public void ReadHybridUInt_UsesSplitExponent(int mostSignificantBits, int leastSignificantBits, int expected)
    {
        JxlAnsHybridUIntConfiguration configuration = new(4, (uint)mostSignificantBits, (uint)leastSignificantBits);

        // Token 20 selects three payload bits in both configurations. The fixed payload is binary 101.
        using MemoryStream input = new([0x05]);
        JxlBitReader reader = new(input);

        Assert.Equal(expected, JxlAnsSymbolReader.ReadHybridUIntConfiguration(configuration, 20, reader));
        Assert.Equal(3, reader.TotalBitsConsumed);
    }

    /// <summary>
    /// Checks the terminal state against the ANS signature rather than the frequency-table size.
    /// </summary>
    /// <param name="signature">The high word of the fixed little-endian ANS state.</param>
    /// <param name="expected">Whether the state is terminal.</param>
    [Theory]
    [InlineData(0x13, true)]
    [InlineData(0x10, false)]
    public void CheckAnsFinalState_UsesFormatSignature(byte signature, bool expected)
    {
        // Ten header bits select no LZ77, ANS, a 32-entry table, split exponent zero, and symbol zero.
        // The following 32 LSB-first bits hold the terminal state. Its high word begins at bit 26.
        using MemoryStream input = new([0x80, 0, 0, (byte)(signature << 2), 0, 0]);
        JxlBitReader bitReader = new(input);
        JxlAnsCode code = new();
        List<byte> contextMap = [];
        JxlAnsReader.DecodeHistograms(Configuration.Default, bitReader, 1, code, ref contextMap, false);

        using JxlAnsSymbolReader reader = JxlAnsSymbolReader.Create(Configuration.Default, code, bitReader, 0);

        Assert.Equal(expected, reader.CheckAnsFinalState());
    }

    /// <summary>
    /// Checks that alias lookup visits every symbol offset exactly once at its declared frequency.
    /// </summary>
    /// <param name="distribution">The independently specified frequencies.</param>
    /// <param name="logRange">The exponent of their sum.</param>
    private static void VerifyAliasDistribution(ReadOnlySpan<uint> distribution, uint logRange)
    {
        const int logAlphaSize = 8;

        Span<JxlAnsEntry> table = stackalloc JxlAnsEntry[1 << logAlphaSize];
        bool success = JxlAnsHelper.InitAliasTable(distribution, logRange, logAlphaSize, table);
        Assert.True(success);

        uint range = 1u << (int)logRange;
        List<int>[] offsets = new List<int>[distribution.Length];

        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = new List<int>((int)distribution[i]);
        }

        for (int i = 0; i < range; i++)
        {
            JxlAnsSymbol s = JxlAnsHelper.Lookup(table, i, JxlAnsConstants.AnsLogTableSize - 8, (1 << (JxlAnsConstants.AnsLogTableSize - 8)) - 1);

            Assert.Equal((int)distribution[s.Value], s.Frequency);
            offsets[s.Value].Add(s.Offset);
        }

        for (int i = 0; i < distribution.Length; i++)
        {
            Assert.Equal((int)distribution[i], offsets[i].Count);
            offsets[i].Sort();

            for (int j = 0; j < offsets[i].Count; j++)
            {
                Assert.Equal(offsets[i][j], j);
            }
        }
    }

    [Fact]
    public void AliasDistributionSmoke()
    {
        VerifyAliasDistribution([JxlAnsConstants.AnsTableSize / 2, JxlAnsConstants.AnsTableSize / 2], JxlAnsConstants.AnsLogTableSize);
        VerifyAliasDistribution([JxlAnsConstants.AnsTableSize], JxlAnsConstants.AnsLogTableSize);
        VerifyAliasDistribution([0, 0, 0, JxlAnsConstants.AnsTableSize, 0], JxlAnsConstants.AnsLogTableSize);
        VerifyAliasDistribution([2048, 1024, 1024], JxlAnsConstants.AnsLogTableSize);
        VerifyAliasDistribution([0, 2048, 0, 1024, 1024], JxlAnsConstants.AnsLogTableSize);
    }

    /// <summary>
    /// Checks that a sparse single-symbol histogram cannot retain dirty pooled counts in its unused entries.
    /// </summary>
    [Fact]
    public void ReadHistogram_ClearsUnspecifiedCounts()
    {
        Configuration configuration = new() { MemoryAllocator = new TestMemoryAllocator() };

        // Simple histogram, one symbol, symbol index 3. Each unused count must be zero, not allocator contents.
        using MemoryStream input = new([0x4D]);
        using IMemoryOwner<uint> counts = JxlAnsReader.ReadHistogram(configuration, JxlAnsConstants.AnsLogTableSize, new JxlBitReader(input));
        ReadOnlySpan<uint> expected = [0, 0, 0, JxlAnsConstants.AnsTableSize];

        Assert.True(counts.GetSpan().SequenceEqual(expected));
    }

    /// <summary>
    /// Checks the valid one-symbol flat histogram against fixed wire data.
    /// </summary>
    [Fact]
    public void ReadHistogram_AcceptsFlatAlphabet()
    {
        // Not simple, flat, alphabet size one. The alphabet needs no explicit frequency bits.
        using MemoryStream input = new([0x02]);
        using IMemoryOwner<uint> counts = JxlAnsReader.ReadHistogram(Configuration.Default, JxlAnsConstants.AnsLogTableSize, new JxlBitReader(input));

        Assert.Equal(1, counts.GetSpan().Length);
        Assert.Equal((uint)JxlAnsConstants.AnsTableSize, counts.GetSpan()[0]);
    }
}
