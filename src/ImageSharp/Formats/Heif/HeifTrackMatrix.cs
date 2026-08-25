// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Contains a fixed-point movie or track transformation matrix from a HEIF image sequence.
/// </summary>
internal readonly struct HeifTrackMatrix
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifTrackMatrix"/> struct.
    /// </summary>
    /// <param name="a">The horizontal scale and rotation coefficient in 16.16 fixed-point form.</param>
    /// <param name="b">The horizontal skew and rotation coefficient in 16.16 fixed-point form.</param>
    /// <param name="u">The first perspective coefficient in 2.30 fixed-point form.</param>
    /// <param name="c">The vertical skew and rotation coefficient in 16.16 fixed-point form.</param>
    /// <param name="d">The vertical scale and rotation coefficient in 16.16 fixed-point form.</param>
    /// <param name="v">The second perspective coefficient in 2.30 fixed-point form.</param>
    /// <param name="x">The horizontal translation in 16.16 fixed-point form.</param>
    /// <param name="y">The vertical translation in 16.16 fixed-point form.</param>
    /// <param name="w">The homogeneous scale coefficient in 2.30 fixed-point form.</param>
    public HeifTrackMatrix(int a, int b, int u, int c, int d, int v, int x, int y, int w)
    {
        this.A = a;
        this.B = b;
        this.U = u;
        this.C = c;
        this.D = d;
        this.V = v;
        this.X = x;
        this.Y = y;
        this.W = w;
    }

    /// <summary>
    /// Gets the horizontal scale and rotation coefficient in 16.16 fixed-point form.
    /// </summary>
    public int A { get; }

    /// <summary>
    /// Gets the horizontal skew and rotation coefficient in 16.16 fixed-point form.
    /// </summary>
    public int B { get; }

    /// <summary>
    /// Gets the first perspective coefficient in 2.30 fixed-point form.
    /// </summary>
    public int U { get; }

    /// <summary>
    /// Gets the vertical skew and rotation coefficient in 16.16 fixed-point form.
    /// </summary>
    public int C { get; }

    /// <summary>
    /// Gets the vertical scale and rotation coefficient in 16.16 fixed-point form.
    /// </summary>
    public int D { get; }

    /// <summary>
    /// Gets the second perspective coefficient in 2.30 fixed-point form.
    /// </summary>
    public int V { get; }

    /// <summary>
    /// Gets the horizontal translation in 16.16 fixed-point form.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical translation in 16.16 fixed-point form.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the homogeneous scale coefficient in 2.30 fixed-point form.
    /// </summary>
    public int W { get; }

    /// <summary>
    /// Gets a value indicating whether the matrix leaves the image coordinate system unchanged.
    /// </summary>
    public bool IsIdentity => this.A == 0x00010000 && this.B == 0 && this.U == 0 && this.C == 0 && this.D == 0x00010000 &&
        this.V == 0 && this.X == 0 && this.Y == 0 && this.W == 0x40000000;

    /// <summary>
    /// Reads the nine fixed-point coefficients from a matrix payload in file byte order.
    /// </summary>
    /// <param name="data">The 36-byte matrix payload.</param>
    /// <returns>The decoded transformation matrix.</returns>
    public static HeifTrackMatrix Parse(ReadOnlySpan<byte> data)
        => new(
            BinaryPrimitives.ReadInt32BigEndian(data),
            BinaryPrimitives.ReadInt32BigEndian(data[4..]),
            BinaryPrimitives.ReadInt32BigEndian(data[8..]),
            BinaryPrimitives.ReadInt32BigEndian(data[12..]),
            BinaryPrimitives.ReadInt32BigEndian(data[16..]),
            BinaryPrimitives.ReadInt32BigEndian(data[20..]),
            BinaryPrimitives.ReadInt32BigEndian(data[24..]),
            BinaryPrimitives.ReadInt32BigEndian(data[28..]),
            BinaryPrimitives.ReadInt32BigEndian(data[32..]));
}
