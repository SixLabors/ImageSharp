// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

internal sealed class JxlDctQuantWeightParameters
{
    private const int Log2MaxDistanceBands = 4;
    private const int MaxDistanceBands = 1 + (1 << Log2MaxDistanceBands);

    public JxlDctQuantWeightParameters()
    {
        this.DistanceBands = new float[3][];
        for (int i = 0; i < 3; i++)
        {
            this.DistanceBands[i] = new float[MaxDistanceBands];
        }
    }

    public JxlDctQuantWeightParameters(float[][] distanceBands, int numDistanceBands)
    {
        this.NumDistanceBands = numDistanceBands;
        this.DistanceBands = distanceBands;
    }

    public int NumDistanceBands { get; set; }

    public float[][] DistanceBands { get; }
}
