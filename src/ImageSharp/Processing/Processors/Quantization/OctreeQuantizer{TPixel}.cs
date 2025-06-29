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
/// Encapsulates methods to calculate the color palette if an image using an Octree pattern.
/// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
// See https://github.com/dotnet/roslyn-analyzers/issues/6151
public struct OctreeQuantizer<TPixel> : IQuantizer<TPixel>
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int maxColors;
    private readonly int bitDepth;
    private readonly Octree octree;
    private readonly IMemoryOwner<TPixel> paletteOwner;
    private ReadOnlyMemory<TPixel> palette;
    private PixelMap<TPixel>? pixelMap;
    private readonly bool isDithering;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OctreeQuantizer{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public OctreeQuantizer(Configuration configuration, QuantizerOptions options)
    {
        this.Configuration = configuration;
        this.Options = options;

        this.maxColors = this.Options.MaxColors;
        this.bitDepth = Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(this.maxColors), 1, 8);
        this.octree = new Octree(configuration, this.bitDepth, this.maxColors, this.Options.TransparencyThreshold);
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
        PixelRowDelegate pixelRowDelegate = new(this.octree);
        QuantizerUtilities.AddPaletteColors<OctreeQuantizer<TPixel>, TPixel, Rgba32, PixelRowDelegate>(
            ref Unsafe.AsRef(in this),
            in pixelRegion,
            in pixelRowDelegate);
    }

    private void ResolvePalette()
    {
        short paletteIndex = 0;
        Span<TPixel> paletteSpan = this.paletteOwner.GetSpan();

        this.octree.Palettize(paletteSpan, ref paletteIndex);
        ReadOnlyMemory<TPixel> result = this.paletteOwner.Memory[..paletteSpan.Length];

        if (this.isDithering)
        {
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
        // Due to the addition of new colors by dithering that are not part of the original histogram,
        // the octree nodes might not match the correct color.
        // In this case, we must use the pixel map to get the closest color.
        if (this.isDithering)
        {
            return (byte)this.pixelMap!.GetClosestColor(color, out match);
        }

        ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.palette.Span);

        int index = this.octree.GetPaletteIndex(color);
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
            this.octree.Dispose();
        }
    }

    private readonly struct PixelRowDelegate : IQuantizingPixelRowDelegate<Rgba32>
    {
        private readonly Octree octree;

        public PixelRowDelegate(Octree octree) => this.octree = octree;

        public void Invoke(ReadOnlySpan<Rgba32> row, int rowIndex) => this.octree.AddColors(row);
    }

    /// <summary>
    /// A hexadecatree-based color quantization structure used for fast color distance lookups and palette generation.
    /// This tree maintains a fixed pool of nodes (capacity 4096) where each node can have up to 16 children, stores
    /// color accumulation data, and supports dynamic node allocation and reduction. It offers near-constant-time insertions
    /// and lookups while consuming roughly 240 KB for the node pool.
    /// </summary>
    internal sealed class Octree : IDisposable
    {
        // The memory allocator.
        private readonly MemoryAllocator allocator;

        // Pooled buffer for OctreeNodes.
        private readonly IMemoryOwner<OctreeNode> nodesOwner;

        // Reducible nodes: one per level; we use an integer index; -1 means “no node.”
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
        /// Initializes a new instance of the <see cref="Octree"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
        /// <param name="maxColorBits">The maximum number of significant bits in the image.</param>
        /// <param name="maxColors">The maximum number of colors to allow in the palette.</param>
        /// <param name="transparencyThreshold">The threshold for transparent colors.</param>
        public Octree(
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
            this.allocator = configuration.MemoryAllocator;
            this.nodesOwner = this.allocator.Allocate<OctreeNode>(capacity, AllocationOptions.Clean);

            // Create the reducible nodes array (one per level 0 .. maxColorBits-1).
            this.reducibleNodes = new short[this.maxColorBits];
            this.reducibleNodes.AsSpan().Fill(-1);

            // Reserve index 0 for the root.
            this.rootIndex = 0;
            ref OctreeNode root = ref this.Nodes[this.rootIndex];
            root.Initialize(0, this.maxColorBits, this, this.rootIndex);
        }

        /// <summary>
        /// Gets or sets the number of leaves in the tree.
        /// </summary>
        public int Leaves { get; set; }

        /// <summary>
        /// Gets the full collection of nodes as a span.
        /// </summary>
        internal Span<OctreeNode> Nodes => this.nodesOwner.Memory.Span;

        /// <summary>
        /// Adds a span of colors to the octree.
        /// </summary>
        /// <param name="row">A span of color values to be added.</param>
        public void AddColors(ReadOnlySpan<Rgba32> row)
        {
            for (int x = 0; x < row.Length; x++)
            {
                this.AddColor(row[x]);
            }
        }

        /// <summary>
        /// Add a color to the Octree.
        /// </summary>
        /// <param name="color">The color to add.</param>
        private void AddColor(Rgba32 color)
        {
            // Ensure that the tree is not already full.
            if (this.nextNode >= this.Nodes.Length && this.freeIndices.Count == 0)
            {
                while (this.Leaves > this.maxColors)
                {
                    this.Reduce();
                }
            }

            // If the color is the same as the previous color, increment the node.
            // Otherwise, add a new node.
            if (this.previousColor.Equals(color))
            {
                if (this.previousNode == -1)
                {
                    this.previousColor = color;
                    OctreeNode.AddColor(this.rootIndex, color, this.maxColorBits, 0, this);
                }
                else
                {
                    OctreeNode.Increment(this.previousNode, color, this);
                }
            }
            else
            {
                this.previousColor = color;
                OctreeNode.AddColor(this.rootIndex, color, this.maxColorBits, 0, this);
            }
        }

        /// <summary>
        /// Construct the palette from the octree.
        /// </summary>
        /// <param name="palette">The palette to construct.</param>
        /// <param name="paletteIndex">The current palette index.</param>
        public void Palettize(Span<TPixel> palette, ref short paletteIndex)
        {
            while (this.Leaves > this.maxColors)
            {
                this.Reduce();
            }

            this.Nodes[this.rootIndex].ConstructPalette(this, palette, ref paletteIndex);
        }

        /// <summary>
        /// Get the palette index for the passed color.
        /// </summary>
        /// <param name="color">The color to get the palette index for.</param>
        /// <returns>The <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPaletteIndex(TPixel color)
            => this.Nodes[this.rootIndex].GetPaletteIndex(color.ToRgba32(), 0, this);

        /// <summary>
        /// Track the previous node and color.
        /// </summary>
        /// <param name="nodeIndex">The node index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackPrevious(int nodeIndex)
            => this.previousNode = nodeIndex;

        /// <summary>
        /// Reduce the depth of the tree.
        /// </summary>
        private void Reduce()
        {
            // Find the deepest level containing at least one reducible node
            int index = this.maxColorBits - 1;
            while ((index > 0) && (this.reducibleNodes[index] == -1))
            {
                index--;
            }

            // Reduce the node most recently added to the list at level 'index'
            ref OctreeNode node = ref this.Nodes[this.reducibleNodes[index]];
            this.reducibleNodes[index] = node.NextReducibleIndex;

            // Decrement the leaf count after reducing the node
            node.Reduce(this);

            // And just in case I've reduced the last color to be added, and the next color to
            // be added is the same, invalidate the previousNode...
            this.previousNode = -1;
        }

        // Allocate a new OctreeNode from the pooled buffer.
        // First check the freeIndices stack.
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
        /// Free a node index, making it available for re-allocation.
        /// </summary>
        /// <param name="index">The index to free.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FreeNode(short index)
        {
            this.freeIndices.Push(index);
            this.Leaves--;
        }

        /// <inheritdoc/>
        public void Dispose() => this.nodesOwner.Dispose();

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct OctreeNode
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

            [UnscopedRef]
            public Span<short> Children => this.children;

            /// <summary>
            /// Initialize the <see cref="OctreeNode"/>.
            /// </summary>
            /// <param name="level">The level of the node.</param>
            /// <param name="colorBits">The number of significant color bits in the image.</param>
            /// <param name="octree">The parent octree.</param>
            /// <param name="index">The index of the node.</param>
            public void Initialize(int level, int colorBits, Octree octree, short index)
            {
                // Construct the new node.
                this.Leaf = level == colorBits;
                this.Red = 0;
                this.Green = 0;
                this.Blue = 0;
                this.Alpha = 0;
                this.PixelCount = 0;
                this.PaletteIndex = 0;
                this.NextReducibleIndex = -1;

                // Always clear the Children array.
                this.Children.Fill(-1);

                if (this.Leaf)
                {
                    octree.Leaves++;
                }
                else
                {
                    // Add this node to the reducible nodes list for its level.
                    this.NextReducibleIndex = octree.reducibleNodes[level];
                    octree.reducibleNodes[level] = index;
                }
            }

            /// <summary>
            /// Add a color to the Octree.
            /// </summary>
            /// <param name="nodeIndex">The node index.</param>
            /// <param name="color">The color to add.</param>
            /// <param name="colorBits">The number of significant color bits in the image.</param>
            /// <param name="level">The level of the node.</param>
            /// <param name="octree">The parent octree.</param>
            public static void AddColor(int nodeIndex, Rgba32 color, int colorBits, int level, Octree octree)
            {
                ref OctreeNode node = ref octree.Nodes[nodeIndex];
                if (node.Leaf)
                {
                    Increment(nodeIndex, color, octree);
                    octree.TrackPrevious(nodeIndex);
                }
                else
                {
                    int index = GetColorIndex(color, level);
                    short childIndex;

                    Span<short> children = node.Children;
                    childIndex = children[index];

                    if (childIndex == -1)
                    {
                        childIndex = octree.AllocateNode();

                        if (childIndex == -1)
                        {
                            // No room in the tree, so increment the count and return.
                            Increment(nodeIndex, color, octree);
                            octree.TrackPrevious(nodeIndex);
                            return;
                        }

                        ref OctreeNode child = ref octree.Nodes[childIndex];
                        child.Initialize(level + 1, colorBits, octree, childIndex);
                        children[index] = childIndex;
                    }

                    AddColor(childIndex, color, colorBits, level + 1, octree);
                }
            }

            /// <summary>
            /// Increment the color components of this node.
            /// </summary>
            /// <param name="nodeIndex">The node index.</param>
            /// <param name="color">The color to increment by.</param>
            /// <param name="octree">The parent octree.</param>
            public static void Increment(int nodeIndex, Rgba32 color, Octree octree)
            {
                ref OctreeNode node = ref octree.Nodes[nodeIndex];
                node.PixelCount++;
                node.Red += color.R;
                node.Green += color.G;
                node.Blue += color.B;
                node.Alpha += color.A;
            }

            /// <summary>
            /// Reduce this node by ensuring its children are all reduced (i.e. leaves) and then merging their data.
            /// </summary>
            /// <param name="octree">The parent octree.</param>
            public void Reduce(Octree octree)
            {
                // If already a leaf, do nothing.
                if (this.Leaf)
                {
                    return;
                }

                // Now merge the (presumably reduced) children.
                int pixelCount = 0;
                int sumRed = 0, sumGreen = 0, sumBlue = 0, sumAlpha = 0;
                Span<short> children = this.Children;
                for (int i = 0; i < children.Length; i++)
                {
                    short childIndex = children[i];
                    if (childIndex != -1)
                    {
                        ref OctreeNode child = ref octree.Nodes[childIndex];
                        int pixels = child.PixelCount;

                        sumRed += child.Red;
                        sumGreen += child.Green;
                        sumBlue += child.Blue;
                        sumAlpha += child.Alpha;
                        pixelCount += pixels;

                        // Free the child immediately.
                        children[i] = -1;
                        octree.FreeNode(childIndex);
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
                octree.Leaves++;
            }

            /// <summary>
            /// Traverse the tree to construct the palette.
            /// </summary>
            /// <param name="octree">The parent octree.</param>
            /// <param name="palette">The palette to construct.</param>
            /// <param name="paletteIndex">The current palette index.</param>
            public void ConstructPalette(Octree octree, Span<TPixel> palette, ref short paletteIndex)
            {
                if (this.Leaf)
                {
                    Vector4 sum = new(this.Red, this.Green, this.Blue, this.Alpha);
                    Vector4 offset = new(this.PixelCount >> 1);
                    Vector4 vector = Vector4.Clamp(
                        (sum + offset) / this.PixelCount,
                        Vector4.Zero,
                        new Vector4(255));

                    if (vector.W < octree.transparencyThreshold255)
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
                            octree.Nodes[childIndex].ConstructPalette(octree, palette, ref paletteIndex);
                        }
                    }
                }
            }

            /// <summary>
            /// Get the palette index for the passed color.
            /// </summary>
            /// <param name="color">The color to get the palette index for.</param>
            /// <param name="level">The level of the node.</param>
            /// <param name="octree">The parent octree.</param>
            public int GetPaletteIndex(Rgba32 color, int level, Octree octree)
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
                    return octree.Nodes[childIndex].GetPaletteIndex(color, level + 1, octree);
                }

                for (int i = 0; i < children.Length; i++)
                {
                    childIndex = children[i];
                    if (childIndex != -1)
                    {
                        int childPaletteIndex = octree.Nodes[childIndex].GetPaletteIndex(color, level + 1, octree);
                        if (childPaletteIndex != -1)
                        {
                            return childPaletteIndex;
                        }
                    }
                }

                return -1;
            }

            /// <summary>
            /// Gets the color index at the given level.
            /// </summary>
            /// <param name="color">The color to get the index for.</param>
            /// <param name="level">The level to get the index at.</param>
            public static int GetColorIndex(Rgba32 color, int level)
            {
                // Determine how many bits to shift based on the current tree level.
                // At level 0, shift = 7; as level increases, the shift decreases.
                int shift = 7 - level;
                byte mask = (byte)(1 << shift);

                // Compute the luminance of the RGB components using the BT.709 standard.
                // This gives a measure of brightness for the color.
                int luminance = ColorNumerics.Get8BitBT709Luminance(color.R, color.G, color.B);

                // Define thresholds for determining when to include the alpha bit in the index.
                // The thresholds are scaled according to the current level.
                // 128 is the midpoint of the 8-bit range (0–255), so shifting it right by 'level'
                // produces a threshold that scales with the color cube subdivision.
                int darkThreshold = 128 >> level;

                // The light threshold is set symmetrically: 255 minus the scaled midpoint.
                int lightThreshold = 255 - (128 >> level);

                // If the pixel is fully opaque and its brightness falls between the dark and light thresholds,
                // ignore the alpha channel to maximize RGB resolution.
                // Otherwise (if the pixel is dark, light, or semi-transparent), include the alpha bit
                // to preserve any gradient that may be present.
                if (color.A == 255 && luminance > darkThreshold && luminance < lightThreshold)
                {
                    // Extract one bit each from R, G, and B channels and combine them into a 3-bit index.
                    int rBits = ((color.R & mask) >> shift) << 2;
                    int gBits = ((color.G & mask) >> shift) << 1;
                    int bBits = (color.B & mask) >> shift;
                    return rBits | gBits | bBits;
                }
                else
                {
                    // Extract one bit from each channel including alpha (alpha becomes the most significant bit).
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
