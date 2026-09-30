// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;

/// <summary>
/// Provides statistics gathered during compression or decompression.
/// </summary>
internal sealed class JxlAuxiliaryOutput
{
    public JxlLayerTotals[] Layers { get; set; } = new JxlLayerTotals[JxlAuxiliaryOutputConstants.NumberOfImageLayers];

    public long NumberOfBlocks { get; set; }

    public long NumberOfSmallBlocks { get; set; }

    public long NumberOfDct4x8Blocks { get; set; }

    public long NumberOfAfvBlocks { get; set; }

    public long NumberOfDct8Blocks { get; set; }

    public long NumberOfDct8x16Blocks { get; set; }

    public long NumberOfDct8x32Blocks { get; set; }

    public long NumberOfDct16Blocks { get; set; }

    public long NumberOfDct16x32Blocks { get; set; }

    public long NumberOfDct32Blocks { get; set; }

    public long NumberOfDct32x64Blocks { get; set; }

    public long NumberOfDct64Blocks { get; set; }

    public long NumberOfButteraugliIterations { get; set; }

    public long TotalBits => this.Layers.Sum(x => x.TotalBits);

    public static string GetLayerName(JxlLayerType layer) => layer switch
    {
        JxlLayerType.Header => "Headers",
        JxlLayerType.Toc => "TOC",
        JxlLayerType.Dictionary => "Patches",
        JxlLayerType.Splines => "Splines",
        JxlLayerType.Noise => "Noise",
        JxlLayerType.Quant => "Quantizer",
        JxlLayerType.ModularTree => "ModularTree",
        JxlLayerType.ModularGlobal => "ModularGlobal",
        JxlLayerType.Dc => "DC",
        JxlLayerType.ModularDcGroup => "ModularDcGroup",
        JxlLayerType.ControlFields => "ControlFields",
        JxlLayerType.Order => "CoeffOrder",
        JxlLayerType.Ac => "ACHistograms",
        JxlLayerType.AcTokens => "ACTokens",
        JxlLayerType.ModularAcGroup => "ModularAcGroup",
        _ => "Invalid",
    };

    public void Assimilate(JxlAuxiliaryOutput victim)
    {
        for (int i = 0; i < JxlAuxiliaryOutputConstants.NumberOfImageLayers; i++)
        {
            this.Layers[i].Assimilate(victim.Layers[i]);
        }

        this.NumberOfBlocks += victim.NumberOfBlocks;
        this.NumberOfSmallBlocks += victim.NumberOfSmallBlocks;
        this.NumberOfDct4x8Blocks += victim.NumberOfDct4x8Blocks;
        this.NumberOfAfvBlocks += victim.NumberOfAfvBlocks;
        this.NumberOfDct8Blocks += victim.NumberOfDct8Blocks;
        this.NumberOfDct8x16Blocks += victim.NumberOfDct8x16Blocks;
        this.NumberOfDct8x32Blocks += victim.NumberOfDct8x32Blocks;
        this.NumberOfDct16Blocks += victim.NumberOfDct16Blocks;
        this.NumberOfDct16x32Blocks += victim.NumberOfDct16x32Blocks;
        this.NumberOfDct32Blocks += victim.NumberOfDct32Blocks;
        this.NumberOfDct32x64Blocks += victim.NumberOfDct32x64Blocks;
        this.NumberOfDct64Blocks += victim.NumberOfDct64Blocks;
        this.NumberOfButteraugliIterations += victim.NumberOfButteraugliIterations;
    }
}
