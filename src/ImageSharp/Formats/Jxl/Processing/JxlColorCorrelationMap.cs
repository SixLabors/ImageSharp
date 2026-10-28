// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// JPEG XL Color correlation map.
/// </summary>
internal sealed class JxlColorCorrelationMap
{
    public JxlColorCorrelation Base { get; set; } = new();

    public JxlPlane<sbyte>? YToXMap { get; set; }

    public JxlPlane<sbyte>? YToBMap { get; set; }

    public bool DecodeDc(JxlBitReader reader) => this.Base.DecodeDc(reader);

    public static JxlColorCorrelationMap Create(Configuration configuration, int width, int height, bool xyb = true)
    {
        JxlColorCorrelationMap map = new();

        (int xBlocks, int yBlocks) = (JxlMath.DivCeil(width, JxlChromaFromLuma.ColorTileDimension), JxlMath.DivCeil(height, JxlChromaFromLuma.ColorTileDimension));

        map.YToXMap = new JxlPlane<sbyte>(configuration, xBlocks, yBlocks);
        map.YToBMap = new JxlPlane<sbyte>(configuration, xBlocks, yBlocks);

        map.YToXMap.Clear();
        map.YToBMap.Clear();

        if (!xyb)
        {
            map.Base.BaseCorrelationB = 0;
        }

        map.Base.RecomputeDcFactors();
        return map;
    }
}
