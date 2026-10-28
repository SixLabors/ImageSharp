// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

/// <summary>
/// Owns the transform selection and first-block flag for each block of 8 by 8 pixels.
/// </summary>
internal sealed class JxlAcStrategyImage : IDisposable
{
    /// <summary>
    /// Marks a block that has no assigned transform.
    /// </summary>
    private const byte Invalid = byte.MaxValue;

    /// <summary>
    /// The owned transform-selection plane.
    /// </summary>
    private readonly JxlPlane<byte> layers;

    /// <summary>
    /// The borrowed storage used to address multi-block transforms by stride.
    /// </summary>
    private readonly Memory<byte> row;

    /// <summary>
    /// The physical row stride in blocks.
    /// </summary>
    private readonly int stride;

    /// <summary>
    /// Initializes an allocated transform-selection plane.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="xSize">The width in blocks of 8 by 8 pixels.</param>
    /// <param name="ySize">The height in blocks of 8 by 8 pixels.</param>
    private JxlAcStrategyImage(Configuration configuration, int xSize, int ySize)
    {
        this.layers = new(configuration, xSize, ySize);
        this.row = this.layers.GetRowMemory(0);
        this.stride = this.layers.PixelsPerRow;
    }

    /// <summary>
    /// Gets the logical width in blocks of 8 by 8 pixels.
    /// </summary>
    public int XSize => this.layers.XSize;

    /// <summary>
    /// Gets the logical height in blocks of 8 by 8 pixels.
    /// </summary>
    public int YSize => this.layers.YSize;

    /// <summary>
    /// Gets the physical row stride in blocks.
    /// </summary>
    public int PixelsPerRow => this.stride;

    /// <summary>
    /// Borrows a row of transform selections.
    /// </summary>
    /// <param name="y">The row index in blocks.</param>
    /// <param name="xPrefix">The starting column in blocks.</param>
    /// <returns>The selections from the starting column to the logical row end.</returns>
    public JxlAcStrategyRow GetRow(int y, int xPrefix = 0)
    {
        ReadOnlySpan<byte> layerRow = this.layers.GetRow(y)[..this.XSize];
        return new JxlAcStrategyRow(layerRow[xPrefix..]);
    }

    /// <summary>
    /// Creates an allocated transform-selection plane.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="xSize">The width in blocks of 8 by 8 pixels.</param>
    /// <param name="ySize">The height in blocks of 8 by 8 pixels.</param>
    /// <returns>The owner of the transform selections.</returns>
    public static JxlAcStrategyImage Create(Configuration configuration, int xSize, int ySize) => new(configuration, xSize, ySize);

    /// <summary>
    /// Counts the first blocks of transforms with the selected type.
    /// </summary>
    /// <param name="type">The transform type to count.</param>
    /// <returns>The number of transforms, excluding their continuation blocks.</returns>
    public int CountBlocks(JxlAcStrategyType type)
    {
        int value = 0;
        int compare = ((int)type << 1) | 1;

        for (int y = 0; y < this.layers.YSize; y++)
        {
            ReadOnlySpan<byte> row = this.layers.GetRow(y);

            for (int x = 0; x < this.layers.XSize; x++)
            {
                if (row[x] == compare)
                {
                    value++;
                }
            }
        }

        return value;
    }

    /// <summary>
    /// Borrows transform selections from a row within a rectangle.
    /// </summary>
    /// <param name="rect">The rectangle in blocks of 8 by 8 pixels.</param>
    /// <param name="y">The row index relative to the rectangle.</param>
    /// <returns>The selections from the rectangle's left edge.</returns>
    public JxlAcStrategyRow GetRow(in Rectangle rect, int y) => this.GetRow(rect.Y + y, rect.X);

    /// <summary>
    /// Checks whether a block has an assigned transform.
    /// </summary>
    /// <param name="x">The block column.</param>
    /// <param name="y">The block row.</param>
    /// <returns>Whether the block has an assigned transform.</returns>
    public bool IsValid(int x, int y) => this.row.Span[(y * this.stride) + x] != Invalid;

    /// <summary>
    /// Assigns a transform to blocks whose extent has already been validated.
    /// </summary>
    /// <param name="x">The first block column.</param>
    /// <param name="y">The first block row.</param>
    /// <param name="type">The transform type.</param>
    /// <param name="check">Whether to reject an overlapping transform.</param>
    /// <returns>Whether the assignment succeeded.</returns>
    public bool SetNoBoundsChecks(int x, int y, JxlAcStrategyType type, bool check = true)
    {
        JxlAcStrategy strategy = new(type);
        Span<byte> rowSpan = this.row.Span;
        int rawType = (int)type;
        int rawTypeTimes2 = rawType << 1;

        // Every covered block stores the transform type in the upper bits. Only the top-left
        // block sets bit zero, so readers can distinguish a transform from its continuation blocks.
        for (int iy = 0; iy < strategy.CoveredBlocksY; iy++)
        {
            for (int ix = 0; ix < strategy.CoveredBlocksX; ix++)
            {
                int pos = ((y + iy) * this.stride) + x + ix;

                if (check && rowSpan[pos] != Invalid)
                {
                    throw new InvalidOperationException("Invalid AC strategy. Blocks overlap.");
                }

                rowSpan[pos] = (byte)(rawTypeTimes2 | ((iy | ix) == 0 ? 1 : 0));
            }
        }

        return true;
    }

    /// <summary>
    /// Assigns a transform that fits within the logical plane.
    /// </summary>
    /// <param name="x">The first block column.</param>
    /// <param name="y">The first block row.</param>
    /// <param name="type">The transform type.</param>
    /// <returns>Whether the assignment succeeded.</returns>
    public bool Set(int x, int y, JxlAcStrategyType type)
    {
        JxlAcStrategy strategy = new(type);

        if (y + strategy.CoveredBlocksY > this.layers.YSize ||
            x + strategy.CoveredBlocksX > this.layers.XSize)
        {
            throw new InvalidOperationException("Invalid range");
        }

        return this.SetNoBoundsChecks(x, y, type, check: false);
    }

    /// <summary>
    /// Assigns independent eight-by-eight transforms within a rectangle.
    /// </summary>
    /// <param name="rect">The rectangle in blocks of 8 by 8 pixels.</param>
    public void FillDct8(in Rectangle rect) => JxlImageOperations.FillPlane((byte)(((int)JxlAcStrategyType.DCT << 1) | 1), this.layers, rect);

    /// <summary>
    /// Assigns independent eight-by-eight transforms to every logical block.
    /// </summary>
    public void FillDct8() => this.FillDct8(this.layers.GetRectangle());

    /// <summary>
    /// Marks every logical block as unassigned.
    /// </summary>
    public void FillInvalid() => JxlImageOperations.FillImage(Invalid, this.layers);

    /// <inheritdoc />
    public void Dispose()
    {
        this.layers.Dispose();
        GC.SuppressFinalize(this);
    }
}
