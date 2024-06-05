// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1FrameBuffer
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
    private readonly Av1TransformInfo[] transformInfosY;
    private readonly Av1TransformInfo[] transformInfosUv;
    private readonly int[] deltaQ;
    private readonly int cdefStrengthFactorLog2;
    private readonly int[] cdefStrength;
    private readonly int deltaLoopFactorLog2 = 2;
    private readonly int[] deltaLoopFilter;

    public Av1FrameBuffer(ObuSequenceHeader sequenceHeader)
    {
        // init_main_frame_ctxt
        int superblockSizeLog2 = sequenceHeader.SuperBlockSizeLog2;
        int superblockAlignedWidth = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, superblockSizeLog2);
        int superblockAlignedHeight = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, superblockSizeLog2);
        this.superblockColumnCount = superblockAlignedWidth >> superblockSizeLog2;
        this.superblockRowCount = superblockAlignedHeight >> superblockSizeLog2;
        int superblockCount = this.superblockColumnCount * this.superblockRowCount;
        this.modeInfoSizePerSuperblock = 1 << (superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);
        this.modeInfoCountPerSuperblock = this.modeInfoSizePerSuperblock * this.modeInfoSizePerSuperblock;
        this.superblockInfos = new Av1SuperblockInfo[superblockCount];
        this.modeInfos = new Av1BlockModeInfo[superblockCount * this.modeInfoCountPerSuperblock];
        this.transformInfosY = new Av1TransformInfo[superblockCount * this.modeInfoCountPerSuperblock];
        this.transformInfosUv = new Av1TransformInfo[2 * superblockCount * this.modeInfoCountPerSuperblock];
        this.coefficientsY = new int[superblockCount * this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo];
        bool subX = sequenceHeader.ColorConfig.SubSamplingX;
        bool subY = sequenceHeader.ColorConfig.SubSamplingY;

        // Factor: 444 => 0, 422 => 1, 420 => 2.
        this.subsamplingFactor = (subX && subY) ? 2 : (subX && !subY) ? 1 : (!subX && !subY) ? 0 : -1;
        Guard.IsFalse(this.subsamplingFactor == -1, nameof(this.subsamplingFactor), "Invalid combination of subsampling.");
        this.coefficientsU = new int[(this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo) >> this.subsamplingFactor];
        this.coefficientsV = new int[(this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo) >> this.subsamplingFactor];
        this.deltaQ = new int[superblockCount];

        // Superblock size: 128x128 has sizelog2 = 7, 64x64 = 6. Factor should be 128x128 => 4 and 64x64 => 1.
        this.cdefStrengthFactorLog2 = (superblockSizeLog2 - 6) << 2;
        this.cdefStrength = new int[superblockCount << this.cdefStrengthFactorLog2];
        Array.Fill(this.cdefStrength, -1);
        this.deltaLoopFilter = new int[superblockCount << this.deltaLoopFactorLog2];
    }

    public Av1SuperblockInfo GetSuperblock(Point index)
    {
        Span<Av1SuperblockInfo> span = this.superblockInfos;
        int i = (index.Y * this.superblockColumnCount) + index.X;
        return span[i];
    }

    public Av1BlockModeInfo GetModeInfo(Point superblockIndex, Point modeInfoIndex)
    {
        Span<Av1BlockModeInfo> span = this.modeInfos;
        int baseIndex = ((superblockIndex.Y * this.superblockColumnCount) + superblockIndex.X) * this.modeInfoCountPerSuperblock;
        int offset = (modeInfoIndex.Y * this.modeInfoSizePerSuperblock) + modeInfoIndex.X;
        return span[baseIndex + offset];
    }

    public Av1TransformInfo GetTransformY(Point index)
    {
        Span<Av1TransformInfo> span = this.transformInfosY;
        int i = (index.Y * this.superblockColumnCount) + index.X;
        return span[i * this.modeInfoCountPerSuperblock];
    }

    public void GetTransformUv(Point index, out Av1TransformInfo transformU, out Av1TransformInfo transformV)
    {
        Span<Av1TransformInfo> span = this.transformInfosUv;
        int i = 2 * ((index.Y * this.superblockColumnCount) + index.X) * this.modeInfoCountPerSuperblock;
        transformU = span[i];
        transformV = span[i + 1];
    }

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

    public Span<int> GetDeltaLoopFilter(Point index)
    {
        Span<int> span = this.deltaLoopFilter;
        int i = ((index.Y * this.superblockColumnCount) + index.X) << this.deltaLoopFactorLog2;
        return span.Slice(i, 1 << this.deltaLoopFactorLog2);
    }

    internal void ClearDeltaLoopFilter() => Array.Fill(this.deltaLoopFilter, 0);
}
