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
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "https://github.com/dotnet/roslyn-analyzers/issues/6151")]
public struct OctreeQuantizer<TPixel> : IQuantizer<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int maxColors;
    private readonly int bitDepth;
    private readonly Octree octree;
    private readonly IMemoryOwner<TPixel> paletteOwner;
    private ReadOnlyMemory<TPixel> palette;
    private EuclideanPixelMap<TPixel>? pixelMap;
    private readonly bool isDithering;
    private readonly short transparencyThreshold;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OctreeQuantizer{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public OctreeQuantizer(Configuration configuration, QuantizerOptions options)
    {
        this.Configuration = configuration;
        this.Options = options;

        this.maxColors = this.Options.MaxColors;
        this.transparencyThreshold = (short)(this.Options.TransparencyThreshold * 255);
        this.bitDepth = Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(this.maxColors), 1, 8);
        this.octree = new Octree(this.bitDepth, this.maxColors, this.transparencyThreshold, configuration.MemoryAllocator);
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
        using IMemoryOwner<Rgba32> buffer = this.Configuration.MemoryAllocator.Allocate<Rgba32>(pixelRegion.Width);
        Span<Rgba32> bufferSpan = buffer.GetSpan();

        // Loop through each row
        for (int y = 0; y < pixelRegion.Height; y++)
        {
            Span<TPixel> row = pixelRegion.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToRgba32(this.Configuration, row, bufferSpan);

            Octree octree = this.octree;
            int transparencyThreshold = this.transparencyThreshold;
            for (int x = 0; x < bufferSpan.Length; x++)
            {
                // Add the color to the Octree
                octree.AddColor(bufferSpan[x]);
            }
        }
    }

    private void ResolvePalette()
    {
        short paletteIndex = 0;
        Span<TPixel> paletteSpan = this.paletteOwner.GetSpan();

        this.octree.Palettize(paletteSpan, ref paletteIndex);
        ReadOnlyMemory<TPixel> result = this.paletteOwner.Memory[..paletteSpan.Length];

        if (this.isDithering)
        {
            this.pixelMap = new EuclideanPixelMap<TPixel>(this.Configuration, result);
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
            return (byte)this.pixelMap!.GetClosestColor(color, out match, this.transparencyThreshold);
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

    /// <summary>
    /// A hexadecatree-based color quantization structure used for fast color distance lookups and palette generation.
    /// This tree maintains a fixed pool of nodes (capacity 4096) where each node can have up to 16 children, stores
    /// color accumulation data, and supports dynamic node allocation and reduction. It offers near-constant-time insertions
    /// and lookups while consuming roughly 240 KB for the node pool.
    /// </summary>
    internal sealed class Octree : IDisposable
    {
        // Pooled buffer for OctreeNodes.
        private readonly IMemoryOwner<OctreeNode> nodesOwner;

        // Reducible nodes: one per level; we use an integer index; -1 means “no node.”
        private readonly short[] reducibleNodes;

        // Maximum number of allowable colors.
        private readonly int maxColors;

        // Maximum significant bits.
        private readonly int maxColorBits;

        // The threshold for transparent colors.
        private readonly short transparencyThreshold;

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
        /// <param name="maxColorBits">The maximum number of significant bits in the image.</param>
        /// <param name="maxColors">The maximum number of colors to allow in the palette.</param>
        /// <param name="transparencyThreshold">The threshold for transparent colors.</param>
        /// <param name="allocator">The memory allocator.</param>
        public Octree(int maxColorBits, int maxColors, short transparencyThreshold, MemoryAllocator allocator)
        {
            this.maxColorBits = maxColorBits;
            this.maxColors = maxColors;
            this.transparencyThreshold = transparencyThreshold;
            this.Leaves = 0;
            this.previousNode = -1;
            this.previousColor = default;

            // Allocate a conservative buffer for nodes.
            const int capacity = 4096;
            this.nodesOwner = allocator.Allocate<OctreeNode>(capacity, AllocationOptions.Clean);

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
        /// Add a color to the Octree.
        /// </summary>
        /// <param name="color">The color to add.</param>
        public void AddColor(Rgba32 color)
        {
            // Ensure that the tree is not already full.
            if (this.nextNode >= this.Nodes.Length && this.freeIndices.Count == 0)
            {
                while (this.Leaves > this.maxColors)
                {
                    this.Reduce();
                }
            }

            if (color.A < this.transparencyThreshold)
            {
                color = default;
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

                    if (vector.W < octree.transparencyThreshold)
                    {
                        vector = default;
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
                if (color.A < octree.transparencyThreshold)
                {
                    color = default;
                }

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

    /// <summary>
    /// Class which does the actual quantization.
    /// </summary>
    private sealed class Octree2
    {
        /// <summary>
        /// The root of the Octree2
        /// </summary>
        private readonly OctreeNode root;

        /// <summary>
        /// Maximum number of significant bits in the image
        /// </summary>
        private readonly int maxColorBits;

        /// <summary>
        /// The threshold for transparent colors.
        /// </summary>
        private readonly int transparencyThreshold;

        /// <summary>
        /// Store the last node quantized
        /// </summary>
        private OctreeNode? previousNode;

        /// <summary>
        /// Cache the previous color quantized
        /// </summary>
        private Rgba32 previousColor;

        /// <summary>
        /// Initializes a new instance of the <see cref="Octree2"/> class.
        /// </summary>
        /// <param name="maxColorBits">
        /// The maximum number of significant bits in the image
        /// </param>
        /// <param name="transparencyThreshold">The threshold for transparent colors.</param>
        public Octree2(int maxColorBits, int transparencyThreshold)
        {
            this.maxColorBits = maxColorBits;
            this.transparencyThreshold = transparencyThreshold;
            this.Leaves = 0;
            this.ReducibleNodes = new OctreeNode[9];
            this.root = new OctreeNode(0, this.maxColorBits, this);
            this.previousColor = default;
            this.previousNode = null;
        }

        /// <summary>
        /// Gets or sets the number of leaves in the tree
        /// </summary>
        public int Leaves
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;

            [MethodImpl(InliningOptions.ShortMethod)]
            set;
        }

        /// <summary>
        /// Gets the array of reducible nodes
        /// </summary>
        private OctreeNode?[] ReducibleNodes
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }

        /// <summary>
        /// Add a given color value to the Octree2
        /// </summary>
        /// <param name="color">The color to add.</param>
        public void AddColor(Rgba32 color)
        {
            if (color.A < this.transparencyThreshold)
            {
                color.A = 0;
            }

            // Check if this request is for the same color as the last
            if (this.previousColor.Equals(color))
            {
                // If so, check if I have a previous node setup.
                if (this.previousNode is null)
                {
                    this.previousColor = color;
                    this.root.AddColor(color, this.maxColorBits, 0, this);
                }
                else
                {
                    // Just update the previous node
                    this.previousNode.Increment(color, this);
                }
            }
            else
            {
                this.previousColor = color;
                this.root.AddColor(color, this.maxColorBits, 0, this);
            }
        }

        /// <summary>
        /// Convert the nodes in the Octree2 to a palette with a maximum of colorCount colors
        /// </summary>
        /// <param name="palette">The palette to fill.</param>
        /// <param name="colorCount">The maximum number of colors</param>
        /// <param name="paletteIndex">The palette index, used to calculate the final size of the palette.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Palettize(Span<TPixel> palette, int colorCount, ref int paletteIndex)
        {
            while (this.Leaves > colorCount)
            {
                this.Reduce();
            }

            this.root.ConstructPalette(palette, ref paletteIndex);
        }

        /// <summary>
        /// Get the palette index for the passed color
        /// </summary>
        /// <param name="color">The color to match.</param>
        /// <returns>
        /// The <see cref="int"/> index.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetPaletteIndex(TPixel color)
            => this.root.GetPaletteIndex(color.ToRgba32(), 0);

        /// <summary>
        /// Keep track of the previous node that was quantized
        /// </summary>
        /// <param name="node">
        /// The node last quantized
        /// </param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void TrackPrevious(OctreeNode node) => this.previousNode = node;

        /// <summary>
        /// Reduce the depth of the tree
        /// </summary>
        private void Reduce()
        {
            // Find the deepest level containing at least one reducible node
            int index = this.maxColorBits - 1;
            while ((index > 0) && (this.ReducibleNodes[index] is null))
            {
                index--;
            }

            // Reduce the node most recently added to the list at level 'index'
            OctreeNode node = this.ReducibleNodes[index]!;
            this.ReducibleNodes[index] = node.NextReducible;

            // Decrement the leaf count after reducing the node
            this.Leaves -= node.Reduce(this);

            // And just in case I've reduced the last color to be added, and the next color to
            // be added is the same, invalidate the previousNode...
            this.previousNode = null;
        }

        /// <summary>
        /// Class which encapsulates each node in the tree
        /// </summary>
        public sealed class OctreeNode
        {
            /// <summary>
            /// Pointers to any child nodes
            /// </summary>
            private readonly OctreeNode?[]? children;

            /// <summary>
            /// Flag indicating that this is a leaf node
            /// </summary>
            private bool leaf;

            /// <summary>
            /// Number of pixels in this node
            /// </summary>
            private int pixelCount;

            /// <summary>
            /// Red component
            /// </summary>
            private int red;

            /// <summary>
            /// Green Component
            /// </summary>
            private int green;

            /// <summary>
            /// Blue component
            /// </summary>
            private int blue;

            /// <summary>
            /// Alpha component
            /// </summary>
            private int alpha;

            /// <summary>
            /// The index of this node in the palette
            /// </summary>
            private int paletteIndex;

            /// <summary>
            /// Initializes a new instance of the <see cref="OctreeNode"/> class.
            /// </summary>
            /// <param name="level">The level in the tree = 0 - 7.</param>
            /// <param name="colorBits">The number of significant color bits in the image.</param>
            /// <param name="octree">The tree to which this node belongs.</param>
            public OctreeNode(int level, int colorBits, Octree2 octree)
            {
                // Construct the new node
                this.leaf = level == colorBits;

                this.red = this.green = this.blue = this.alpha = 0;
                this.pixelCount = 0;

                // If a leaf, increment the leaf count
                if (this.leaf)
                {
                    octree.Leaves++;
                    this.NextReducible = null;
                    this.children = null;
                }
                else
                {
                    // Otherwise add this to the reducible nodes
                    this.NextReducible = octree.ReducibleNodes[level];
                    octree.ReducibleNodes[level] = this;
                    this.children = new OctreeNode[16];
                }
            }

            /// <summary>
            /// Gets the next reducible node
            /// </summary>
            public OctreeNode? NextReducible
            {
                [MethodImpl(InliningOptions.ShortMethod)]
                get;
            }

            /// <summary>
            /// Add a color into the tree
            /// </summary>
            /// <param name="color">The color to add.</param>
            /// <param name="colorBits">The number of significant color bits.</param>
            /// <param name="level">The level in the tree.</param>
            /// <param name="octree">The tree to which this node belongs.</param>
            public void AddColor(Rgba32 color, int colorBits, int level, Octree2 octree)
            {
                // Update the color information if this is a leaf
                if (this.leaf)
                {
                    this.Increment(color, octree);

                    // Setup the previous node
                    octree.TrackPrevious(this);
                }
                else
                {
                    // Go to the next level down in the tree
                    int index = GetColorIndex(color, level);

                    OctreeNode? child = this.children![index];
                    if (child is null)
                    {
                        // Create a new child node and store it in the array
                        child = new OctreeNode(level + 1, colorBits, octree);
                        this.children[index] = child;
                    }

                    // Add the color to the child node
                    child.AddColor(color, colorBits, level + 1, octree);
                }
            }

            /// <summary>
            /// Reduce this node by removing all of its children.
            /// </summary>
            /// <returns>The number of leaves removed</returns>
            /// <param name="octree">The tree to which this node belongs.</param>
            public int Reduce(Octree2 octree)
            {
                if (this.leaf)
                {
                    return 1;
                }

                int childNodes = 0;
                int sumRed = 0, sumGreen = 0, sumBlue = 0, sumAlpha = 0, pixelCount = 0;

                // Loop through all children.
                for (int index = 0; index < this.children!.Length; index++)
                {
                    OctreeNode? child = this.children[index];
                    if (child != null)
                    {
                        childNodes++;

                        sumRed += child.red;
                        sumGreen += child.green;
                        sumBlue += child.blue;
                        sumAlpha += child.alpha;
                        pixelCount += child.pixelCount;

                        // Remove the child reference.
                        this.children[index] = null;
                    }
                }

                if (pixelCount > 0)
                {
                    int offset = pixelCount >> 1;
                    this.red = sumRed;
                    this.green = sumGreen;
                    this.blue = sumBlue;
                    this.alpha = ((sumAlpha + offset) / pixelCount < octree.transparencyThreshold) ? 0 : sumAlpha;
                    this.pixelCount = pixelCount;
                }
                else
                {
                    this.red = this.green = this.blue = this.alpha = 0;
                    this.pixelCount = 0;
                }

                // Convert this node into a leaf.
                this.leaf = true;

                // Return the number of nodes merged (for decrementing the leaf count).
                return childNodes - 1;
            }

            /// <summary>
            /// Traverse the tree, building up the color palette
            /// </summary>
            /// <param name="palette">The palette</param>
            /// <param name="index">The current palette index</param>
            [MethodImpl(InliningOptions.ColdPath)]
            public void ConstructPalette(Span<TPixel> palette, ref int index)
            {
                if (this.leaf)
                {
                    // Set the color of the palette entry
                    Vector4 sum = new(this.red, this.green, this.blue, this.alpha);
                    Vector4 offset = new(this.pixelCount >> 1);

                    Vector4 vector = Vector4.Clamp(
                        (sum + offset) / this.pixelCount,
                        Vector4.Zero,
                        new Vector4(255));

                    palette[index] = TPixel.FromRgba32(new Rgba32((byte)vector.X, (byte)vector.Y, (byte)vector.Z, (byte)vector.W));

                    // Consume the next palette index
                    this.paletteIndex = index++;
                }
                else
                {
                    // Loop through children looking for leaves
                    for (int i = 0; i < this.children!.Length; i++)
                    {
                        this.children[i]?.ConstructPalette(palette, ref index);
                    }
                }
            }

            /// <summary>
            /// Return the palette index for the passed color
            /// </summary>
            /// <param name="pixel">The pixel data.</param>
            /// <param name="level">The level.</param>
            /// <returns>
            /// The <see cref="int"/> representing the index of the pixel in the palette.
            /// </returns>
            [MethodImpl(InliningOptions.ColdPath)]
            public int GetPaletteIndex(Rgba32 pixel, int level)
            {
                if (this.leaf)
                {
                    return this.paletteIndex;
                }

                int colorIndex = GetColorIndex(pixel, level);
                OctreeNode? child = this.children![colorIndex];

                int index = -1;
                if (child != null)
                {
                    index = child.GetPaletteIndex(pixel, level + 1);
                }
                else
                {
                    // Check other children.
                    for (int i = 0; i < this.children.Length; i++)
                    {
                        child = this.children[i];
                        if (child != null)
                        {
                            int childIndex = child.GetPaletteIndex(pixel, level + 1);
                            if (childIndex != -1)
                            {
                                return childIndex;
                            }
                        }
                    }
                }

                return index;
            }

            /// <summary>
            /// Gets the color index at the given level.
            /// </summary>
            /// <param name="color">The color.</param>
            /// <param name="level">The node level.</param>
            /// <returns>The <see cref="int"/> index.</returns>
            [MethodImpl(InliningOptions.ShortMethod)]
            private static int GetColorIndex(Rgba32 color, int level)
            {
                int shift = 7 - level;
                byte mask = (byte)(1 << shift);

                // Compute luminance of the RGB channels.
                int luminance = ColorNumerics.Get8BitBT709Luminance(color.R, color.G, color.B);

                // Shift the threshold (arbitrary) right by the current level.
                // This allows us to partition the RGB space into smaller and smaller cubes
                // with increasing accuracy for the alpha component.
                int darkThreshold = 24 >> level;

                // For fully opaque and bright pixels, ignore the alpha bit to achieve finer RGB partitioning.
                // For dark pixels, include the alpha bit so that dark drop shadows remain distinct.
                if (color.A == 255 && luminance > darkThreshold)
                {
                    int rBits = ((color.R & mask) >> shift) << 2;
                    int gBits = ((color.G & mask) >> shift) << 1;
                    int bBits = (color.B & mask) >> shift;
                    return rBits | gBits | bBits;
                }
                else
                {
                    int aBits = ((color.A & mask) >> shift) << 3;
                    int rBits = ((color.R & mask) >> shift) << 2;
                    int gBits = ((color.G & mask) >> shift) << 1;
                    int bBits = (color.B & mask) >> shift;
                    return aBits | rBits | gBits | bBits;
                }
            }

            /// <summary>
            /// Increment the color count and add to the color information
            /// </summary>
            /// <param name="color">The pixel to add.</param>
            /// <param name="octree">The parent octree.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Increment(Rgba32 color, Octree2 octree)
            {
                this.pixelCount++;
                this.red += color.R;
                this.green += color.G;
                this.blue += color.B;

                int sumAlpha = this.alpha + color.A;
                int offset = this.pixelCount >> 1;
                this.alpha = ((sumAlpha + offset) / this.pixelCount < octree.transparencyThreshold) ? 0 : sumAlpha;
            }
        }
    }
}
