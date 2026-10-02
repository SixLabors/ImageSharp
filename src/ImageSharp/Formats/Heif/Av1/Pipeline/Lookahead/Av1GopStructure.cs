// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The coding order of one golden frame group: for each coded frame its update role, the look-ahead offset of its
/// source, its display position, its pyramid layer and its boost. A group of <c>n</c> shown frames that uses an
/// alternate reference codes the alternate reference first, then the frames before it in a pyramid of internal
/// alternate references and their overlays, and ends with the overlay of the alternate reference.
/// Reference: GF_GROUP.
/// </summary>
internal sealed class Av1GopStructure
{
    /// <summary>
    /// The largest number of coded frames in a group. Reference: MAX_STATIC_GF_GROUP_LENGTH.
    /// </summary>
    public const int MaximumLength = 250;

    /// <summary>
    /// The pyramid layer of leaf frames and overlays. Reference: MAX_ARF_LAYERS.
    /// </summary>
    public const int MaximumArfLayers = 6;

    private readonly Av1FrameUpdateType[] updateTypes = new Av1FrameUpdateType[MaximumLength];
    private readonly int[] arfSourceOffsets = new int[MaximumLength];
    private readonly int[] currentFrameIndices = new int[MaximumLength];
    private readonly int[] layerDepths = new int[MaximumLength];
    private readonly int[] arfBoosts = new int[MaximumLength];
    private readonly bool[] keyFrames = new bool[MaximumLength];
    private readonly bool[] referenceResets = new bool[MaximumLength];
    private readonly int[] displayIndices = new int[MaximumLength];
    private readonly int[] qValues = new int[MaximumLength];
    private readonly int[] bitAllocations = new int[MaximumLength];

    /// <summary>
    /// Gets the update role of each coded frame. Reference: update_type.
    /// </summary>
    public Span<Av1FrameUpdateType> UpdateTypes => this.updateTypes;

    /// <summary>
    /// Gets the offset of each frame's source from the first frame not yet shown, which is nonzero only for
    /// alternate references. Reference: arf_src_offset.
    /// </summary>
    public Span<int> ArfSourceOffsets => this.arfSourceOffsets;

    /// <summary>
    /// Gets the number of shown frames of the group before each frame. Reference: cur_frame_idx.
    /// </summary>
    public Span<int> CurrentFrameIndices => this.currentFrameIndices;

    /// <summary>
    /// Gets the pyramid layer of each frame: 0 for key and golden frames, 1 for the alternate reference, deeper
    /// for internal alternate references, and <see cref="MaximumArfLayers"/> for leaves. Reference: layer_depth.
    /// </summary>
    public Span<int> LayerDepths => this.layerDepths;

    /// <summary>
    /// Gets the boost of each frame. Reference: arf_boost.
    /// </summary>
    public Span<int> ArfBoosts => this.arfBoosts;

    /// <summary>
    /// Gets a value for each frame indicating whether it is a key frame. Reference: frame_type.
    /// </summary>
    public Span<bool> KeyFrames => this.keyFrames;

    /// <summary>
    /// Gets a value for each frame indicating whether it refreshes every reference slot.
    /// Reference: refbuf_state equal to REFBUF_RESET.
    /// </summary>
    public Span<bool> ReferenceResets => this.referenceResets;

    /// <summary>
    /// Gets the display index of each frame. Reference: display_idx.
    /// </summary>
    public Span<int> DisplayIndices => this.displayIndices;

    /// <summary>
    /// Gets the quantizer index the rate control estimated for each frame before the temporal dependency model runs.
    /// Reference: q_val.
    /// </summary>
    public Span<int> QValues => this.qValues;

    /// <summary>
    /// Gets the bit target of each frame of the group, which coding under a bit budget allocates. Reference:
    /// bit_allocation.
    /// </summary>
    public Span<int> BitAllocations => this.bitAllocations;

    /// <summary>
    /// Gets or sets the number of coded frames. Reference: size.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the deepest pyramid layer of the group. Reference: max_layer_depth.
    /// </summary>
    public int MaxLayerDepth { get; set; }

    /// <summary>
    /// Gets or sets the deepest pyramid layer the group may use; zero disables the alternate reference.
    /// Reference: max_layer_depth_allowed.
    /// </summary>
    public int MaxLayerDepthAllowed { get; set; }

    /// <summary>
    /// Gets or sets the index of the alternate reference, or -1. Reference: arf_index.
    /// </summary>
    public int ArfIndex { get; set; }

    /// <summary>
    /// Resets every entry. Reference: av1_zero() of GF_GROUP.
    /// </summary>
    public void Clear()
    {
        Array.Clear(this.updateTypes);
        Array.Clear(this.arfSourceOffsets);
        Array.Clear(this.currentFrameIndices);
        Array.Clear(this.layerDepths);
        Array.Clear(this.arfBoosts);
        Array.Clear(this.keyFrames);
        Array.Clear(this.referenceResets);
        Array.Clear(this.displayIndices);
        Array.Clear(this.qValues);
        Array.Clear(this.bitAllocations);
        this.Size = 0;
        this.MaxLayerDepth = 0;
        this.MaxLayerDepthAllowed = 0;
        this.ArfIndex = 0;
    }

    /// <summary>
    /// Sets the entries of one coded frame.
    /// </summary>
    /// <param name="index">The frame's index in the group.</param>
    /// <param name="updateType">The update role.</param>
    /// <param name="arfSourceOffset">The look-ahead offset of the source.</param>
    /// <param name="currentFrameIndex">The number of shown frames of the group before the frame.</param>
    /// <param name="layerDepth">The pyramid layer.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="referenceReset">Whether the frame refreshes every reference slot.</param>
    public void Set(int index, Av1FrameUpdateType updateType, int arfSourceOffset, int currentFrameIndex, int layerDepth, bool keyFrame, bool referenceReset)
    {
        this.updateTypes[index] = updateType;
        this.arfSourceOffsets[index] = arfSourceOffset;
        this.currentFrameIndices[index] = currentFrameIndex;
        this.layerDepths[index] = layerDepth;
        this.keyFrames[index] = keyFrame;
        this.referenceResets[index] = referenceReset;
    }
}
