// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Converts current-frame prediction coordinates into a retained reference frame's sample grid.
/// </summary>
internal readonly struct Av1ReferenceScale
{
    /// <summary>
    /// The identity scale in the normative Q14 representation.
    /// </summary>
    private const int IdentityScale = 1 << 14;

    /// <summary>
    /// The number of fractional bits carried by scaled prediction positions and steps.
    /// </summary>
    public const int SubpixelBits = 10;

    /// <summary>
    /// The mask selecting one scaled sample's fractional position.
    /// </summary>
    public const int SubpixelMask = (1 << SubpixelBits) - 1;

    /// <summary>
    /// The half-unit offset that centers Q4 input coordinates on the Q10 reference grid.
    /// </summary>
    public const int ExtraOffset = 1 << (SubpixelBits - 4 - 1);

    /// <summary>
    /// The border, in luma samples, to which a scaled position clamps on the top and left. Reference:
    /// AOM_BORDER_IN_PIXELS in AOM_LEFT_TOP_MARGIN_SCALED().
    /// </summary>
    public const int ClampBorder = 288;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ReferenceScale"/> struct.
    /// </summary>
    /// <param name="referenceWidth">The retained reference width.</param>
    /// <param name="referenceHeight">The retained reference height.</param>
    /// <param name="currentWidth">The current coded-frame width.</param>
    /// <param name="currentHeight">The current coded-frame height.</param>
    public Av1ReferenceScale(int referenceWidth, int referenceHeight, int currentWidth, int currentHeight)
    {
        this.HorizontalScale = ((referenceWidth << 14) + (currentWidth >> 1)) / currentWidth;
        this.VerticalScale = ((referenceHeight << 14) + (currentHeight >> 1)) / currentHeight;
        this.HorizontalStep = (this.HorizontalScale + 8) >> 4;
        this.VerticalStep = (this.VerticalScale + 8) >> 4;
    }

    /// <summary>
    /// Gets the horizontal Q14 scale factor.
    /// </summary>
    public int HorizontalScale { get; }

    /// <summary>
    /// Gets the vertical Q14 scale factor.
    /// </summary>
    public int VerticalScale { get; }

    /// <summary>
    /// Gets the horizontal per-output-sample step in Q10 reference samples.
    /// </summary>
    public int HorizontalStep { get; }

    /// <summary>
    /// Gets the vertical per-output-sample step in Q10 reference samples.
    /// </summary>
    public int VerticalStep { get; }

    /// <summary>
    /// Gets a value indicating whether either reference dimension differs from the current frame.
    /// </summary>
    public bool IsScaled => this.HorizontalScale != IdentityScale || this.VerticalScale != IdentityScale;

    /// <summary>
    /// Maps a position of the current plane into the reference plane, centered on the reference grid, and clamps it
    /// to the scaled border on the top and left and to the interpolation extension past the visible reference on the
    /// bottom and right. Reference: init_subpel_params() with av1_scaled_x(), av1_scaled_y() and
    /// AOM_LEFT_TOP_MARGIN_SCALED().
    /// </summary>
    /// <param name="columnQ4">The column in sixteenth samples of the current plane.</param>
    /// <param name="rowQ4">The row in sixteenth samples of the current plane.</param>
    /// <param name="planeWidth">The visible width of the reference plane.</param>
    /// <param name="planeHeight">The visible height of the reference plane.</param>
    /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
    /// <returns>The reference position in 1/1024 samples.</returns>
    public Point ScalePosition(int columnQ4, int rowQ4, int planeWidth, int planeHeight, int subsamplingX, int subsamplingY)
    {
        // The extension is AOM_INTERP_EXTEND.
        const int extension = 4;
        int column = Math.Clamp(
            this.ScaleHorizontal(columnQ4) + ExtraOffset,
            -(((ClampBorder >> subsamplingX) - extension) << SubpixelBits),
            (planeWidth + extension) << SubpixelBits);

        int row = Math.Clamp(
            this.ScaleVertical(rowQ4) + ExtraOffset,
            -(((ClampBorder >> subsamplingY) - extension) << SubpixelBits),
            (planeHeight + extension) << SubpixelBits);

        return new Point(column, row);
    }

    /// <summary>
    /// Scales one horizontal Q4 current-frame coordinate into the Q10 reference grid.
    /// </summary>
    public int ScaleHorizontal(int value) => Scale(value, this.HorizontalScale);

    /// <summary>
    /// Scales one vertical Q4 current-frame coordinate into the Q10 reference grid.
    /// </summary>
    public int ScaleVertical(int value) => Scale(value, this.VerticalScale);

    /// <summary>
    /// Applies the reference decoder's signed fixed-point rounding without relying on implementation-defined negative shifts.
    /// </summary>
    private static int Scale(int value, int scale)
    {
        long offset = (scale - IdentityScale) * 8L;
        long scaled = ((long)value * scale) + offset;
        const int shift = 8;
        const long rounding = 1L << (shift - 1);
        return scaled < 0
            ? (int)-((-scaled + rounding) >> shift)
            : (int)((scaled + rounding) >> shift);
    }
}
