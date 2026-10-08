// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// The packed DC-sign and coefficient-level edges of the three planes of one tile, read once by the caller that
/// writes the coefficients of a block.
/// </summary>
internal readonly ref struct Av1CoefficientNeighborEdges
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CoefficientNeighborEdges"/> struct.
    /// </summary>
    /// <param name="picture">The picture that owns the neighbor arrays.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    public Av1CoefficientNeighborEdges(Av1PictureControlSet picture, int tileIndex)
    {
        this.Luma = picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
        this.Blue = picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
        this.Red = picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
    }

    /// <summary>
    /// Gets the luma edges.
    /// </summary>
    public Av1NeighborEdges<byte> Luma { get; }

    /// <summary>
    /// Gets the blue-difference chroma edges.
    /// </summary>
    public Av1NeighborEdges<byte> Blue { get; }

    /// <summary>
    /// Gets the red-difference chroma edges.
    /// </summary>
    public Av1NeighborEdges<byte> Red { get; }

    /// <summary>
    /// Gets the edges of one plane.
    /// </summary>
    /// <param name="plane">The plane.</param>
    /// <returns>The edges of the plane.</returns>
    public Av1NeighborEdges<byte> Get(Av1Plane plane) => plane switch
    {
        Av1Plane.Y => this.Luma,
        Av1Plane.U => this.Blue,
        _ => this.Red
    };
}
