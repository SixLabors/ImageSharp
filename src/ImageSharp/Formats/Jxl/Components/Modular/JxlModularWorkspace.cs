// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Owns reusable residual history, integer prediction and reconstruction scratch, and bounded cross-channel properties for one modular frame.
/// </summary>
internal sealed class JxlModularWorkspace : IDisposable
{
    private readonly MemoryAllocator allocator;
    private IMemoryOwner<uint>? history;
    private IMemoryOwner<int>? integerScratch;
    private IMemoryOwner<int>? references;
    private IMemoryOwner<JxlModularTree.Node>? channelTree;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularWorkspace"/> class without renting unused buffers.
    /// </summary>
    /// <param name="allocator">The decoder's tracked allocator.</param>
    public JxlModularWorkspace(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Prepares a power-of-two history ring before decoding one entropy stream.
    /// </summary>
    /// <param name="length">The bounded ring length, or zero without LZ77.</param>
    /// <returns>The exact requested span, reused from an earlier stream when its allocation is large enough.</returns>
    public Span<uint> PrepareHistory(int length) => Prepare(ref this.history, length, this.allocator);

    /// <summary>
    /// Prepares weighted error rows for the largest channel handled by the current operation.
    /// </summary>
    /// <param name="width">The validated maximum channel width.</param>
    /// <returns>The caller's reusable error span.</returns>
    public Span<int> PrepareErrors(int width) => Prepare(ref this.integerScratch, JxlWeightedPredictor.GetStorageLength(width), this.allocator);

    /// <summary>
    /// Borrows one integer row after modular decoding, reusing predictor storage when its capacity is sufficient.
    /// </summary>
    /// <param name="width">The validated coded row width.</param>
    /// <returns>The reusable row span. No predictor operation can overlap its reconstruction use.</returns>
    public Span<int> PrepareReconstructionRow(int width) => Prepare(ref this.integerScratch, width, this.allocator);

    /// <summary>
    /// Prepares four property planes per earlier channel, with at most 64 samples in each plane.
    /// </summary>
    /// <param name="length">The required property count times the 64-sample block length, at most 15360 integers.</param>
    /// <returns>The reusable property span, borrowed once before sample loops.</returns>
    public Span<int> PrepareReferences(int length) => Prepare(ref this.references, length, this.allocator);

    /// <summary>
    /// Prepares reusable nodes for the current channel's statically reduced prediction tree.
    /// </summary>
    /// <param name="length">The initialized source tree length, which bounds the reduced tree and its pending nodes.</param>
    /// <returns>The reusable tree span, borrowed once before channel traversal.</returns>
    public Span<JxlModularTree.Node> PrepareChannelTree(int length) => Prepare(ref this.channelTree, length, this.allocator);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.channelTree?.Dispose();
        this.references?.Dispose();
        this.integerScratch?.Dispose();
        this.history?.Dispose();
    }

    /// <summary>
    /// Grows one tracked allocation only before an entropy stream, inverse transform or completed-frame reconstruction.
    /// </summary>
    /// <typeparam name="T">The unmanaged scratch element type.</typeparam>
    /// <param name="owner">The allocation retained between operations.</param>
    /// <param name="length">The required span length.</param>
    /// <param name="allocator">The allocator used for a larger replacement.</param>
    /// <returns>The exact requested span, or an empty span when no storage is required.</returns>
    private static Span<T> Prepare<T>(ref IMemoryOwner<T>? owner, int length, MemoryAllocator allocator)
        where T : unmanaged
    {
        if (length == 0)
        {
            return [];
        }

        if (owner is null || owner.Memory.Length < length)
        {
            // Scratch does not retain values across streams. Allocate first so a failed rent leaves the old owner disposable.
            IMemoryOwner<T> replacement = allocator.Allocate<T>(length);
            owner?.Dispose();
            owner = replacement;
        }

        return owner.Memory.Span[..length];
    }
}
