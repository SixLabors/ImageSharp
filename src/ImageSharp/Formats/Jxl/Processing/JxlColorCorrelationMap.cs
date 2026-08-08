// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// JPEG XL Color correlation map.
/// </summary>
internal sealed class JxlColorCorrelationMap
{
    public JxlColorCorrelation Base { get; set; } = new();

    public JxlImageSB? YToXMap { get; set; }

    public JxlImageSB? YToBMap { get; set; }

    public bool DecodeDc(JxlBitReader reader) => this.Base.DecodeDc(reader);

    public static JxlColorCorrelationMap Create(Configuration configuration, int width, int height, bool xyb = true)
    {
        JxlColorCorrelationMap map = new();

        (int xBlocks, int yBlocks) = (DivCeil(width, JxlChromaFromLuma.ColorTileDimension), DivCeil(height, JxlChromaFromLuma.ColorTileDimension));

        map.YToXMap = new JxlImageSB(configuration, xBlocks, yBlocks);
        map.YToBMap = new JxlImageSB(configuration, xBlocks, yBlocks);

        ZeroFillImage(map.YToXMap);
        ZeroFillImage(map.YToBMap);

        if (!xyb)
        {
            map.Base.BaseCorrelationB = 0;
        }

        map.Base.RecomputeDcFactors();
        return map;
    }
}
