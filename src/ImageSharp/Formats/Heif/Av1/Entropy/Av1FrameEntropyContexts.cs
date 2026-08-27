// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Owns the reusable frame-base, tile-working, and published AV1 entropy contexts for one decoder session.
/// </summary>
internal sealed class Av1FrameEntropyContexts
{
    /// <summary>
    /// The maximum number of live reference-map and presentation owners plus the newly reconstructed frame awaiting
    /// commit.
    /// </summary>
    private const int MaximumSnapshotCount = Av1Constants.ReferenceFrameCount + 2;

    /// <summary>
    /// Session-local returned snapshot graphs available for later refreshed frames.
    /// </summary>
    private InlineArray10<Av1FrameEntropyContext?> returnedSnapshots;

    /// <summary>
    /// The number of returned snapshot graphs currently available for reuse.
    /// </summary>
    private int returnedSnapshotCount;

    /// <summary>
    /// The base quantizer index used to initialize a newly required snapshot graph.
    /// </summary>
    private int currentQIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameEntropyContexts"/> class.
    /// </summary>
    /// <param name="qIndex">The initial frame base quantizer index.</param>
    public Av1FrameEntropyContexts(int qIndex)
    {
        this.Base = new(qIndex);
        this.Working = new(qIndex);
        this.Published = new(qIndex);
        this.currentQIndex = qIndex;
    }

    /// <summary>
    /// Gets the unchanged frame context from which each independently decoded tile starts.
    /// </summary>
    public Av1FrameEntropyContext Base { get; }

    /// <summary>
    /// Gets the tile-local context reused sequentially for each tile in the current frame.
    /// </summary>
    public Av1FrameEntropyContext Working { get; }

    /// <summary>
    /// Gets the completed frame context selected by the signaled context-update tile, or the unchanged frame-base
    /// context when frame-end updates are disabled.
    /// </summary>
    public Av1FrameEntropyContext Published { get; }

    /// <summary>
    /// Initializes frame entropy state from either a retained primary reference or normative quantizer-band defaults.
    /// </summary>
    /// <param name="qIndex">The frame base quantizer index selecting coefficient distribution defaults.</param>
    /// <param name="primaryReferenceContext">
    /// The retained primary-reference context, or <see langword="null"/> when the frame selects normative defaults.
    /// </param>
    public void BeginFrame(int qIndex, Av1FrameEntropyContext? primaryReferenceContext)
    {
        this.currentQIndex = qIndex;
        if (primaryReferenceContext is null)
        {
            this.Base.ResetToDefaults(qIndex);
        }
        else
        {
            // A retained context is independent from the working and published graphs. Copying it here preserves the
            // reference owner's snapshot while the current frame adapts its own tile-local state.
            this.Base.CopyFrom(primaryReferenceContext);
        }

        // The context-update tile can precede later tiles. Published therefore cannot alias Working: a later tile
        // must be free to overwrite Working while the selected completed-frame state remains available to the owner.
        this.Base.SnapshotTo(this.Published);
    }

    /// <summary>
    /// Clears active frame entropy state when a new coded sequence invalidates the complete reference map.
    /// </summary>
    public void Reset()
    {
        this.currentQIndex = 0;
        this.Base.ResetToDefaults(this.currentQIndex);
        this.Base.SnapshotTo(this.Working);
        this.Base.SnapshotTo(this.Published);

        // Returned graphs contain no live reference state and remain private to this decoder. Retaining them here
        // allows the next sequence to reuse peak reference ownership without a static cross-decode pool.
    }

    /// <summary>
    /// Rents an independently owned, reset-counter snapshot of the completed frame entropy context.
    /// </summary>
    /// <returns>The snapshot that must later be returned through <see cref="ReturnSnapshot"/>.</returns>
    public Av1FrameEntropyContext RentPublishedSnapshot()
    {
        Av1FrameEntropyContext snapshot;
        if (this.returnedSnapshotCount == 0)
        {
            // Eight slots can own distinct frames while the selected output owns a ninth frame no longer present in
            // the map. Rent one further graph before commit releases the owner displaced by the completed frame.
            snapshot = new(this.currentQIndex);
        }
        else
        {
            int snapshotIndex = --this.returnedSnapshotCount;
            snapshot = this.returnedSnapshots[snapshotIndex]!;
            this.returnedSnapshots[snapshotIndex] = null;
        }

        this.Published.SnapshotTo(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Returns a retained-frame entropy snapshot to this decoder session for later reuse.
    /// </summary>
    /// <param name="snapshot">The snapshot whose reference-frame ownership has ended.</param>
    public void ReturnSnapshot(Av1FrameEntropyContext snapshot)
    {
        // The fixed capacity covers eight distinct slot owners, one detached presentation owner, and the replacement
        // frame rented before commit. Av1ReferenceFrame returns each graph exactly once, so the session cannot exceed
        // this bound.
        this.returnedSnapshots[this.returnedSnapshotCount++] = snapshot;
    }

    /// <summary>
    /// Provides inline storage for every entropy snapshot graph that one decoder session can allocate concurrently.
    /// </summary>
    /// <typeparam name="T">The stored reference type.</typeparam>
    [InlineArray(MaximumSnapshotCount)]
    private struct InlineArray10<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }
}
