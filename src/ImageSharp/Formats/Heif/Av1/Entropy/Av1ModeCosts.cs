// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Provides the mode rates retained between encoder cost updates.
/// </summary>
internal readonly ref struct Av1ModeCosts
{
    private const int ChromaFromLumaSignCount = 8;
    private const int ChromaFromLumaPlaneCount = 2;
    private const int ChromaFromLumaAlphabetSize = 16;
    private const int ChromaFromLumaLength = ChromaFromLumaSignCount * ChromaFromLumaPlaneCount * ChromaFromLumaAlphabetSize;

    private const int PartitionTypesAlphabetSize = 10;
    private const int PartitionTypesRowCount = 20;
    private const int PartitionTypesLength = PartitionTypesRowCount * PartitionTypesAlphabetSize;
    private const int PartitionTypesOffset = 0;

    private const int FrameYModeAlphabetSize = 13;
    private const int FrameYModeRowCount = 4;
    private const int FrameYModeLength = FrameYModeRowCount * FrameYModeAlphabetSize;
    private const int FrameYModeOffset = PartitionTypesOffset + PartitionTypesLength;

    private const int KeyFrameYModeAlphabetSize = 13;
    private const int KeyFrameYModeRowCount = 13;
    private const int KeyFrameYModePlaneCount = 13;
    private const int KeyFrameYModeLength = KeyFrameYModePlaneCount * KeyFrameYModeRowCount * KeyFrameYModeAlphabetSize;
    private const int KeyFrameYModeOffset = FrameYModeOffset + FrameYModeLength;

    private const int UvModeAlphabetSize = 14;
    private const int UvModeRowCount = 13;
    private const int UvModePlaneCount = 2;
    private const int UvModeLength = UvModePlaneCount * UvModeRowCount * UvModeAlphabetSize;
    private const int UvModeOffset = KeyFrameYModeOffset + KeyFrameYModeLength;

    private const int FilterIntraAlphabetSize = 2;
    private const int FilterIntraRowCount = 22;
    private const int FilterIntraLength = FilterIntraRowCount * FilterIntraAlphabetSize;
    private const int FilterIntraOffset = UvModeOffset + UvModeLength;

    private const int FilterIntraModeAlphabetSize = 5;
    private const int FilterIntraModeLength = FilterIntraModeAlphabetSize;
    private const int FilterIntraModeOffset = FilterIntraOffset + FilterIntraLength;

    private const int AngleDeltaAlphabetSize = 7;
    private const int AngleDeltaRowCount = 8;
    private const int AngleDeltaLength = AngleDeltaRowCount * AngleDeltaAlphabetSize;
    private const int AngleDeltaOffset = FilterIntraModeOffset + FilterIntraModeLength;

    private const int IntraBlockCopyAlphabetSize = 2;
    private const int IntraBlockCopyLength = IntraBlockCopyAlphabetSize;
    private const int IntraBlockCopyOffset = AngleDeltaOffset + AngleDeltaLength;

    private const int PaletteYSizeAlphabetSize = 7;
    private const int PaletteYSizeRowCount = 7;
    private const int PaletteYSizeLength = PaletteYSizeRowCount * PaletteYSizeAlphabetSize;
    private const int PaletteYSizeOffset = IntraBlockCopyOffset + IntraBlockCopyLength;

    private const int PaletteUvSizeAlphabetSize = 7;
    private const int PaletteUvSizeRowCount = 7;
    private const int PaletteUvSizeLength = PaletteUvSizeRowCount * PaletteUvSizeAlphabetSize;
    private const int PaletteUvSizeOffset = PaletteYSizeOffset + PaletteYSizeLength;

    private const int PaletteYColorIndexAlphabetSize = 8;
    private const int PaletteYColorIndexRowCount = 5;
    private const int PaletteYColorIndexPlaneCount = 7;
    private const int PaletteYColorIndexLength = PaletteYColorIndexPlaneCount * PaletteYColorIndexRowCount * PaletteYColorIndexAlphabetSize;
    private const int PaletteYColorIndexOffset = PaletteUvSizeOffset + PaletteUvSizeLength;

    private const int PaletteUvColorIndexAlphabetSize = 8;
    private const int PaletteUvColorIndexRowCount = 5;
    private const int PaletteUvColorIndexPlaneCount = 7;
    private const int PaletteUvColorIndexLength = PaletteUvColorIndexPlaneCount * PaletteUvColorIndexRowCount * PaletteUvColorIndexAlphabetSize;
    private const int PaletteUvColorIndexOffset = PaletteYColorIndexOffset + PaletteYColorIndexLength;

    private const int PaletteYModeAlphabetSize = 2;
    private const int PaletteYModeRowCount = 3;
    private const int PaletteYModePlaneCount = 7;
    private const int PaletteYModeLength = PaletteYModePlaneCount * PaletteYModeRowCount * PaletteYModeAlphabetSize;
    private const int PaletteYModeOffset = PaletteUvColorIndexOffset + PaletteUvColorIndexLength;

    private const int PaletteUvModeAlphabetSize = 2;
    private const int PaletteUvModeRowCount = 2;
    private const int PaletteUvModeLength = PaletteUvModeRowCount * PaletteUvModeAlphabetSize;
    private const int PaletteUvModeOffset = PaletteYModeOffset + PaletteYModeLength;

    private const int SkipModeAlphabetSize = 2;
    private const int SkipModeRowCount = 3;
    private const int SkipModeLength = SkipModeRowCount * SkipModeAlphabetSize;
    private const int SkipModeOffset = PaletteUvModeOffset + PaletteUvModeLength;

    private const int NewMvAlphabetSize = 2;
    private const int NewMvRowCount = 6;
    private const int NewMvLength = NewMvRowCount * NewMvAlphabetSize;
    private const int NewMvOffset = SkipModeOffset + SkipModeLength;

    private const int ZeroMvAlphabetSize = 2;
    private const int ZeroMvRowCount = 2;
    private const int ZeroMvLength = ZeroMvRowCount * ZeroMvAlphabetSize;
    private const int ZeroMvOffset = NewMvOffset + NewMvLength;

    private const int RefMvAlphabetSize = 2;
    private const int RefMvRowCount = 6;
    private const int RefMvLength = RefMvRowCount * RefMvAlphabetSize;
    private const int RefMvOffset = ZeroMvOffset + ZeroMvLength;

    private const int DrlAlphabetSize = 2;
    private const int DrlRowCount = 3;
    private const int DrlLength = DrlRowCount * DrlAlphabetSize;
    private const int DrlOffset = RefMvOffset + RefMvLength;

    private const int SingleReferenceAlphabetSize = 2;
    private const int SingleReferenceRowCount = 6;
    private const int SingleReferencePlaneCount = 3;
    private const int SingleReferenceLength = SingleReferencePlaneCount * SingleReferenceRowCount * SingleReferenceAlphabetSize;
    private const int SingleReferenceOffset = DrlOffset + DrlLength;

    private const int CompInterAlphabetSize = 2;
    private const int CompInterRowCount = 5;
    private const int CompInterLength = CompInterRowCount * CompInterAlphabetSize;
    private const int CompInterOffset = SingleReferenceOffset + SingleReferenceLength;

    private const int CompoundReferenceTypeAlphabetSize = 3;
    private const int CompoundReferenceTypeRowCount = 5;
    private const int CompoundReferenceTypeLength = CompoundReferenceTypeRowCount * CompoundReferenceTypeAlphabetSize;
    private const int CompoundReferenceTypeOffset = CompInterOffset + CompInterLength;

    private const int UnidirectionalCompoundReferenceAlphabetSize = 3;
    private const int UnidirectionalCompoundReferenceRowCount = 3;
    private const int UnidirectionalCompoundReferencePlaneCount = 3;
    private const int UnidirectionalCompoundReferenceLength =
        UnidirectionalCompoundReferencePlaneCount * UnidirectionalCompoundReferenceRowCount * UnidirectionalCompoundReferenceAlphabetSize;

    private const int UnidirectionalCompoundReferenceOffset = CompoundReferenceTypeOffset + CompoundReferenceTypeLength;

    private const int CompoundReferenceAlphabetSize = 2;
    private const int CompoundReferenceRowCount = 3;
    private const int CompoundReferencePlaneCount = 3;
    private const int CompoundReferenceLength = CompoundReferencePlaneCount * CompoundReferenceRowCount * CompoundReferenceAlphabetSize;
    private const int CompoundReferenceOffset = UnidirectionalCompoundReferenceOffset + UnidirectionalCompoundReferenceLength;

    private const int CompoundBackwardReferenceAlphabetSize = 2;
    private const int CompoundBackwardReferenceRowCount = 2;
    private const int CompoundBackwardReferencePlaneCount = 3;
    private const int CompoundBackwardReferenceLength =
        CompoundBackwardReferencePlaneCount * CompoundBackwardReferenceRowCount * CompoundBackwardReferenceAlphabetSize;

    private const int CompoundBackwardReferenceOffset = CompoundReferenceOffset + CompoundReferenceLength;

    private const int IntraInterAlphabetSize = 2;
    private const int IntraInterRowCount = 4;
    private const int IntraInterLength = IntraInterRowCount * IntraInterAlphabetSize;
    private const int IntraInterOffset = CompoundBackwardReferenceOffset + CompoundBackwardReferenceLength;

    private const int InterCompoundModeAlphabetSize = 8;
    private const int InterCompoundModeRowCount = 8;
    private const int InterCompoundModeLength = InterCompoundModeRowCount * InterCompoundModeAlphabetSize;
    private const int InterCompoundModeOffset = IntraInterOffset + IntraInterLength;

    private const int CompoundTypeAlphabetSize = 2;
    private const int CompoundTypeRowCount = 22;
    private const int CompoundTypeLength = CompoundTypeRowCount * CompoundTypeAlphabetSize;
    private const int CompoundTypeOffset = InterCompoundModeOffset + InterCompoundModeLength;

    private const int WedgeIndexAlphabetSize = 16;
    private const int WedgeIndexRowCount = 22;
    private const int WedgeIndexLength = WedgeIndexRowCount * WedgeIndexAlphabetSize;
    private const int WedgeIndexOffset = CompoundTypeOffset + CompoundTypeLength;

    private const int InterIntraAlphabetSize = 2;
    private const int InterIntraRowCount = 4;
    private const int InterIntraLength = InterIntraRowCount * InterIntraAlphabetSize;
    private const int InterIntraOffset = WedgeIndexOffset + WedgeIndexLength;

    private const int WedgeInterIntraAlphabetSize = 2;
    private const int WedgeInterIntraRowCount = 22;
    private const int WedgeInterIntraLength = WedgeInterIntraRowCount * WedgeInterIntraAlphabetSize;
    private const int WedgeInterIntraOffset = InterIntraOffset + InterIntraLength;

    private const int InterIntraModeAlphabetSize = 4;
    private const int InterIntraModeRowCount = 4;
    private const int InterIntraModeLength = InterIntraModeRowCount * InterIntraModeAlphabetSize;
    private const int InterIntraModeOffset = WedgeInterIntraOffset + WedgeInterIntraLength;

    private const int CompoundIndexAlphabetSize = 2;
    private const int CompoundIndexRowCount = 6;
    private const int CompoundIndexLength = CompoundIndexRowCount * CompoundIndexAlphabetSize;
    private const int CompoundIndexOffset = InterIntraModeOffset + InterIntraModeLength;

    private const int CompoundGroupIndexAlphabetSize = 2;
    private const int CompoundGroupIndexRowCount = 6;
    private const int CompoundGroupIndexLength = CompoundGroupIndexRowCount * CompoundGroupIndexAlphabetSize;
    private const int CompoundGroupIndexOffset = CompoundIndexOffset + CompoundIndexLength;

    private const int MotionModeAlphabetSize = 3;
    private const int MotionModeRowCount = 22;
    private const int MotionModeLength = MotionModeRowCount * MotionModeAlphabetSize;
    private const int MotionModeOffset = CompoundGroupIndexOffset + CompoundGroupIndexLength;

    private const int ObmcAlphabetSize = 2;
    private const int ObmcRowCount = 22;
    private const int ObmcLength = ObmcRowCount * ObmcAlphabetSize;
    private const int ObmcOffset = MotionModeOffset + MotionModeLength;

    private const int SwitchableInterpolationAlphabetSize = 3;
    private const int SwitchableInterpolationRowCount = 16;
    private const int SwitchableInterpolationLength = SwitchableInterpolationRowCount * SwitchableInterpolationAlphabetSize;
    private const int SwitchableInterpolationOffset = ObmcOffset + ObmcLength;

    private const int SkipAlphabetSize = 2;
    private const int SkipRowCount = 3;
    private const int SkipLength = SkipRowCount * SkipAlphabetSize;
    private const int SkipOffset = SwitchableInterpolationOffset + SwitchableInterpolationLength;

    private const int TransformSizeAlphabetSize = 5;
    private const int TransformSizeRowCount = 3;
    private const int TransformSizePlaneCount = 4;
    private const int TransformSizeLength = TransformSizePlaneCount * TransformSizeRowCount * TransformSizeAlphabetSize;
    private const int TransformSizeOffset = SkipOffset + SkipLength;

    private const int TransformPartitionAlphabetSize = 2;
    private const int TransformPartitionRowCount = 21;
    private const int TransformPartitionLength = TransformPartitionRowCount * TransformPartitionAlphabetSize;
    private const int TransformPartitionOffset = TransformSizeOffset + TransformSizeLength;

    private const int InterExtendedTransformAlphabetSize = 16;
    private const int InterExtendedTransformRowCount = 4;
    private const int InterExtendedTransformPlaneCount = 4;
    private const int InterExtendedTransformLength = InterExtendedTransformPlaneCount * InterExtendedTransformRowCount * InterExtendedTransformAlphabetSize;
    private const int InterExtendedTransformOffset = TransformPartitionOffset + TransformPartitionLength;

    private const int IntraExtendedTransformAlphabetSize = 16;
    private const int IntraExtendedTransformRowCount = 13;
    private const int IntraExtendedTransformPlaneCount = 4;
    private const int IntraExtendedTransformCubeCount = 3;
    private const int IntraExtendedTransformLength =
        IntraExtendedTransformCubeCount * IntraExtendedTransformPlaneCount * IntraExtendedTransformRowCount * IntraExtendedTransformAlphabetSize;

    private const int IntraExtendedTransformOffset = InterExtendedTransformOffset + InterExtendedTransformLength;

    private const int SwitchableRestorationAlphabetSize = 3;
    private const int SwitchableRestorationLength = SwitchableRestorationAlphabetSize;
    private const int SwitchableRestorationOffset = IntraExtendedTransformOffset + IntraExtendedTransformLength;

    private const int WienerRestorationAlphabetSize = 2;
    private const int WienerRestorationLength = WienerRestorationAlphabetSize;
    private const int WienerRestorationOffset = SwitchableRestorationOffset + SwitchableRestorationLength;

    private const int SgrProjectionRestorationAlphabetSize = 2;
    private const int SgrProjectionRestorationLength = SgrProjectionRestorationAlphabetSize;
    private const int SgrProjectionRestorationOffset = WienerRestorationOffset + WienerRestorationLength;

    private const int SegmentIdPredictedAlphabetSize = 2;
    private const int SegmentIdPredictedRowCount = 3;
    private const int SegmentIdPredictedLength = SegmentIdPredictedRowCount * SegmentIdPredictedAlphabetSize;
    private const int SegmentIdPredictedOffset = SgrProjectionRestorationOffset + SgrProjectionRestorationLength;

    private const int SegmentIdAlphabetSize = 8;
    private const int SegmentIdRowCount = 3;
    private const int SegmentIdLength = SegmentIdRowCount * SegmentIdAlphabetSize;
    private const int SegmentIdOffset = SegmentIdPredictedOffset + SegmentIdPredictedLength;

    private const int ChromaFromLumaOffset = SegmentIdOffset + SegmentIdLength;
    public const int StorageLength = ChromaFromLumaOffset + ChromaFromLumaLength;

    private readonly Span<int> storage;

    public Av1ModeCosts(Span<int> storage) => this.storage = storage;

    /// <summary>
    /// Gets the partition types rate entries.
    /// </summary>
    private Span<int> PartitionTypes => this.storage.Slice(PartitionTypesOffset, PartitionTypesLength);

    /// <summary>
    /// Gets the frame ymode rate entries.
    /// </summary>
    private Span<int> FrameYMode => this.storage.Slice(FrameYModeOffset, FrameYModeLength);

    /// <summary>
    /// Gets the key frame ymode rate entries.
    /// </summary>
    private Span<int> KeyFrameYMode => this.storage.Slice(KeyFrameYModeOffset, KeyFrameYModeLength);

    /// <summary>
    /// Gets the uv mode rate entries.
    /// </summary>
    private Span<int> UvMode => this.storage.Slice(UvModeOffset, UvModeLength);

    /// <summary>
    /// Gets the filter intra rate entries.
    /// </summary>
    private Span<int> FilterIntra => this.storage.Slice(FilterIntraOffset, FilterIntraLength);

    /// <summary>
    /// Gets the filter intra mode rate entries.
    /// </summary>
    private Span<int> FilterIntraMode => this.storage.Slice(FilterIntraModeOffset, FilterIntraModeLength);

    /// <summary>
    /// Gets the angle delta rate entries.
    /// </summary>
    private Span<int> AngleDelta => this.storage.Slice(AngleDeltaOffset, AngleDeltaLength);

    /// <summary>
    /// Gets the intra block copy rate entries.
    /// </summary>
    private Span<int> IntraBlockCopy => this.storage.Slice(IntraBlockCopyOffset, IntraBlockCopyLength);

    /// <summary>
    /// Gets the palette ysize rate entries.
    /// </summary>
    private Span<int> PaletteYSize => this.storage.Slice(PaletteYSizeOffset, PaletteYSizeLength);

    /// <summary>
    /// Gets the palette uv size rate entries.
    /// </summary>
    private Span<int> PaletteUvSize => this.storage.Slice(PaletteUvSizeOffset, PaletteUvSizeLength);

    /// <summary>
    /// Gets the palette ycolor index rate entries.
    /// </summary>
    private Span<int> PaletteYColorIndex => this.storage.Slice(PaletteYColorIndexOffset, PaletteYColorIndexLength);

    /// <summary>
    /// Gets the palette uv color index rate entries.
    /// </summary>
    private Span<int> PaletteUvColorIndex => this.storage.Slice(PaletteUvColorIndexOffset, PaletteUvColorIndexLength);

    /// <summary>
    /// Gets the palette ymode rate entries.
    /// </summary>
    private Span<int> PaletteYMode => this.storage.Slice(PaletteYModeOffset, PaletteYModeLength);

    /// <summary>
    /// Gets the palette uv mode rate entries.
    /// </summary>
    private Span<int> PaletteUvMode => this.storage.Slice(PaletteUvModeOffset, PaletteUvModeLength);

    /// <summary>
    /// Gets the skip mode rate entries.
    /// </summary>
    private Span<int> SkipMode => this.storage.Slice(SkipModeOffset, SkipModeLength);

    /// <summary>
    /// Gets the new mv rate entries.
    /// </summary>
    private Span<int> NewMv => this.storage.Slice(NewMvOffset, NewMvLength);

    /// <summary>
    /// Gets the zero mv rate entries.
    /// </summary>
    private Span<int> ZeroMv => this.storage.Slice(ZeroMvOffset, ZeroMvLength);

    /// <summary>
    /// Gets the ref mv rate entries.
    /// </summary>
    private Span<int> RefMv => this.storage.Slice(RefMvOffset, RefMvLength);

    /// <summary>
    /// Gets the drl rate entries.
    /// </summary>
    private Span<int> Drl => this.storage.Slice(DrlOffset, DrlLength);

    /// <summary>
    /// Gets the single reference rate entries.
    /// </summary>
    private Span<int> SingleReference => this.storage.Slice(SingleReferenceOffset, SingleReferenceLength);

    /// <summary>
    /// Gets the comp inter rate entries.
    /// </summary>
    private Span<int> CompInter => this.storage.Slice(CompInterOffset, CompInterLength);

    /// <summary>
    /// Gets the compound reference type rate entries.
    /// </summary>
    private Span<int> CompoundReferenceType => this.storage.Slice(CompoundReferenceTypeOffset, CompoundReferenceTypeLength);

    /// <summary>
    /// Gets the unidirectional compound reference rate entries.
    /// </summary>
    private Span<int> UnidirectionalCompoundReference => this.storage.Slice(UnidirectionalCompoundReferenceOffset, UnidirectionalCompoundReferenceLength);

    /// <summary>
    /// Gets the compound reference rate entries.
    /// </summary>
    private Span<int> CompoundReference => this.storage.Slice(CompoundReferenceOffset, CompoundReferenceLength);

    /// <summary>
    /// Gets the compound backward reference rate entries.
    /// </summary>
    private Span<int> CompoundBackwardReference => this.storage.Slice(CompoundBackwardReferenceOffset, CompoundBackwardReferenceLength);

    /// <summary>
    /// Gets the intra inter rate entries.
    /// </summary>
    private Span<int> IntraInter => this.storage.Slice(IntraInterOffset, IntraInterLength);

    /// <summary>
    /// Gets the inter compound mode rate entries.
    /// </summary>
    private Span<int> InterCompoundMode => this.storage.Slice(InterCompoundModeOffset, InterCompoundModeLength);

    /// <summary>
    /// Gets the compound type rate entries.
    /// </summary>
    private Span<int> CompoundType => this.storage.Slice(CompoundTypeOffset, CompoundTypeLength);

    /// <summary>
    /// Gets the wedge index rate entries.
    /// </summary>
    private Span<int> WedgeIndex => this.storage.Slice(WedgeIndexOffset, WedgeIndexLength);

    /// <summary>
    /// Gets the inter intra rate entries.
    /// </summary>
    private Span<int> InterIntra => this.storage.Slice(InterIntraOffset, InterIntraLength);

    /// <summary>
    /// Gets the wedge inter intra rate entries.
    /// </summary>
    private Span<int> WedgeInterIntra => this.storage.Slice(WedgeInterIntraOffset, WedgeInterIntraLength);

    /// <summary>
    /// Gets the inter intra mode rate entries.
    /// </summary>
    private Span<int> InterIntraMode => this.storage.Slice(InterIntraModeOffset, InterIntraModeLength);

    /// <summary>
    /// Gets the compound index rate entries.
    /// </summary>
    private Span<int> CompoundIndex => this.storage.Slice(CompoundIndexOffset, CompoundIndexLength);

    /// <summary>
    /// Gets the compound group index rate entries.
    /// </summary>
    private Span<int> CompoundGroupIndex => this.storage.Slice(CompoundGroupIndexOffset, CompoundGroupIndexLength);

    /// <summary>
    /// Gets the motion mode rate entries.
    /// </summary>
    private Span<int> MotionMode => this.storage.Slice(MotionModeOffset, MotionModeLength);

    /// <summary>
    /// Gets the obmc rate entries.
    /// </summary>
    private Span<int> Obmc => this.storage.Slice(ObmcOffset, ObmcLength);

    /// <summary>
    /// Gets the switchable interpolation rate entries.
    /// </summary>
    private Span<int> SwitchableInterpolation => this.storage.Slice(SwitchableInterpolationOffset, SwitchableInterpolationLength);

    /// <summary>
    /// Gets the skip rate entries.
    /// </summary>
    private Span<int> Skip => this.storage.Slice(SkipOffset, SkipLength);

    /// <summary>
    /// Gets the transform size rate entries.
    /// </summary>
    private Span<int> TransformSize => this.storage.Slice(TransformSizeOffset, TransformSizeLength);

    /// <summary>
    /// Gets the transform partition rate entries.
    /// </summary>
    private Span<int> TransformPartition => this.storage.Slice(TransformPartitionOffset, TransformPartitionLength);

    /// <summary>
    /// Gets the inter extended transform rate entries.
    /// </summary>
    private Span<int> InterExtendedTransform => this.storage.Slice(InterExtendedTransformOffset, InterExtendedTransformLength);

    /// <summary>
    /// Gets the intra extended transform rate entries.
    /// </summary>
    private Span<int> IntraExtendedTransform => this.storage.Slice(IntraExtendedTransformOffset, IntraExtendedTransformLength);

    /// <summary>
    /// Gets the switchable restoration rate entries.
    /// </summary>
    private Span<int> SwitchableRestoration => this.storage.Slice(SwitchableRestorationOffset, SwitchableRestorationLength);

    /// <summary>
    /// Gets the wiener restoration rate entries.
    /// </summary>
    private Span<int> WienerRestoration => this.storage.Slice(WienerRestorationOffset, WienerRestorationLength);

    /// <summary>
    /// Gets the sgr projection restoration rate entries.
    /// </summary>
    private Span<int> SgrProjectionRestoration => this.storage.Slice(SgrProjectionRestorationOffset, SgrProjectionRestorationLength);

    /// <summary>
    /// Gets the segment id predicted rate entries.
    /// </summary>
    private Span<int> SegmentIdPredicted => this.storage.Slice(SegmentIdPredictedOffset, SegmentIdPredictedLength);

    /// <summary>
    /// Gets the segment id rate entries.
    /// </summary>
    private Span<int> SegmentId => this.storage.Slice(SegmentIdOffset, SegmentIdLength);

    /// <summary>
    /// Gets the chroma from luma rate entries.
    /// </summary>
    private Span<int> ChromaFromLuma => this.storage.Slice(ChromaFromLumaOffset, ChromaFromLumaLength);

    /// <summary>
    /// Gets a retained partition types rate in 1/512-bit units.
    /// </summary>
    public int GetPartitionTypes(int row, int symbol)
        => this.PartitionTypes[(row * PartitionTypesAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained frame ymode rate in 1/512-bit units.
    /// </summary>
    public int GetFrameYMode(int row, int symbol)
        => this.FrameYMode[(row * FrameYModeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained key frame ymode rate in 1/512-bit units.
    /// </summary>
    public int GetKeyFrameYMode(int plane, int row, int symbol)
        => this.KeyFrameYMode[(((plane * KeyFrameYModeRowCount) + row) * KeyFrameYModeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained uv mode rate in 1/512-bit units.
    /// </summary>
    public int GetUvMode(int plane, int row, int symbol)
        => this.UvMode[(((plane * UvModeRowCount) + row) * UvModeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained filter intra rate in 1/512-bit units.
    /// </summary>
    public int GetFilterIntra(int row, int symbol)
        => this.FilterIntra[(row * FilterIntraAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained filter intra mode rate in 1/512-bit units.
    /// </summary>
    public int GetFilterIntraMode(int symbol)
        => this.FilterIntraMode[symbol];

    /// <summary>
    /// Gets a retained angle delta rate in 1/512-bit units.
    /// </summary>
    public int GetAngleDelta(int row, int symbol)
        => this.AngleDelta[(row * AngleDeltaAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained intra block copy rate in 1/512-bit units.
    /// </summary>
    public int GetIntraBlockCopy(int symbol)
        => this.IntraBlockCopy[symbol];

    /// <summary>
    /// Gets a retained palette ysize rate in 1/512-bit units.
    /// </summary>
    public int GetPaletteYSize(int row, int symbol)
        => this.PaletteYSize[(row * PaletteYSizeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained palette uv size rate in 1/512-bit units.
    /// </summary>
    public int GetPaletteUvSize(int row, int symbol)
        => this.PaletteUvSize[(row * PaletteUvSizeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained palette ycolor index rate in 1/512-bit units.
    /// </summary>
    public int GetPaletteYColorIndex(int plane, int row, int symbol)
        => this.PaletteYColorIndex[(((plane * PaletteYColorIndexRowCount) + row) * PaletteYColorIndexAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained palette uv color index rate in 1/512-bit units.
    /// </summary>
    public int GetPaletteUvColorIndex(int plane, int row, int symbol)
        => this.PaletteUvColorIndex[(((plane * PaletteUvColorIndexRowCount) + row) * PaletteUvColorIndexAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained palette ymode rate in 1/512-bit units.
    /// </summary>
    public int GetPaletteYMode(int plane, int row, int symbol)
        => this.PaletteYMode[(((plane * PaletteYModeRowCount) + row) * PaletteYModeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained palette uv mode rate in 1/512-bit units.
    /// </summary>
    public int GetPaletteUvMode(int row, int symbol)
        => this.PaletteUvMode[(row * PaletteUvModeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained new mv rate in 1/512-bit units.
    /// </summary>
    public int GetNewMv(int row, int symbol)
        => this.NewMv[(row * NewMvAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained zero mv rate in 1/512-bit units.
    /// </summary>
    public int GetZeroMv(int row, int symbol)
        => this.ZeroMv[(row * ZeroMvAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained ref mv rate in 1/512-bit units.
    /// </summary>
    public int GetRefMv(int row, int symbol)
        => this.RefMv[(row * RefMvAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained drl rate in 1/512-bit units.
    /// </summary>
    public int GetDrl(int row, int symbol)
        => this.Drl[(row * DrlAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained single reference rate in 1/512-bit units.
    /// </summary>
    public int GetSingleReference(int plane, int row, int symbol)
        => this.SingleReference[(((plane * SingleReferenceRowCount) + row) * SingleReferenceAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained intra inter rate in 1/512-bit units.
    /// </summary>
    public int GetIntraInter(int row, int symbol)
        => this.IntraInter[(row * IntraInterAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained switchable interpolation rate in 1/512-bit units.
    /// </summary>
    public int GetSwitchableInterpolation(int row, int symbol)
        => this.SwitchableInterpolation[(row * SwitchableInterpolationAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained skip rate in 1/512-bit units.
    /// </summary>
    public int GetSkip(int row, int symbol)
        => this.Skip[(row * SkipAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained transform size rate in 1/512-bit units.
    /// </summary>
    public int GetTransformSize(int plane, int row, int symbol)
        => this.TransformSize[(((plane * TransformSizeRowCount) + row) * TransformSizeAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained transform partition rate in 1/512-bit units.
    /// </summary>
    public int GetTransformPartition(int row, int symbol)
        => this.TransformPartition[(row * TransformPartitionAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained inter extended transform rate in 1/512-bit units.
    /// </summary>
    public int GetInterExtendedTransform(int plane, int row, int symbol)
        => this.InterExtendedTransform[(((plane * InterExtendedTransformRowCount) + row) * InterExtendedTransformAlphabetSize) + symbol];

    /// <summary>
    /// Gets a retained intra extended transform rate in 1/512-bit units.
    /// </summary>
    public int GetIntraExtendedTransform(int cube, int plane, int row, int symbol)
        => this.IntraExtendedTransform[
            (((((cube * IntraExtendedTransformPlaneCount) + plane) * IntraExtendedTransformRowCount) + row) * IntraExtendedTransformAlphabetSize) + symbol];

    /// <summary>
    /// Gets the combined U/V magnitude and joint-sign rate in 1/512-bit units.
    /// </summary>
    public int GetChromaFromLuma(int index, int joinedSign)
    {
        int offset = joinedSign * ChromaFromLumaPlaneCount * ChromaFromLumaAlphabetSize;
        return this.ChromaFromLuma[offset + Av1ChromaFromLumaMath.IndexU(index)]
            + this.ChromaFromLuma[offset + ChromaFromLumaAlphabetSize + Av1ChromaFromLumaMath.IndexV(index)];
    }

    /// <summary>
    /// Replaces the retained rates with the costs of the supplied entropy distributions.
    /// </summary>
    public void Update(Av1FrameEntropyContext context)
    {
        // Fixed strides preserve the context and alphabet dimensions. Unused symbols are not read:
        // each caller selects its legal alphabet before indexing a retained rate.
        FillRows(context.PartitionTypes, this.PartitionTypes, PartitionTypesAlphabetSize);
        FillRows(context.FrameYMode, this.FrameYMode, FrameYModeAlphabetSize);
        FillPlanes(context.KeyFrameYMode, this.KeyFrameYMode, KeyFrameYModeAlphabetSize, KeyFrameYModeRowCount);
        FillPlanes(context.UvMode, this.UvMode, UvModeAlphabetSize, UvModeRowCount);
        FillRows(context.FilterIntra, this.FilterIntra, FilterIntraAlphabetSize);
        Av1ProbabilityCost.FillSymbolCosts(context.FilterIntraMode, this.FilterIntraMode);
        FillRows(context.AngleDelta, this.AngleDelta, AngleDeltaAlphabetSize);
        Av1ProbabilityCost.FillSymbolCosts(context.IntraBlockCopy, this.IntraBlockCopy);
        FillRows(context.PaletteYSize, this.PaletteYSize, PaletteYSizeAlphabetSize);
        FillRows(context.PaletteUvSize, this.PaletteUvSize, PaletteUvSizeAlphabetSize);
        FillPlanes(context.PaletteYColorIndex, this.PaletteYColorIndex, PaletteYColorIndexAlphabetSize, PaletteYColorIndexRowCount);
        FillPlanes(context.PaletteUvColorIndex, this.PaletteUvColorIndex, PaletteUvColorIndexAlphabetSize, PaletteUvColorIndexRowCount);
        FillPlanes(context.PaletteYMode, this.PaletteYMode, PaletteYModeAlphabetSize, PaletteYModeRowCount);
        FillRows(context.PaletteUvMode, this.PaletteUvMode, PaletteUvModeAlphabetSize);
        FillRows(context.SkipMode, this.SkipMode, SkipModeAlphabetSize);
        FillRows(context.NewMv, this.NewMv, NewMvAlphabetSize);
        FillRows(context.ZeroMv, this.ZeroMv, ZeroMvAlphabetSize);
        FillRows(context.RefMv, this.RefMv, RefMvAlphabetSize);
        FillRows(context.Drl, this.Drl, DrlAlphabetSize);
        FillPlanes(context.SingleReference, this.SingleReference, SingleReferenceAlphabetSize, SingleReferenceRowCount);
        FillRows(context.CompInter, this.CompInter, CompInterAlphabetSize);
        FillRows(context.CompoundReferenceType, this.CompoundReferenceType, CompoundReferenceTypeAlphabetSize);
        FillPlanes(
            context.UnidirectionalCompoundReference,
            this.UnidirectionalCompoundReference,
            UnidirectionalCompoundReferenceAlphabetSize,
            UnidirectionalCompoundReferenceRowCount);

        FillPlanes(context.CompoundReference, this.CompoundReference, CompoundReferenceAlphabetSize, CompoundReferenceRowCount);
        FillPlanes(context.CompoundBackwardReference, this.CompoundBackwardReference, CompoundBackwardReferenceAlphabetSize, CompoundBackwardReferenceRowCount);
        FillRows(context.IntraInter, this.IntraInter, IntraInterAlphabetSize);
        FillRows(context.InterCompoundMode, this.InterCompoundMode, InterCompoundModeAlphabetSize);
        FillRows(context.CompoundType, this.CompoundType, CompoundTypeAlphabetSize);
        FillRows(context.WedgeIndex, this.WedgeIndex, WedgeIndexAlphabetSize);
        FillRows(context.InterIntra, this.InterIntra, InterIntraAlphabetSize);
        FillRows(context.WedgeInterIntra, this.WedgeInterIntra, WedgeInterIntraAlphabetSize);
        FillRows(context.InterIntraMode, this.InterIntraMode, InterIntraModeAlphabetSize);
        FillRows(context.CompoundIndex, this.CompoundIndex, CompoundIndexAlphabetSize);
        FillRows(context.CompoundGroupIndex, this.CompoundGroupIndex, CompoundGroupIndexAlphabetSize);
        FillRows(context.MotionMode, this.MotionMode, MotionModeAlphabetSize);
        FillRows(context.Obmc, this.Obmc, ObmcAlphabetSize);
        FillRows(context.SwitchableInterpolation, this.SwitchableInterpolation, SwitchableInterpolationAlphabetSize);
        FillRows(context.Skip, this.Skip, SkipAlphabetSize);
        FillPlanes(context.TransformSize, this.TransformSize, TransformSizeAlphabetSize, TransformSizeRowCount);
        FillRows(context.TransformPartition, this.TransformPartition, TransformPartitionAlphabetSize);
        FillPlanes(context.InterExtendedTransform, this.InterExtendedTransform, InterExtendedTransformAlphabetSize, InterExtendedTransformRowCount);
        FillCubes(
            context.IntraExtendedTransform,
            this.IntraExtendedTransform,
            IntraExtendedTransformAlphabetSize,
            IntraExtendedTransformRowCount,
            IntraExtendedTransformPlaneCount);

        Av1ProbabilityCost.FillSymbolCosts(context.SwitchableRestoration, this.SwitchableRestoration);
        Av1ProbabilityCost.FillSymbolCosts(context.WienerRestoration, this.WienerRestoration);
        Av1ProbabilityCost.FillSymbolCosts(context.SgrProjectionRestoration, this.SgrProjectionRestoration);
        FillRows(context.SegmentIdPredicted, this.SegmentIdPredicted, SegmentIdPredictedAlphabetSize);
        FillRows(context.SegmentId, this.SegmentId, SegmentIdAlphabetSize);

        // The joint sign is charged once, in U's row. A zero component has no magnitude symbol,
        // so its complete magnitude row is constant rather than conditionally charged in every trial.
        Span<int> chromaFromLuma = this.ChromaFromLuma;
        for (int sign = 0; sign < ChromaFromLumaSignCount; sign++)
        {
            int signCost = Av1ProbabilityCost.GetSymbolCost(context.ChromaFromLumaSign, sign);
            Span<int> u = chromaFromLuma.Slice(sign * ChromaFromLumaPlaneCount * ChromaFromLumaAlphabetSize, ChromaFromLumaAlphabetSize);
            Span<int> v = chromaFromLuma.Slice(((sign * ChromaFromLumaPlaneCount) + 1) * ChromaFromLumaAlphabetSize, ChromaFromLumaAlphabetSize);
            if (Av1ChromaFromLumaMath.SignU(sign) == Av1ChromaFromLumaMath.SignZero)
            {
                u.Fill(signCost);
            }
            else
            {
                Av1ProbabilityCost.FillSymbolCosts(context.ChromaFromLumaAlpha[Av1ChromaFromLumaMath.ContextU(sign)], u);
                for (int index = 0; index < u.Length; index++)
                {
                    u[index] += signCost;
                }
            }

            if (Av1ChromaFromLumaMath.SignV(sign) == Av1ChromaFromLumaMath.SignZero)
            {
                v.Clear();
            }
            else
            {
                Av1ProbabilityCost.FillSymbolCosts(context.ChromaFromLumaAlpha[Av1ChromaFromLumaMath.ContextV(sign)], v);
            }
        }
    }

    private static void FillRows(Av1Distribution[] distributions, Span<int> costs, int alphabetSize)
    {
        for (int row = 0; row < distributions.Length; row++)
        {
            Av1ProbabilityCost.FillSymbolCosts(distributions[row], costs.Slice(row * alphabetSize, alphabetSize));
        }
    }

    private static void FillPlanes(Av1Distribution[][] distributions, Span<int> costs, int alphabetSize, int rowCount)
    {
        int planeLength = rowCount * alphabetSize;
        for (int plane = 0; plane < distributions.Length; plane++)
        {
            FillRows(distributions[plane], costs.Slice(plane * planeLength, planeLength), alphabetSize);
        }
    }

    private static void FillCubes(Av1Distribution[][][] distributions, Span<int> costs, int alphabetSize, int rowCount, int planeCount)
    {
        int cubeLength = planeCount * rowCount * alphabetSize;
        for (int cube = 0; cube < distributions.Length; cube++)
        {
            FillPlanes(distributions[cube], costs.Slice(cube * cubeLength, cubeLength), alphabetSize, rowCount);
        }
    }
}
