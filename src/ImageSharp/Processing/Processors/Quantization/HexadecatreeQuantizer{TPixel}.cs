// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Quantizes an image by building an adaptive 16-way color tree and reducing it to the requested palette size.
/// </summary>
/// <remarks>
/// <para>
/// Each level routes colors using one bit of RGB and, when useful, one bit of alpha, giving the tree up to 16 children
/// per node and letting transparency participate directly in palette construction.
/// </para>
/// <para>
/// Fully opaque mid-tone colors use RGB-only routing so more branch resolution is spent on visible color detail.
/// Transparent, dark, and light colors use alpha-aware routing so opacity changes can form distinct palette buckets.
/// </para>
/// </remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
// See https://github.com/dotnet/roslyn-analyzers/issues/6151
public struct HexadecatreeQuantizer<TPixel> : IQuantizer<TPixel>
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int maxColors;
    private readonly int bitDepth;
    private readonly Hexadecatree tree;
    private readonly IMemoryOwner<TPixel> paletteOwner;
    private ReadOnlyMemory<TPixel> palette;
    private PixelMap<TPixel>? pixelMap;
    private readonly bool isDithering;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HexadecatreeQuantizer{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration that provides memory allocation and pixel conversion services.</param>
    /// <param name="options">The quantizer options that control palette size, dithering, and transparency behavior.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public HexadecatreeQuantizer(Configuration configuration, QuantizerOptions options)
    {
        this.Configuration = configuration;
        this.Options = options;

        this.maxColors = this.Options.MaxColors;
        this.bitDepth = Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(this.maxColors), 1, 8);
        this.tree = new Hexadecatree(configuration, this.bitDepth, this.maxColors, this.Options.TransparencyThreshold);
        this.paletteOwner = configuration.MemoryAllocator.Allocate<TPixel>(this.maxColors, AllocationOptions.Clean);
        this.pixelMap = default;
        this.palette = default;
        this.isDithering = this.Options.Dither is not null;
        this.isDisposed = false;
    }

    /// <inheritdoc/>
    public Configuration Configuration { get; }

    /// <inheritdoc/>
    public QuantizerOptions Options { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<TPixel> Palette
    {
        get
        {
            if (this.palette.IsEmpty)
            {
                this.ResolvePalette();
                QuantizerUtilities.CheckPaletteState(in this.palette);
            }

            return this.palette;
        }
    }

    /// <inheritdoc/>
    public readonly void AddPaletteColors(in Buffer2DRegion<TPixel> pixelRegion)
    {
        PixelRowDelegate pixelRowDelegate = new(this.tree);
        QuantizerUtilities.AddPaletteColors<HexadecatreeQuantizer<TPixel>, TPixel, Rgba32, PixelRowDelegate>(
            ref Unsafe.AsRef(in this),
            in pixelRegion,
            in pixelRowDelegate);
    }

    /// <summary>
    /// Materializes the final palette from the accumulated tree and prepares the dither lookup map when needed.
    /// </summary>
    private void ResolvePalette()
    {
        short paletteIndex = 0;
        Span<TPixel> paletteSpan = this.paletteOwner.GetSpan();

        this.tree.Palettize(paletteSpan, ref paletteIndex);
        ReadOnlyMemory<TPixel> result = this.paletteOwner.Memory[..paletteSpan.Length];

        if (this.isDithering)
        {
            // Dithered colors often no longer land on a color that was seen during palette construction,
            // so the quantization pass switches to nearest-palette matching once the palette is finalized.
            this.pixelMap = PixelMapFactory.Create(this.Configuration, result, this.Options.ColorMatchingMode);
        }

        this.palette = result;
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
        => QuantizerUtilities.QuantizeFrame(ref Unsafe.AsRef(in this), source, bounds);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly byte GetQuantizedColor(TPixel color, out TPixel match)
    {
        if (this.isDithering)
        {
            // Dithering introduces adjusted colors that were never inserted into the tree, so tree lookup
            // is only reliable for the non-dithered path.
            return (byte)this.pixelMap!.GetClosestColor(color, out match);
        }

        ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.palette.Span);
        int index = this.tree.GetPaletteIndex(color);
        match = Unsafe.Add(ref paletteRef, (nuint)index);
        return (byte)index;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.isDisposed = true;
            this.paletteOwner.Dispose();
            this.pixelMap?.Dispose();
            this.pixelMap = null;
            this.tree.Dispose();
        }
    }

    /// <summary>
    /// Forwards source rows into the tree without creating an intermediate buffer.
    /// </summary>
    private readonly struct PixelRowDelegate : IQuantizingPixelRowDelegate<Rgba32>
    {
        private readonly Hexadecatree tree;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelRowDelegate"/> struct.
        /// </summary>
        /// <param name="tree">The destination tree that should accumulate each visited row.</param>
        public PixelRowDelegate(Hexadecatree tree) => this.tree = tree;

        /// <inheritdoc/>
        public void Invoke(ReadOnlySpan<Rgba32> row, int rowIndex) => this.tree.AddColors(row);
    }

    /// <summary>
    /// Stores the adaptive 16-way partition tree used to accumulate colors and emit palette entries.
    /// </summary>
    /// <remarks>
    /// The tree uses a fixed node arena for predictable allocation behavior, keeps per-level reducible node lists so
    /// deeper buckets can be merged until the palette fits, and caches the previously inserted leaf so repeated colors
    /// can be accumulated cheaply.
    /// </remarks>
    internal sealed class Hexadecatree : IDisposable
    {
        // Pooled buffer for OctreeNodes.
        private readonly IMemoryOwner<Node> nodesOwner;

        // One reducible-node head per level.
        // Each entry stores a node index, or -1 when that level currently
        // has no reducible nodes.
        private readonly short[] reducibleNodes;

        // Maximum number of allowable colors.
        private readonly int maxColors;

        // Maximum significant bits.
        private readonly int maxColorBits;

        // The threshold for transparent colors.
        private readonly int transparencyThreshold255;

        // Instead of a reference to the root, we store the index of the root node.
        // Index 0 is reserved for the root.
        private readonly short rootIndex;

        // Running index for node allocation. Start at 1 so that index 0 is reserved for the root.
        private short nextNode = 1;

        // Previously quantized node (index; -1 if none) and its color.
        private int previousNode;
        private Rgba32 previousColor;

        // Free list for reclaimed node indices.
        private readonly Stack<short> freeIndices = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Hexadecatree"/> class.
        /// </summary>
        /// <param name="configuration">The configuration that provides the backing memory allocator.</param>
        /// <param name="maxColorBits">The number of levels to descend before forcing leaves.</param>
        /// <param name="maxColors">The maximum number of palette entries the reduced tree may retain.</param>
        /// <param name="transparencyThreshold">The alpha threshold below which generated palette entries become fully transparent.</param>
        public Hexadecatree(
            Configuration configuration,
            int maxColorBits,
            int maxColors,
            float transparencyThreshold)
        {
            this.maxColorBits = maxColorBits;
            this.maxColors = maxColors;
            this.transparencyThreshold255 = (int)(transparencyThreshold * 255F);
            this.Leaves = 0;
            this.previousNode = -1;
            this.previousColor = default;

            // Allocate a conservative buffer for nodes.
            const int capacity = 4096;
            this.nodesOwner = configuration.MemoryAllocator.Allocate<Node>(capacity, AllocationOptions.Clean);

            // Create the reducible nodes array (one per level 0 .. maxColorBits-1).
            this.reducibleNodes = new short[this.maxColorBits];
            this.reducibleNodes.AsSpan().Fill(-1);

            // Reserve index 0 for the root.
            this.rootIndex = 0;
            ref Node root = ref this.Nodes[this.rootIndex];
            root.Initialize(0, this.maxColorBits, this, this.rootIndex);
        }

        /// <summary>
        /// Gets or sets the number of leaf nodes currently representing palette buckets.
        /// </summary>
        public int Leaves { get; set; }

        /// <summary>
        /// Gets the underlying node arena.
        /// </summary>
        internal Span<Node> Nodes => this.nodesOwner.Memory.Span;

        /// <summary>
        /// Adds a row of colors to the tree.
        /// </summary>
        /// <param name="row">The colors to accumulate.</param>
        public void AddColors(ReadOnlySpan<Rgba32> row)
        {
            for (int x = 0; x < row.Length; x++)
            {
                this.AddColor(row[x]);
            }
        }

        /// <summary>
        /// Adds a single color sample to the tree.
        /// </summary>
        /// <param name="color">The color to accumulate.</param>
        private void AddColor(Rgba32 color)
        {
            // Once the node arena is full and there are no recycled slots available, keep collapsing
            // reducible leaves until the tree is small enough to make forward progress again.
            if (this.nextNode >= this.Nodes.Length && this.freeIndices.Count == 0)
            {
                while (this.Leaves > this.maxColors)
                {
                    this.Reduce();
                }
            }

            // Scanlines often contain long runs of the same color. Caching the previous leaf lets those
            // repeats skip the tree walk and just bump the accumulated sums in place.
            if (this.previousColor.Equals(color))
            {
                if (this.previousNode == -1)
                {
                    this.previousColor = color;
                    Node.AddColor(this.rootIndex, color, this.maxColorBits, 0, this);
                }
                else
                {
                    Node.Increment(this.previousNode, color, this);
                }
            }
            else
            {
                this.previousColor = color;
                Node.AddColor(this.rootIndex, color, this.maxColorBits, 0, this);
            }
        }

        /// <summary>
        /// Reduces the tree to the requested palette size and emits the final palette entries.
        /// </summary>
        /// <param name="palette">The destination palette span.</param>
        /// <param name="paletteIndex">The running palette index.</param>
        public void Palettize(Span<TPixel> palette, ref short paletteIndex)
        {
            while (this.Leaves > this.maxColors)
            {
                this.Reduce();
            }

            this.Nodes[this.rootIndex].ConstructPalette(this, palette, ref paletteIndex);
        }

        /// <summary>
        /// Gets the palette index selected by the tree for the supplied color.
        /// </summary>
        /// <param name="color">The color to resolve.</param>
        /// <returns>The palette index represented by the best matching leaf in the reduced tree.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPaletteIndex(TPixel color)
            => this.Nodes[this.rootIndex].GetPaletteIndex(color.ToRgba32(), 0, this);

        /// <summary>
        /// Records the most recently touched leaf so repeated colors can bypass another descent.
        /// </summary>
        /// <param name="nodeIndex">The leaf node index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackPrevious(int nodeIndex)
            => this.previousNode = nodeIndex;

        /// <summary>
        /// Collapses the deepest currently reducible node into a single leaf.
        /// </summary>
        private void Reduce()
        {
            int index = this.maxColorBits - 1;
            while ((index > 0) && (this.reducibleNodes[index] == -1))
            {
                index--;
            }

            ref Node node = ref this.Nodes[this.reducibleNodes[index]];
            this.reducibleNodes[index] = node.NextReducibleIndex;
            node.Reduce(this);

            // If the last inserted leaf was merged away, the next repeated color must walk the tree again.
            this.previousNode = -1;
        }

        /// <summary>
        /// Allocates a node index from the free list or from the unused tail of the arena.
        /// </summary>
        /// <returns>The allocated node index, or <c>-1</c> if no node can be allocated.</returns>
        internal short AllocateNode()
        {
            if (this.freeIndices.Count > 0)
            {
                return this.freeIndices.Pop();
            }

            if (this.nextNode >= this.Nodes.Length)
            {
                return -1;
            }

            short newIndex = this.nextNode;
            this.nextNode++;
            return newIndex;
        }

        /// <summary>
        /// Returns a node index to the free list.
        /// </summary>
        /// <param name="index">The node index to recycle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FreeNode(short index)
        {
            this.freeIndices.Push(index);
            this.Leaves--;
        }

        /// <inheritdoc/>
        public void Dispose() => this.nodesOwner.Dispose();

        /// <summary>
        /// Represents one node in the hexadecatree node arena.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Node
        {
            public bool Leaf;
            public int PixelCount;
            public int Red;
            public int Green;
            public int Blue;
            public int Alpha;
            public short PaletteIndex;
            public short NextReducibleIndex;
            private InlineArray16<short> children;

            /// <summary>
            /// Gets the 16 child slots for this node.
            /// </summary>
            [UnscopedRef]
            public Span<short> Children => this.children;

            /// <summary>
            /// Initializes a node either as a leaf or as a reducible interior node.
            /// </summary>
            /// <param name="level">The depth of the node being initialized.</param>
            /// <param name="colorBits">The maximum tree depth.</param>
            /// <param name="tree">The owning tree.</param>
            /// <param name="index">The node index in the arena.</param>
            public void Initialize(int level, int colorBits, Hexadecatree tree, short index)
            {
                this.Leaf = level == colorBits;
                this.Red = 0;
                this.Green = 0;
                this.Blue = 0;
                this.Alpha = 0;
                this.PixelCount = 0;
                this.PaletteIndex = 0;
                this.NextReducibleIndex = -1;
                this.Children.Fill(-1);

                if (this.Leaf)
                {
                    tree.Leaves++;
                }
                else
                {
                    // Track reducible nodes per level so palette reduction can always collapse the deepest
                    // buckets first without scanning the entire arena.
                    this.NextReducibleIndex = tree.reducibleNodes[level];
                    tree.reducibleNodes[level] = index;
                }
            }

            /// <summary>
            /// Descends the tree for the supplied color, allocating nodes as needed until a leaf is reached.
            /// </summary>
            /// <param name="nodeIndex">The current node index.</param>
            /// <param name="color">The color being accumulated.</param>
            /// <param name="colorBits">The maximum tree depth.</param>
            /// <param name="level">The current depth.</param>
            /// <param name="tree">The owning tree.</param>
            public static void AddColor(int nodeIndex, Rgba32 color, int colorBits, int level, Hexadecatree tree)
            {
                ref Node node = ref tree.Nodes[nodeIndex];
                if (node.Leaf)
                {
                    Increment(nodeIndex, color, tree);
                    tree.TrackPrevious(nodeIndex);
                    return;
                }

                int index = GetColorIndex(color, level);
                Span<short> children = node.Children;
                short childIndex = children[index];

                if (childIndex == -1)
                {
                    childIndex = tree.AllocateNode();
                    if (childIndex == -1)
                    {
                        // If the arena is exhausted and no node can be reclaimed yet, fall back to
                        // accumulating into the current node instead of failing the insert outright.
                        Increment(nodeIndex, color, tree);
                        tree.TrackPrevious(nodeIndex);
                        return;
                    }

                    ref Node child = ref tree.Nodes[childIndex];
                    child.Initialize(level + 1, colorBits, tree, childIndex);
                    children[index] = childIndex;
                }

                // Keep descending until we reach the leaf bucket that should accumulate this sample.
                AddColor(childIndex, color, colorBits, level + 1, tree);
            }

            /// <summary>
            /// Adds the supplied color sample to an existing node's running sums.
            /// </summary>
            /// <param name="nodeIndex">The node index to update.</param>
            /// <param name="color">The color sample being accumulated.</param>
            /// <param name="tree">The owning tree.</param>
            public static void Increment(int nodeIndex, Rgba32 color, Hexadecatree tree)
            {
                ref Node node = ref tree.Nodes[nodeIndex];
                node.PixelCount++;
                node.Red += color.R;
                node.Green += color.G;
                node.Blue += color.B;
                node.Alpha += color.A;
            }

            /// <summary>
            /// Merges all child nodes into this node and turns it into a leaf.
            /// </summary>
            /// <param name="tree">The owning tree.</param>
            public void Reduce(Hexadecatree tree)
            {
                // If already a leaf, do nothing.
                if (this.Leaf)
                {
                    return;
                }

                // Now merge the (presumably reduced) children.
                int pixelCount = 0;
                int sumRed = 0;
                int sumGreen = 0;
                int sumBlue = 0;
                int sumAlpha = 0;
                Span<short> children = this.Children;

                for (int i = 0; i < children.Length; i++)
                {
                    short childIndex = children[i];
                    if (childIndex != -1)
                    {
                        ref Node child = ref tree.Nodes[childIndex];
                        int pixels = child.PixelCount;
                        sumRed += child.Red;
                        sumGreen += child.Green;
                        sumBlue += child.Blue;
                        sumAlpha += child.Alpha;
                        pixelCount += pixels;

                        children[i] = -1;
                        tree.FreeNode(childIndex);
                    }
                }

                if (pixelCount > 0)
                {
                    this.Red = sumRed;
                    this.Green = sumGreen;
                    this.Blue = sumBlue;
                    this.Alpha = sumAlpha;
                    this.PixelCount = pixelCount;
                }
                else
                {
                    this.Red = this.Green = this.Blue = this.Alpha = 0;
                    this.PixelCount = 0;
                }

                this.Leaf = true;
                tree.Leaves++;
            }

            /// <summary>
            /// Traverses the reduced tree and emits one palette color per leaf.
            /// </summary>
            /// <param name="tree">The owning tree.</param>
            /// <param name="palette">The destination palette span.</param>
            /// <param name="paletteIndex">The running palette index.</param>
            public void ConstructPalette(Hexadecatree tree, Span<TPixel> palette, ref short paletteIndex)
            {
                if (this.Leaf)
                {
                    Vector4 sum = new(this.Red, this.Green, this.Blue, this.Alpha);
                    Vector4 offset = new(this.PixelCount >> 1);
                    Vector4 vector = Vector4.Clamp(
                        (sum + offset) / this.PixelCount,
                        Vector4.Zero,
                        new Vector4(255));

                    if (vector.W < tree.transparencyThreshold255)
                    {
                        vector = Vector4.Zero;
                    }

                    palette[paletteIndex] = TPixel.FromRgba32(new Rgba32((byte)vector.X, (byte)vector.Y, (byte)vector.Z, (byte)vector.W));
                    this.PaletteIndex = paletteIndex++;
                }
                else
                {
                    Span<short> children = this.Children;
                    for (int i = 0; i < children.Length; i++)
                    {
                        int childIndex = children[i];
                        if (childIndex != -1)
                        {
                            tree.Nodes[childIndex].ConstructPalette(tree, palette, ref paletteIndex);
                        }
                    }
                }
            }

            /// <summary>
            /// Resolves the palette index represented by this node for the supplied color.
            /// </summary>
            /// <param name="color">The color to resolve.</param>
            /// <param name="level">The current tree depth.</param>
            /// <param name="tree">The owning tree.</param>
            /// <returns>The palette index for the best reachable leaf, or <c>-1</c> if no leaf can be reached.</returns>
            public int GetPaletteIndex(Rgba32 color, int level, Hexadecatree tree)
            {
                if (this.Leaf)
                {
                    return this.PaletteIndex;
                }

                int colorIndex = GetColorIndex(color, level);
                Span<short> children = this.Children;
                int childIndex = children[colorIndex];
                if (childIndex != -1)
                {
                    return tree.Nodes[childIndex].GetPaletteIndex(color, level + 1, tree);
                }

                // After reductions the exact branch can disappear, so fall back to the first reachable descendant leaf.
                for (int i = 0; i < children.Length; i++)
                {
                    childIndex = children[i];
                    if (childIndex != -1)
                    {
                        int childPaletteIndex = tree.Nodes[childIndex].GetPaletteIndex(color, level + 1, tree);
                        if (childPaletteIndex != -1)
                        {
                            return childPaletteIndex;
                        }
                    }
                }

                return -1;
            }

            /// <summary>
            /// Computes the child slot for a color at the supplied tree level.
            /// </summary>
            /// <param name="color">The color being routed.</param>
            /// <param name="level">The tree depth whose bit plane should be sampled.</param>
            /// <returns>The child slot index for the color at the supplied level.</returns>
            /// <remarks>
            /// For fully opaque mid-tone colors the tree ignores alpha and routes on RGB only, preserving more branch
            /// resolution for visible color detail. For transparent, dark, and light colors it includes alpha as the
            /// most significant routing bit so opacity changes can form their own branches.
            /// </remarks>
            public static int GetColorIndex(Rgba32 color, int level)
            {
                // Sample one bit plane per level, starting at the most significant bit and moving downward.
                int shift = 7 - level;
                byte mask = (byte)(1 << shift);

                // Use BT.709 luminance as a cheap brightness estimate for deciding whether alpha carries
                // useful information at this level for fully opaque colors.
                int luminance = ColorNumerics.Get8BitBT709Luminance(color.R, color.G, color.B);

                // Scale the brightness thresholds with depth so deeper levels become stricter about when
                // to spend a branch bit on alpha instead of RGB detail.
                int darkThreshold = 128 >> level;
                int lightThreshold = 255 - (128 >> level);

                if (color.A == 255 && luminance > darkThreshold && luminance < lightThreshold)
                {
                    // Fully opaque mid-tone colors route on RGB only, which preserves more visible color
                    // resolution because alpha would contribute no extra separation here.
                    int rBits = ((color.R & mask) >> shift) << 2;
                    int gBits = ((color.G & mask) >> shift) << 1;
                    int bBits = (color.B & mask) >> shift;
                    return rBits | gBits | bBits;
                }
                else
                {
                    // Transparent, dark, and light colors include alpha as the high routing bit so opacity
                    // changes can form distinct buckets alongside RGB differences.
                    int aBits = ((color.A & mask) >> shift) << 3;
                    int rBits = ((color.R & mask) >> shift) << 2;
                    int gBits = ((color.G & mask) >> shift) << 1;
                    int bBits = (color.B & mask) >> shift;
                    return aBits | rBits | gBits | bBits;
                }
            }
        }
    }
}
