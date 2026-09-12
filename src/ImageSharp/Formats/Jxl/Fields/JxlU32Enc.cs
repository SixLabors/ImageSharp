// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

internal readonly struct JxlU32Enc
{
    private readonly InlineArray4<JxlU32Distribution> d = default;

    public JxlU32Enc(JxlU32Distribution d0, JxlU32Distribution d1, JxlU32Distribution d2, JxlU32Distribution d3)
    {
        this.d[0] = d0;
        this.d[1] = d1;
        this.d[2] = d2;
        this.d[3] = d3;
    }

    public JxlU32Distribution GetDistribution(int selector)
    {
        // This stuff is internal, so if argument check
        // fails it's not a user error.
        DebugGuard.MustBeLessThan(selector, 4, nameof(selector));

        return this.d[selector];
    }
}
