// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Collection of all information for a single frame.
/// </summary>
internal partial class Av1FrameInfo
{
    // Number of Coefficients in a single ModeInfo 4x4 block of pixels (1 length + 4 x 4).
    public const int CoefficientCountPerModeInfo = 1 + 16;

    private readonly int[] coefficientsY = [];
    private readonly int[] coefficientsU = [];
    private readonly int[] coefficientsV = [];
    private readonly int modeInfoSizePerSuperblock;
    private readonly int modeInfoCountPerSuperblock;
    private readonly int superblockColumnCount;
    private readonly int superblockRowCount;
    private readonly int subsamplingFactor;
    private readonly Av1SuperblockInfo[] superblockInfos;
    private readonly Av1BlockModeInfo[] modeInfos;
    private readonly Av1FrameModeInfoMap modeInfoMap;
    private readonly Av1TransformInfo[] transformInfosY;
    private readonly Av1TransformInfo[] transformInfosUv;
    private readonly int[] deltaQ;
    private readonly int cdefStrengthFactorLog2;
    private readonly int[] cdefStrength;
    private readonly int deltaLoopFactorLog2 = 2;
    private readonly int[] deltaLoopFilter;

    public Av1FrameInfo(ObuSequenceHeader sequenceHeader)
    {
        // init_main_frame_ctxt
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockAlignedWidth = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, superblockSizeLog2);
        int superblockAlignedHeight = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, superblockSizeLog2);
        this.superblockColumnCount = superblockAlignedWidth >> superblockSizeLog2;
        this.superblockRowCount = superblockAlignedHeight >> superblockSizeLog2;
        int superblockCount = this.superblockColumnCount * this.superblockRowCount;
        this.modeInfoSizePerSuperblock = 1 << (superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);
        this.modeInfoCountPerSuperblock = this.modeInfoSizePerSuperblock * this.modeInfoSizePerSuperblock;
        int numPlanes = sequenceHeader.ColorConfig.IsMonochrome ? 1 : Av1Constants.MaxPlanes;

        // Allocate the arrays.
        this.superblockInfos = new Av1SuperblockInfo[superblockCount];
        this.modeInfos = new Av1BlockModeInfo[superblockCount * this.modeInfoCountPerSuperblock];
        this.modeInfoMap = new Av1FrameModeInfoMap(new Size(this.modeInfoSizePerSuperblock * this.superblockColumnCount, this.modeInfoSizePerSuperblock * this.superblockRowCount), superblockSizeLog2);
        this.transformInfosY = new Av1TransformInfo[superblockCount * this.modeInfoCountPerSuperblock];
        this.transformInfosUv = new Av1TransformInfo[2 * superblockCount * this.modeInfoCountPerSuperblock];

        // Initialize the arrays.
        int i = 0;
        for (int y = 0; y < this.superblockRowCount; y++)
        {
            for (int x = 0; x < this.superblockColumnCount; x++)
            {
                Point point = new(x, y);
                this.superblockInfos[i] = new(this, point);
                i++;
            }
        }

        bool subX = sequenceHeader.ColorConfig.SubSamplingX;
        bool subY = sequenceHeader.ColorConfig.SubSamplingY;

        // Factor: 444 => 0, 422 => 1, 420 => 2.
        this.subsamplingFactor = (subX && subY) ? 2 : (subX && !subY) ? 1 : (!subX && !subY) ? 0 : -1;
        Guard.IsFalse(this.subsamplingFactor == -1, nameof(this.subsamplingFactor), "Invalid combination of subsampling.");
        this.coefficientsY = new int[superblockCount * this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo];
        this.coefficientsU = new int[(this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo) >> this.subsamplingFactor];
        this.coefficientsV = new int[(this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo) >> this.subsamplingFactor];
        this.deltaQ = new int[superblockCount];

        // Superblock size: 128x128 has sizelog2 = 7, 64x64 = 6. Factor should be 128x128 => 4 and 64x64 => 1.
        this.cdefStrengthFactorLog2 = (superblockSizeLog2 - 6) << 2;
        this.cdefStrength = new int[superblockCount << this.cdefStrengthFactorLog2];
        Array.Fill(this.cdefStrength, -1);
        this.deltaLoopFilter = new int[superblockCount << this.deltaLoopFactorLog2];
    }

    /// <summary>
    /// Gets the number of mode info blocks in a single superblock.
    /// </summary>
    public int ModeInfoCount => this.modeInfos.Length;

    /// <summary>
    /// Gets the Width or height of a single superblock, counted in mode info blocks.
    /// </summary>
    public int SuperblockModeInfoSize => this.modeInfoSizePerSuperblock;

    public Av1SuperblockInfo GetSuperblock(Point index)
    {
        Span<Av1SuperblockInfo> span = this.superblockInfos;
        int i = (index.Y * this.superblockColumnCount) + index.X;
        return span[i];
    }

    public Av1BlockModeInfo GetModeInfo(Point superblockIndex)
    {
        Span<Av1BlockModeInfo> span = this.modeInfos;
        int superblock = (superblockIndex.Y * this.superblockColumnCount) + superblockIndex.X;
        return span[superblock * this.modeInfoCountPerSuperblock];
    }

    public Av1BlockModeInfo GetModeInfo(Point superblockIndex, Point modeInfoIndex)
    {
        Point location = this.GetModeInfoPosition(superblockIndex, modeInfoIndex);
        int index = this.modeInfoMap[location];
        return this.modeInfos[index];
    }

    public Span<Av1TransformInfo> GetSuperblockTransform(int plane, Point index)
    {
        if (plane == 0)
        {
            return this.GetSuperblockTransformY(index);
        }

        return this.GetSuperblockTransformUv(index);
    }

    public Span<Av1TransformInfo> GetSuperblockTransformY(Point index)
    {
        Span<Av1TransformInfo> span = this.transformInfosY;
        int offset = ((index.Y * this.superblockColumnCount) + index.X) * this.modeInfoCountPerSuperblock;
        return span.Slice(offset, this.modeInfoCountPerSuperblock);
    }

    public Span<Av1TransformInfo> GetSuperblockTransformUv(Point index)
    {
        Span<Av1TransformInfo> span = this.transformInfosUv;
        int offset = (((index.Y * this.superblockColumnCount) + index.X) * this.modeInfoCountPerSuperblock) << 1;
        return span.Slice(offset, this.modeInfoCountPerSuperblock);
    }

    public Span<int> GetCoefficients(int plane) =>
        plane switch
        {
            0 => (Span<int>)this.coefficientsY,
            1 => (Span<int>)this.coefficientsY,
            2 => (Span<int>)this.coefficientsY,
            _ => null,
        };

    public Span<int> GetCoefficientsY(Point index)
    {
        Span<int> span = this.coefficientsY;
        int i = ((index.Y * this.modeInfoCountPerSuperblock) + index.X) * CoefficientCountPerModeInfo;
        return span.Slice(i, CoefficientCountPerModeInfo);
    }

    public Span<int> GetCoefficientsU(Point index)
    {
        Span<int> span = this.coefficientsU;
        int i = ((index.Y * this.modeInfoCountPerSuperblock) + index.X) * CoefficientCountPerModeInfo;
        return span.Slice(i >> this.subsamplingFactor, CoefficientCountPerModeInfo);
    }

    public Span<int> GetCoefficientsV(Point index)
    {
        Span<int> span = this.coefficientsV;
        int i = ((index.Y * this.modeInfoCountPerSuperblock) + index.X) * CoefficientCountPerModeInfo;
        return span.Slice(i >> this.subsamplingFactor, CoefficientCountPerModeInfo);
    }

    public ref int GetDeltaQuantizationIndex(Point index)
    {
        Span<int> span = this.deltaQ;
        int i = (index.Y * this.superblockColumnCount) + index.X;
        return ref span[i];
    }

    public Span<int> GetCdefStrength(Point index)
    {
        Span<int> span = this.cdefStrength;
        int i = ((index.Y * this.superblockColumnCount) + index.X) << this.cdefStrengthFactorLog2;
        return span.Slice(i, 1 << this.cdefStrengthFactorLog2);
    }

    internal void ClearCdef(Point index)
    {
        Span<int> cdefs = this.GetCdefStrength(index);
        for (int i = 0; i < cdefs.Length; i++)
        {
            cdefs[i] = -1;
        }
    }

    public Span<int> GetDeltaLoopFilter(Point index)
    {
        Span<int> span = this.deltaLoopFilter;
        int i = ((index.Y * this.superblockColumnCount) + index.X) << this.deltaLoopFactorLog2;
        return span.Slice(i, 1 << this.deltaLoopFactorLog2);
    }

    public void ClearDeltaLoopFilter() => Array.Fill(this.deltaLoopFilter, 0);

    public void UpdateModeInfo(Av1BlockModeInfo modeInfo, Av1SuperblockInfo superblockInfo)
    {
        this.modeInfos[this.modeInfoMap.NextIndex] = modeInfo;
        this.modeInfoMap.Update(this.GetModeInfoPosition(superblockInfo.Position, modeInfo.PositionInSuperblock), modeInfo.BlockSize);
    }

    private Point GetModeInfoPosition(Point superblockPosition, Point positionInSuperblock)
    {
        int x = (superblockPosition.X * this.modeInfoCountPerSuperblock) + positionInSuperblock.X;
        int y = (superblockPosition.Y * this.modeInfoCountPerSuperblock) + positionInSuperblock.Y;
        return new Point(x, y);
    }
}
