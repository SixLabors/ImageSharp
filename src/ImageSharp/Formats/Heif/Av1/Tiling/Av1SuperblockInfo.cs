// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the partition tree and decoded mode information for one AV1 superblock.
/// </summary>
internal readonly struct Av1SuperblockInfo
{
    /// <summary>
    /// Provides the frame-owned state and current-superblock scratch used by this view.
    /// </summary>
    private readonly Av1FrameInfo frameInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SuperblockInfo"/> struct.
    /// </summary>
    /// <param name="frameInfo">The owning frame information.</param>
    /// <param name="position">The superblock position in the frame superblock grid.</param>
    public Av1SuperblockInfo(Av1FrameInfo frameInfo, Point position)
    {
        this.Position = position;
        this.frameInfo = frameInfo;
    }

    /// <summary>
    /// Gets the position of this superblock in the frame superblock grid.
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// Gets the frame-relative superblock origin in 4x4 mode-information units.
    /// </summary>
    public Point ModeInfoPosition => this.Position * this.frameInfo.SuperblockModeInfoSize;

    /// <summary>
    /// Gets a reference to the active base quantizer index for this superblock.
    /// </summary>
    public ref int SuperblockQuantizerIndex => ref this.frameInfo.GetQuantizerIndex(this.Position);

    /// <summary>
    /// Gets the mode information that covers the superblock origin.
    /// </summary>
    public Av1BlockModeInfo SuperblockModeInfo => this.GetModeInfo(new Point(0, 0));

    /// <summary>
    /// Gets the luma coefficient scratch for the current superblock.
    /// </summary>
    public Span<int> CoefficientsY => this.frameInfo.GetCoefficientsY();

    /// <summary>
    /// Gets the blue-difference chroma coefficient scratch for the current superblock.
    /// </summary>
    public Span<int> CoefficientsU => this.frameInfo.GetCoefficientsU();

    /// <summary>
    /// Gets the red-difference chroma coefficient scratch for the current superblock.
    /// </summary>
    public Span<int> CoefficientsV => this.frameInfo.GetCoefficientsV();

    /// <summary>
    /// Gets the constrained directional enhancement filter strengths for this superblock.
    /// </summary>
    public Span<int> CdefStrength => this.frameInfo.GetCdefStrength(this.Position);

    /// <summary>
    /// Gets the loop-filter deltas for this superblock.
    /// </summary>
    public Span<int> SuperblockDeltaLoopFilter => this.frameInfo.GetDeltaLoopFilter(this.Position);

    /// <summary>
    /// Gets the number of mode-information records parsed for this superblock.
    /// </summary>
    public int BlockCount => this.frameInfo.GetModeInfoCount(this.Position);

    /// <summary>
    /// Gets the luma transform-information scratch for the current superblock.
    /// </summary>
    /// <returns>The current-superblock luma transform-information span.</returns>
    public Span<Av1TransformInfo> GetTransformInfoY() => this.frameInfo.GetSuperblockTransformY();

    /// <summary>
    /// Gets the shared chroma transform-information scratch for the current superblock.
    /// </summary>
    /// <returns>The current-superblock chroma transform-information span.</returns>
    public Span<Av1TransformInfo> GetTransformInfoUv() => this.frameInfo.GetSuperblockTransformUv();

    /// <summary>
    /// Gets the transform-information storage for the specified color plane.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <returns>The transform-information span for the plane.</returns>
    public Span<Av1TransformInfo> GetTransformInfo(int plane) => this.frameInfo.GetSuperblockTransform(plane);

    /// <summary>
    /// Gets the mode information records parsed for this superblock in bitstream order.
    /// </summary>
    /// <returns>The mode information records for the superblock.</returns>
    public Av1FrameInfo.ModeInfoCollection GetModeInfos() =>
        this.frameInfo.GetModeInfos(this.Position, this.BlockCount);

    /// <summary>
    /// Gets the mode information covering a position relative to this superblock.
    /// </summary>
    /// <param name="index">The position in 4x4 mode-information units relative to the superblock.</param>
    /// <returns>The mode information covering the position.</returns>
    public Av1BlockModeInfo GetModeInfo(Point index) => this.frameInfo.GetModeInfo(this.Position, index);

    /// <summary>
    /// Gets the mode information covering a frame-relative position.
    /// </summary>
    /// <param name="index">The frame-relative position in 4x4 mode-information units.</param>
    /// <returns>The mode information covering the position.</returns>
    public Av1BlockModeInfo GetModeInfoAt(Point index) => this.frameInfo.GetModeInfoAt(index);

    /// <summary>
    /// Gets the coefficient storage for the specified color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The coefficient span for the plane, or an empty span for an unsupported value.</returns>
    public Span<int> GetCoefficients(Av1Plane plane) => plane switch
    {
        Av1Plane.Y => this.CoefficientsY,
        Av1Plane.U => this.CoefficientsU,
        Av1Plane.V => this.CoefficientsV,
        _ => []
    };
}
