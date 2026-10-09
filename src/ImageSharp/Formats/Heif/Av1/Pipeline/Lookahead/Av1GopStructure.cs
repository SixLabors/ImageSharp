// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The coding order of one golden frame group. For each coded frame, it holds the update role, the look-ahead offset of the source, the display
/// position, the pyramid layer and the boost. A group of <c>n</c> shown frames that uses an alternate reference codes the alternate reference
/// first. Then it codes the frames before it in a pyramid of internal alternate references and their overlays. The overlay of the alternate
/// reference comes last.
/// </summary>
internal sealed class Av1GopStructure
{
    /// <summary>
    /// The largest number of coded frames in a group.
    /// </summary>
    public const int MaximumLength = 250;

    /// <summary>
    /// The pyramid layer of leaf frames and overlays.
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
    /// Gets the update role of each coded frame.
    /// </summary>
    public Span<Av1FrameUpdateType> UpdateTypes => this.updateTypes;

    /// <summary>
    /// Gets the offset of the source of each frame from the first frame not yet shown. Only alternate references have a nonzero offset.
    /// </summary>
    public Span<int> ArfSourceOffsets => this.arfSourceOffsets;

    /// <summary>
    /// Gets the number of shown frames of the group before each frame.
    /// </summary>
    public Span<int> CurrentFrameIndices => this.currentFrameIndices;

    /// <summary>
    /// Gets the pyramid layer of each frame. Key and golden frames use layer 0. The alternate reference uses layer 1. Internal alternate
    /// references use deeper layers. Leaves use <see cref="MaximumArfLayers"/>.
    /// </summary>
    public Span<int> LayerDepths => this.layerDepths;

    /// <summary>
    /// Gets the boost of each frame.
    /// </summary>
    public Span<int> ArfBoosts => this.arfBoosts;

    /// <summary>
    /// Gets a value for each frame indicating whether it is a key frame.
    /// </summary>
    public Span<bool> KeyFrames => this.keyFrames;

    /// <summary>
    /// Gets a value for each frame indicating whether it refreshes every reference slot.
    /// </summary>
    public Span<bool> ReferenceResets => this.referenceResets;

    /// <summary>
    /// Gets the display index of each frame.
    /// </summary>
    public Span<int> DisplayIndices => this.displayIndices;

    /// <summary>
    /// Gets the quantizer index that the rate control estimated for each frame before the temporal dependency model runs.
    /// </summary>
    public Span<int> QValues => this.qValues;

    /// <summary>
    /// Gets the bit target of each frame of the group. Coding under a bit budget allocates these targets.
    /// </summary>
    public Span<int> BitAllocations => this.bitAllocations;

    /// <summary>
    /// Gets or sets the number of coded frames.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the deepest pyramid layer of the group.
    /// </summary>
    public int MaxLayerDepth { get; set; }

    /// <summary>
    /// Gets or sets the deepest pyramid layer that the group can use. Zero disables the alternate reference.
    /// </summary>
    public int MaxLayerDepthAllowed { get; set; }

    /// <summary>
    /// Gets or sets the index of the alternate reference, or -1.
    /// </summary>
    public int ArfIndex { get; set; }

    /// <summary>
    /// Resets every entry and every property to zero.
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
