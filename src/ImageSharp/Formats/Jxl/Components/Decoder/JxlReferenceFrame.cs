// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Owns the completed native planes retained in one ordinary frame-reference slot.
/// </summary>
/// <remarks>
/// LF references have separate decoder-owned storage. This owner retains no entropy, coefficients or reconstruction workspace.
/// </remarks>
internal sealed class JxlReferenceFrame : IDisposable
{
    /// <summary>
    /// The distinct color owners. A gray reference can supply one plane for its three logical components.
    /// </summary>
    private readonly Buffer2D<float>[] colors;

    /// <summary>
    /// The distinct full-resolution extra-channel owners in encoded order, including undisplayed channels.
    /// </summary>
    private readonly Buffer2D<float>[] extras;

    /// <summary>
    /// Indicates whether the retained component planes have been released.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlReferenceFrame"/> class with completed, exclusively owned planes.
    /// </summary>
    /// <param name="colors">One gray or three distinct color owners, with the completed rectangle initialized.</param>
    /// <param name="extras">The completed full-resolution extra-channel owners in encoded order.</param>
    /// <param name="size">The initialized reference rectangle, in samples.</param>
    /// <param name="origin">The rectangle's signed origin on the image canvas.</param>
    /// <param name="savedBeforeColorTransform">Whether these actual samples precede the frame color transform.</param>
    /// <remarks>
    /// Successful construction transfers every plane and both arrays. The caller retains them if construction fails.
    /// The caller supplies the completed transform domain; this constructor does not convert samples.
    /// Keep the current slot alive until reconstruction and replacement creation succeed, then dispose its preceding owner.
    /// </remarks>
    public JxlReferenceFrame(Buffer2D<float>[] colors, Buffer2D<float>[] extras, Size size, Point origin, bool savedBeforeColorTransform)
    {
        this.colors = colors;
        this.extras = extras;
        this.Size = size;
        this.Origin = origin;
        this.SavedBeforeColorTransform = savedBeforeColorTransform;
    }

    /// <summary>
    /// Gets the initialized reference dimensions before placement on the canvas.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// Gets the signed reference origin on the image canvas.
    /// </summary>
    public Point Origin { get; }

    /// <summary>
    /// Gets a value indicating whether the retained samples precede the frame color transform.
    /// </summary>
    public bool SavedBeforeColorTransform { get; }

    /// <summary>
    /// Borrows one logical color component without transferring its owner.
    /// </summary>
    /// <param name="component">The logical color-component index, from zero through two.</param>
    /// <returns>The initialized rectangle. A one-plane gray reference supplies the same samples for all three logical components.</returns>
    public Buffer2DRegion<float> GetColorChannel(int component)
        => this.colors[this.colors.Length == 1 ? 0 : component].GetRegion(new Rectangle(Point.Empty, this.Size));

    /// <summary>
    /// Borrows one extra channel without transferring its owner.
    /// </summary>
    /// <param name="index">The encoded extra-channel index.</param>
    /// <returns>The initialized full-resolution extra rectangle.</returns>
    public Buffer2DRegion<float> GetExtraChannel(int index)
        => this.extras[index].GetRegion(new Rectangle(Point.Empty, this.Size));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        // Arrays contain distinct owners. Gray repetition occurs only in the borrowed accessor;
        // alpha and black selections are views of the extra array, not additional owned allocations.
        foreach (Buffer2D<float> plane in this.colors)
        {
            plane.Dispose();
        }

        foreach (Buffer2D<float> plane in this.extras)
        {
            plane.Dispose();
        }

        this.isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
