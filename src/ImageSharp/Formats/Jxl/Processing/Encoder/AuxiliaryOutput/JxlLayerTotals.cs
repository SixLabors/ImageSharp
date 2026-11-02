// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;

internal struct JxlLayerTotals
{
    public int NumberOfClusteredHistograms;
    public int ExtraBits;
    public int HistogramBits;
    public int TotalBits;
    public double ClusteredEntropy;

    public void Assimilate(in JxlLayerTotals victim)
    {
        this.NumberOfClusteredHistograms += victim.NumberOfClusteredHistograms;
        this.HistogramBits += victim.HistogramBits;
        this.ExtraBits += victim.ExtraBits;
        this.TotalBits += victim.TotalBits;
        this.ClusteredEntropy += victim.ClusteredEntropy;
    }
}
