// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

internal sealed class JxlAcStrategyImage : IDisposable
{
    private const byte Invalid = byte.MaxValue; // 255

    private JxlImageB? layers;
    private Memory<byte> row;
    private int stride;

    public int XSize => this.layers!.XSize;

    public int YSize => this.layers!.YSize;

    public int PixelsPerRow => this.layers!.PixelsPerRow;

    public JxlAcStrategyRow GetRow(int y, int xPrefix = 0)
    {
        ReadOnlyMemory<byte> layerRow = this.layers!.GetRowBytesMemory(y);
        ReadOnlyMemory<byte> row = layerRow[xPrefix..];

        return new JxlAcStrategyRow(row);
    }

    public static JxlAcStrategyImage Create(Configuration memoryManager, int xSize, int ySize)
    {
        JxlAcStrategyImage image = new()
        {
            layers = new JxlImageB(memoryManager, xSize, ySize)
        };

        image.row = image.layers.GetRowBytesMemory(0);
        image.stride = image.layers.PixelsPerRow;

        return image;
    }

    public int CountBlocks(JxlAcStrategyType type)
    {
        int value = 0;
        int compare = ((int)type << 1) | 1;

        for (int y = 0; y < this.layers!.YSize; y++)
        {
            ReadOnlySpan<byte> row = this.layers!.GetRow(y);

            for (int x = 0; x < this.layers!.XSize; x++)
            {
                if (row[x] == compare)
                {
                    value++;
                }
            }
        }

        return value;
    }

    public JxlAcStrategyRow GetRow(in Rectangle rect, int y) => this.GetRow(rect.Y + y, rect.X);

    public bool IsValid(int x, int y) => this.row.Span[(y * this.stride) + x] != Invalid;

    public bool SetNoBoundsChecks(int x, int y, JxlAcStrategyType type, bool check = true)
    {
        JxlAcStrategy strategy = new(type);
        Span<byte> rowSpan = this.row.Span;
        int rawType = (int)type;
        int rawTypeTimes2 = rawType << 1;

        for (int iy = 0; iy < strategy.CoveredBlocksX; iy++)
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

    public bool Set(int x, int y, JxlAcStrategyType type)
    {
        JxlAcStrategy strategy = new(type);

        if (y + strategy.CoveredBlocksX > this.layers!.YSize ||
            x + strategy.CoveredBlocksX > this.layers.XSize)
        {
            throw new InvalidOperationException("Invalid range");
        }

        return this.SetNoBoundsChecks(x, y, type, check: false);
    }

    public void FillDct8(in Rectangle rect) => JxlImageOperations.FillPlane((byte)(((int)JxlAcStrategyType.DCT << 1) | 1), this.layers ?? throw new InvalidOperationException("Image is missing"), rect);

    public void FillDct8() => this.FillDct8(this.layers!.GetRectangle());

    public void FillInvalid() => JxlImageOperations.FillImage(Invalid, this.layers ?? throw new InvalidOperationException("Image is missing"));

    public void Dispose()
    {
        this.layers?.Dispose();
        GC.SuppressFinalize(this);
    }
}
