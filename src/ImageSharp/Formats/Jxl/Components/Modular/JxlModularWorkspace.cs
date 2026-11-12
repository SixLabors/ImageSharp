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
    private IMemoryOwner<int>? predictionRows;
    private IMemoryOwner<int>? references;
    private IMemoryOwner<JxlModularTree.Node>? channelTree;
    private IMemoryOwner<ulong>? treeLookup;
    private IMemoryOwner<JxlModularTree.FlatNode>? flatTree;

    /// <summary>
    /// Indicates whether the retained allocations have been released.
    /// </summary>
    private bool isDisposed;

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
    /// Prepares three wide prediction rows for signed 16-bit channel storage.
    /// </summary>
    /// <param name="width">The largest channel width used by this sequential operation.</param>
    /// <returns>Three int rows, reused by every short channel and inverse palette operation.</returns>
    public Span<int> PreparePredictionRows(int width) => Prepare(ref this.predictionRows, 3 * width, this.allocator);

    /// <summary>
    /// Borrows one integer row after modular decoding, reusing predictor storage when its capacity is sufficient.
    /// </summary>
    /// <param name="width">The validated coded row width.</param>
    /// <returns>The reusable row span. No predictor operation can overlap its reconstruction use.</returns>
    public Span<int> PrepareReconstructionRow(int width) => Prepare(ref this.integerScratch, width, this.allocator);

    /// <summary>
    /// Prepares bounded cross-channel property planes or temporary interval-lookup metadata.
    /// </summary>
    /// <param name="length">The bounded required length. Property planes use at most 15360 integers; lookup metadata uses at most 4369.</param>
    /// <returns>The reusable property span, borrowed once before sample loops.</returns>
    public Span<int> PrepareReferences(int length) => Prepare(ref this.references, length, this.allocator);

    /// <summary>
    /// Borrows one implicit-palette component table after modular sample decoding.
    /// </summary>
    /// <param name="explicitEntries">Space for widening one short explicit palette component, or zero for int storage.</param>
    /// <returns>The reusable table span. No reference-property operation can overlap its palette use.</returns>
    public Span<int> PrepareImplicitPalette(int explicitEntries)
        => Prepare(ref this.references, JxlModularPalette.ImplicitEntryCount + explicitEntries, this.allocator);

    /// <summary>
    /// Prepares reusable nodes for the current channel's statically reduced prediction tree.
    /// </summary>
    /// <param name="length">The initialized source tree length, which bounds the reduced tree and its pending nodes.</param>
    /// <returns>The reusable tree span, borrowed once before channel traversal.</returns>
    public Span<JxlModularTree.Node> PrepareChannelTree(int length) => Prepare(ref this.channelTree, length, this.allocator);

    /// <summary>
    /// Prepares compact interval tables and leaf masks for one reduced channel tree.
    /// </summary>
    /// <param name="length">The actual table size in eight-byte words, including alignment padding.</param>
    /// <returns>The retained table span, borrowed until the next channel is prepared.</returns>
    public Span<ulong> PrepareTreeLookup(int length) => Prepare(ref this.treeLookup, length, this.allocator);

    /// <summary>
    /// Prepares two-level split nodes and duplicated leaf entries for the current channel.
    /// </summary>
    /// <param name="length">The exact completed flat-tree length.</param>
    /// <returns>The retained node span, reused as the preparation queue before sample traversal.</returns>
    public Span<JxlModularTree.FlatNode> PrepareFlatTree(int length) => Prepare(ref this.flatTree, length, this.allocator);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.flatTree?.Dispose();
        this.treeLookup?.Dispose();
        this.channelTree?.Dispose();
        this.references?.Dispose();
        this.predictionRows?.Dispose();
        this.integerScratch?.Dispose();
        this.history?.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    /// Grows one tracked allocation before channel traversal, an inverse transform or completed-frame reconstruction.
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
