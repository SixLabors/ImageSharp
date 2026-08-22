// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlDctQuantWeightParameters
{
    private const int Log2MaxDistanceBands = 4;
    private const int MaxDistanceBands = 1 + (1 << Log2MaxDistanceBands);

    private int numDistanceBands;
    private readonly float[][] distanceBands;

    public JxlDctQuantWeightParameters()
    {
        this.distanceBands = new float[3][];
        for (int i = 0; i < 3; i++)
        {
            this.distanceBands[i] = new float[MaxDistanceBands];
        }
    }

    public JxlDctQuantWeightParameters(float[][] distanceBands, int numDistanceBands)
    {
        this.numDistanceBands = numDistanceBands;
        this.distanceBands = distanceBands;
    }
}
