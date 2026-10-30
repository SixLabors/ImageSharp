// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// JPEG XL Color correlation map.
/// </summary>
internal sealed class JxlColorCorrelationMap : IDisposable
{
    /// <summary>
    /// Allocates both sample planes before publishing a usable correlation map.
    /// </summary>
    /// <param name="configuration">The configuration that owns plane storage.</param>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="xyb">Whether to use the XYB model's default B correlation.</param>
    private JxlColorCorrelationMap(Configuration configuration, int width, int height, bool xyb)
    {
        int xBlocks = JxlMath.DivCeil(width, JxlChromaFromLuma.ColorTileDimension);
        int yBlocks = JxlMath.DivCeil(height, JxlChromaFromLuma.ColorTileDimension);
        this.YToXMap = new(configuration, xBlocks, yBlocks);

        try
        {
            this.YToBMap = new(configuration, xBlocks, yBlocks);
        }
        catch
        {
            // Construction has not published the map. Release the first allocation if the second cannot be acquired.
            this.YToXMap.Dispose();
            throw;
        }

        this.Reset(xyb);
    }

    /// <summary>
    /// Gets the scalar correlation factors used with the map samples.
    /// </summary>
    public JxlColorCorrelation Base { get; } = new();

    /// <summary>
    /// Gets the owned Y-to-X sample plane. Callers borrow the plane without taking ownership.
    /// </summary>
    public JxlPlane<sbyte> YToXMap { get; }

    /// <summary>
    /// Gets the owned Y-to-B sample plane. Callers borrow the plane without taking ownership.
    /// </summary>
    public JxlPlane<sbyte> YToBMap { get; }

    /// <summary>
    /// Reads the frame's scalar DC color-correlation factors.
    /// </summary>
    /// <param name="reader">The encoded factor fields.</param>
    /// <returns>Whether the factors decoded successfully.</returns>
    public bool DecodeDc(JxlBitReader reader) => this.Base.DecodeDc(reader);

    /// <summary>
    /// Allocates zeroed correlation planes for the supplied frame extent.
    /// </summary>
    /// <param name="configuration">The configuration that owns plane storage.</param>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="xyb">Whether the map uses the XYB model's default B correlation.</param>
    /// <returns>The map that owns both sample planes.</returns>
    public static JxlColorCorrelationMap Create(Configuration configuration, int width, int height, bool xyb = true)
        => new(configuration, width, height, xyb);

    /// <summary>
    /// Changes the logical tile extent when it fits within this map's original plane allocations.
    /// </summary>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <returns>Whether the dimensions fit without replacing storage.</returns>
    public bool ShrinkTo(int width, int height)
    {
        int tileWidth = JxlMath.DivCeil(width, JxlChromaFromLuma.ColorTileDimension);
        int tileHeight = JxlMath.DivCeil(height, JxlChromaFromLuma.ColorTileDimension);
        if (!this.YToXMap.ShrinkTo(tileWidth, tileHeight))
        {
            return false;
        }

        // Both planes are allocated together at the same extent. Resizing the first proves capacity for the second.
        this.YToBMap.ShrinkTo(tileWidth, tileHeight);
        return true;
    }

    /// <summary>
    /// Clears the logical map samples and restores scalar defaults before another frame uses this storage.
    /// </summary>
    /// <param name="xyb">Whether to use the XYB model's default B correlation.</param>
    public void Reset(bool xyb)
    {
        this.YToXMap.Clear();
        this.YToBMap.Clear();
        this.Base.Reset();
        if (!xyb)
        {
            this.Base.BaseCorrelationB = 0;
        }

        this.Base.RecomputeDcFactors();
    }

    /// <summary>
    /// Releases the owned color-correlation planes.
    /// </summary>
    public void Dispose()
    {
        this.YToXMap.Dispose();
        this.YToBMap.Dispose();
    }
}
