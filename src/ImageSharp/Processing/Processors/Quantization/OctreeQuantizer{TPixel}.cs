// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Encapsulates methods to calculate the color palette if an image using an Octree pattern.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public struct OctreeQuantizer<TPixel> : IQuantizer<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly int maxColors;
        private readonly Octree octree;
        private IMemoryOwner<TPixel> paletteOwner;
        private ReadOnlyMemory<TPixel> palette;
        private EuclideanPixelMap<TPixel> pixelMap;
        private readonly bool isDithering;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer{TPixel}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="options">The quantizer options defining quantization rules.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public OctreeQuantizer(Configuration configuration, QuantizerOptions options)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(options, nameof(options));

            this.Configuration = configuration;
            this.Options = options;

            this.maxColors = this.Options.MaxColors;
            this.octree = new Octree(Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(this.maxColors), 1, 8));
            this.paletteOwner = configuration.MemoryAllocator.Allocate<TPixel>(this.maxColors, AllocationOptions.Clean);
            this.palette = default;
            this.pixelMap = default;
            this.isDithering = !(this.Options.Dither is null);
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
                QuantizerUtilities.CheckPaletteState(in this.palette);
                return this.palette;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void AddPaletteColors(Buffer2DRegion<TPixel> pixelRegion)
        {
            Rectangle bounds = pixelRegion.Rectangle;
            Buffer2D<TPixel> source = pixelRegion.Buffer;
            using IMemoryOwner<Rgba32> buffer = this.Configuration.MemoryAllocator.Allocate<Rgba32>(bounds.Width);
            Span<Rgba32> bufferSpan = buffer.GetSpan();

            // Loop through each row
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                Span<TPixel> row = source.GetRowSpan(y).Slice(bounds.Left, bounds.Width);
                PixelOperations<TPixel>.Instance.ToRgba32(this.Configuration, row, bufferSpan);

                for (int x = 0; x < bufferSpan.Length; x++)
                {
                    Rgba32 rgba = bufferSpan[x];

                    // Add the color to the Octree
                    this.octree.AddColor(rgba);
                }
            }

            Span<TPixel> paletteSpan = this.paletteOwner.GetSpan();
            int paletteIndex = 0;
            this.octree.Palletize(paletteSpan, this.maxColors, ref paletteIndex);

            // Length of reduced palette + transparency.
            ReadOnlyMemory<TPixel> result = this.paletteOwner.Memory.Slice(0, Math.Min(paletteIndex + 2, QuantizerConstants.MaxColors));
            this.pixelMap = new EuclideanPixelMap<TPixel>(this.Configuration, result);

            this.palette = result;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
            => QuantizerUtilities.QuantizeFrame(ref Unsafe.AsRef(this), source, bounds);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly byte GetQuantizedColor(TPixel color, out TPixel match)
        {
            // Octree only maps the RGB component of a color
            // so cannot tell the difference between a fully transparent
            // pixel and a black one.
            if (this.isDithering || color.Equals(default))
            {
                return (byte)this.pixelMap.GetClosestColor(color, out match);
            }

            ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.pixelMap.Palette.Span);
            var index = (byte)this.octree.GetPaletteIndex(color);
            match = Unsafe.Add(ref paletteRef, index);
            return index;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.paletteOwner.Dispose();
                this.paletteOwner = null;
            }
        }

        /// <summary>
        /// Class which does the actual quantization.
        /// </summary>
        private sealed class Octree
        {
            /// <summary>
            /// The root of the Octree
            /// </summary>
            private readonly OctreeNode root;

            /// <summary>
            /// Maximum number of significant bits in the image
            /// </summary>
            private readonly int maxColorBits;

            /// <summary>
            /// Store the last node quantized
            /// </summary>
            private OctreeNode previousNode;

            /// <summary>
            /// Cache the previous color quantized
            /// </summary>
            private Rgba32 previousColor;

            /// <summary>
            /// Initializes a new instance of the <see cref="Octree"/> class.
            /// </summary>
            /// <param name="maxColorBits">
            /// The maximum number of significant bits in the image
            /// </param>
            public Octree(int maxColorBits)
            {
                this.maxColorBits = maxColorBits;
                this.Leaves = 0;
                this.ReducibleNodes = new OctreeNode[9];
                this.root = new OctreeNode(0, this.maxColorBits, this);
                this.previousColor = default;
                this.previousNode = null;
            }

            /// <summary>
            /// Gets the mask used when getting the appropriate pixels for a given node.
            /// </summary>
            private static ReadOnlySpan<byte> Mask => new byte[]
            {
                0b10000000,
                0b1000000,
                0b100000,
                0b10000,
                0b1000,
                0b100,
                0b10,
                0b1
            };

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
            private OctreeNode[] ReducibleNodes
            {
                [MethodImpl(InliningOptions.ShortMethod)]
                get;
            }

            /// <summary>
            /// Add a given color value to the Octree
            /// </summary>
            /// <param name="color">The color to add.</param>
            public void AddColor(Rgba32 color)
            {
                // Check if this request is for the same color as the last
                if (this.previousColor.Equals(color))
                {
                    // If so, check if I have a previous node setup.
                    // This will only occur if the first color in the image
                    // happens to be black, with an alpha component of zero.
                    if (this.previousNode is null)
                    {
                        this.previousColor = color;
                        this.root.AddColor(ref color, this.maxColorBits, 0, this);
                    }
                    else
                    {
                        // Just update the previous node
                        this.previousNode.Increment(ref color);
                    }
                }
                else
                {
                    this.previousColor = color;
                    this.root.AddColor(ref color, this.maxColorBits, 0, this);
                }
            }

            /// <summary>
            /// Convert the nodes in the Octree to a palette with a maximum of colorCount colors
            /// </summary>
            /// <param name="palette">The palette to fill.</param>
            /// <param name="colorCount">The maximum number of colors</param>
            /// <param name="paletteIndex">The palette index, used to calculate the final size of the palette.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Palletize(Span<TPixel> palette, int colorCount, ref int paletteIndex)
            {
                while (this.Leaves > colorCount - 1)
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
            {
                Rgba32 rgba = default;
                color.ToRgba32(ref rgba);
                return this.root.GetPaletteIndex(ref rgba, 0);
            }

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
                OctreeNode node = this.ReducibleNodes[index];
                this.ReducibleNodes[index] = node.NextReducible;

                // Decrement the leaf count after reducing the node
                this.Leaves -= node.Reduce();

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
                private readonly OctreeNode[] children;

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
                /// The index of this node in the palette
                /// </summary>
                private int paletteIndex;

                /// <summary>
                /// Initializes a new instance of the <see cref="OctreeNode"/> class.
                /// </summary>
                /// <param name="level">The level in the tree = 0 - 7.</param>
                /// <param name="colorBits">The number of significant color bits in the image.</param>
                /// <param name="octree">The tree to which this node belongs.</param>
                public OctreeNode(int level, int colorBits, Octree octree)
                {
                    // Construct the new node
                    this.leaf = level == colorBits;

                    this.red = this.green = this.blue = 0;
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
                        this.children = new OctreeNode[8];
                    }
                }

                /// <summary>
                /// Gets the next reducible node
                /// </summary>
                public OctreeNode NextReducible
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
                public void AddColor(ref Rgba32 color, int colorBits, int level, Octree octree)
                {
                    // Update the color information if this is a leaf
                    if (this.leaf)
                    {
                        this.Increment(ref color);

                        // Setup the previous node
                        octree.TrackPrevious(this);
                    }
                    else
                    {
                        // Go to the next level down in the tree
                        int index = GetColorIndex(ref color, level);

                        OctreeNode child = this.children[index];
                        if (child is null)
                        {
                            // Create a new child node and store it in the array
                            child = new OctreeNode(level + 1, colorBits, octree);
                            this.children[index] = child;
                        }

                        // Add the color to the child node
                        child.AddColor(ref color, colorBits, level + 1, octree);
                    }
                }

                /// <summary>
                /// Reduce this node by removing all of its children
                /// </summary>
                /// <returns>The number of leaves removed</returns>
                public int Reduce()
                {
                    this.red = this.green = this.blue = 0;
                    int childNodes = 0;

                    // Loop through all children and add their information to this node
                    for (int index = 0; index < 8; index++)
                    {
                        OctreeNode child = this.children[index];
                        if (child != null)
                        {
                            this.red += child.red;
                            this.green += child.green;
                            this.blue += child.blue;
                            this.pixelCount += child.pixelCount;
                            ++childNodes;
                            this.children[index] = null;
                        }
                    }

                    // Now change this to a leaf node
                    this.leaf = true;

                    // Return the number of nodes to decrement the leaf count by
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
                        var vector = Vector3.Clamp(
                            new Vector3(this.red, this.green, this.blue) / this.pixelCount,
                            Vector3.Zero,
                            new Vector3(255));

                        TPixel pixel = default;
                        pixel.FromRgba32(new Rgba32((byte)vector.X, (byte)vector.Y, (byte)vector.Z, byte.MaxValue));
                        palette[index] = pixel;

                        // Consume the next palette index
                        this.paletteIndex = index++;
                    }
                    else
                    {
                        // Loop through children looking for leaves
                        for (int i = 0; i < 8; i++)
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
                public int GetPaletteIndex(ref Rgba32 pixel, int level)
                {
                    if (this.leaf)
                    {
                        return this.paletteIndex;
                    }

                    int colorIndex = GetColorIndex(ref pixel, level);
                    OctreeNode child = this.children[colorIndex];

                    int index = 0;
                    if (child != null)
                    {
                        index = child.GetPaletteIndex(ref pixel, level + 1);
                    }
                    else
                    {
                        // Check other children.
                        for (int i = 0; i < this.children.Length; i++)
                        {
                            child = this.children[i];
                            if (child != null)
                            {
                                var childIndex = child.GetPaletteIndex(ref pixel, level + 1);
                                if (childIndex != 0)
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
                private static int GetColorIndex(ref Rgba32 color, int level)
                {
                    DebugGuard.MustBeLessThan(level, Mask.Length, nameof(level));

                    int shift = 7 - level;
                    ref byte maskRef = ref MemoryMarshal.GetReference(Mask);
                    byte mask = Unsafe.Add(ref maskRef, level);

                    return ((color.R & mask) >> shift)
                           | ((color.G & mask) >> (shift - 1))
                           | ((color.B & mask) >> (shift - 2));
                }

                /// <summary>
                /// Increment the color count and add to the color information
                /// </summary>
                /// <param name="color">The pixel to add.</param>
                [MethodImpl(InliningOptions.ShortMethod)]
                public void Increment(ref Rgba32 color)
                {
                    this.pixelCount++;
                    this.red += color.R;
                    this.green += color.G;
                    this.blue += color.B;
                }
            }
        }
    }
}
