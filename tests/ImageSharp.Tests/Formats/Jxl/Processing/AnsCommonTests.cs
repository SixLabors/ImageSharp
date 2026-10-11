// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class AnsCommonTests
{
    private static void VerifyAliasDistribution(Span<int> distribution, uint logRange)
    {
        const int logAlphaSize = 8;

        Span<JxlAnsEntry> table = stackalloc JxlAnsEntry[1 << logAlphaSize];
        bool success = JxlAnsHelper.InitAliasTable(distribution, logRange, logAlphaSize, table);
        Assert.True(success);

        uint range = 1u << (int)logRange;
        List<int>[] offsets = new List<int>[distribution.Length];

        for (int i = 0; i < range; i++)
        {
            JxlAnsSymbol s = JxlAnsHelper.Lookup(table, i, JxlAnsConstants.AnsLogTableSize - 8, (1 << (JxlAnsConstants.AnsLogTableSize - 8)) - 1);

            offsets[s.Value] ??= [];
            offsets[s.Value].Add(s.Offset);
        }

        for (int i = 0; i < distribution.Length; i++)
        {
            Assert.Equal(distribution[i], offsets[i].Count);
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
    }
}
