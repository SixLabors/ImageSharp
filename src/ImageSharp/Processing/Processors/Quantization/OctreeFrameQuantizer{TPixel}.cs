// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Encapsulates methods to calculate the color palette if an image using an Octree pattern.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class OctreeFrameQuantizer<TPixel> : FrameQuantizerBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Maximum allowed color depth
        /// </summary>
        private readonly int colors;

        /// <summary>
        /// Stores the tree
        /// </summary>
        private readonly Octree octree;

        /// <summary>
        /// The transparent index
        /// </summary>
        private byte transparentIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeFrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="quantizer">The octree quantizer</param>
        /// <remarks>
        /// The Octree quantizer is a two pass algorithm. The initial pass sets up the Octree,
        /// the second pass quantizes a color based on the nodes in the tree
        /// </remarks>
        public OctreeFrameQuantizer(OctreeQuantizer quantizer)
            : this(quantizer, quantizer.MaxColors)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeFrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="quantizer">The octree quantizer.</param>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette.</param>
        /// <remarks>
        /// The Octree quantizer is a two pass algorithm. The initial pass sets up the Octree,
        /// the second pass quantizes a color based on the nodes in the tree
        /// </remarks>
        public OctreeFrameQuantizer(OctreeQuantizer quantizer, int maxColors)
            : base(quantizer, false)
        {
            this.colors = maxColors;
            this.octree = new Octree(ImageMaths.GetBitsNeededForColorDepth(this.colors).Clamp(1, 8));
        }

        /// <inheritdoc/>
        protected override void FirstPass(ImageFrame<TPixel> source, int width, int height)
        {
            // Loop through each row
            for (int y = 0; y < height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);
                ref TPixel scanBaseRef = ref MemoryMarshal.GetReference(row);

                // And loop through each column
                Rgba32 rgba = default;
                for (int x = 0; x < width; x++)
                {
                    ref TPixel pixel = ref Unsafe.Add(ref scanBaseRef, x);
                    pixel.ToRgba32(ref rgba);

                    // Add the color to the Octree
                    this.octree.AddColor(ref pixel, ref rgba);
                }
            }
        }

        /// <inheritdoc/>
        protected override void SecondPass(
            ImageFrame<TPixel> source,
            Span<byte> output,
            ReadOnlySpan<TPixel> palette,
            int width,
            int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TPixel sourcePixel = source[0, 0];
            TPixel previousPixel = sourcePixel;
            Rgba32 rgba = default;
            this.transparentIndex = this.GetTransparentIndex();
            byte pixelValue = this.QuantizePixel(ref sourcePixel, ref rgba);
            TPixel transformedPixel = palette[pixelValue];

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);

                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Get the pixel.
                    sourcePixel = row[x];

                    // Check if this is the same as the last pixel. If so use that value
                    // rather than calculating it again. This is an inexpensive optimization.
                    if (!previousPixel.Equals(sourcePixel))
                    {
                        // Quantize the pixel
                        pixelValue = this.QuantizePixel(ref sourcePixel, ref rgba);

                        // And setup the previous pointer
                        previousPixel = sourcePixel;

                        if (this.Dither)
                        {
                            transformedPixel = palette[pixelValue];
                        }
                    }

                    if (this.Dither)
                    {
                        // Apply the dithering matrix. We have to reapply the value now as the original has changed.
                        this.Diffuser.Dither(source, sourcePixel, transformedPixel, x, y, 0, 0, width, height);
                    }

                    output[(y * source.Width) + x] = pixelValue;
                }
            }
        }

        /// <inheritdoc/>
        protected override TPixel[] GetPalette() => this.octree.Palletize(this.colors);

        /// <summary>
        /// Process the pixel in the second pass of the algorithm.
        /// </summary>
        /// <param name="pixel">The pixel to quantize.</param>
        /// <param name="rgba">The color to compare against.</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte QuantizePixel(ref TPixel pixel, ref Rgba32 rgba)
        {
            if (this.Dither)
            {
                // The colors have changed so we need to use Euclidean distance calculation to
                // find the closest value.
                return this.GetClosestPixel(ref pixel);
            }

            pixel.ToRgba32(ref rgba);
            if (rgba.Equals(default))
            {
                return this.transparentIndex;
            }

            return (byte)this.octree.GetPaletteIndex(ref pixel, ref rgba);
        }

        /// <summary>
        /// Class which does the actual quantization
        /// </summary>
        private class Octree
        {
            /// <summary>
            /// Mask used when getting the appropriate pixels for a given node
            /// </summary>
            // ReSharper disable once StaticMemberInGenericType
            private static readonly int[] Mask = { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

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
            private TPixel previousColor;

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
            /// Gets or sets the number of leaves in the tree
            /// </summary>
            public int Leaves
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set;
            }

            /// <summary>
            /// Gets the array of reducible nodes
            /// </summary>
            private OctreeNode[] ReducibleNodes
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get;
            }

            /// <summary>
            /// Add a given color value to the Octree
            /// </summary>
            /// <param name="pixel">The pixel data.</param>
            /// <param name="rgba">The color.</param>
            public void AddColor(ref TPixel pixel, ref Rgba32 rgba)
            {
                // Check if this request is for the same color as the last
                if (this.previousColor.Equals(pixel))
                {
                    // If so, check if I have a previous node setup. This will only occur if the first color in the image
                    // happens to be black, with an alpha component of zero.
                    if (this.previousNode is null)
                    {
                        this.previousColor = pixel;
                        this.root.AddColor(ref pixel, this.maxColorBits, 0, this, ref rgba);
                    }
                    else
                    {
                        // Just update the previous node
                        this.previousNode.Increment(ref pixel, ref rgba);
                    }
                }
                else
                {
                    this.previousColor = pixel;
                    this.root.AddColor(ref pixel, this.maxColorBits, 0, this, ref rgba);
                }
            }

            /// <summary>
            /// Convert the nodes in the Octree to a palette with a maximum of colorCount colors
            /// </summary>
            /// <param name="colorCount">The maximum number of colors</param>
            /// <returns>
            /// An <see cref="List{TPixel}"/> with the palletized colors
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TPixel[] Palletize(int colorCount)
            {
                while (this.Leaves > colorCount - 1)
                {
                    this.Reduce();
                }

                // Now palletize the nodes
                var palette = new TPixel[colorCount];

                int paletteIndex = 0;
                this.root.ConstructPalette(palette, ref paletteIndex);

                // And return the palette
                return palette;
            }

            /// <summary>
            /// Get the palette index for the passed color
            /// </summary>
            /// <param name="pixel">The pixel data.</param>
            /// <param name="rgba">The color to map to.</param>
            /// <returns>
            /// The <see cref="int"/>.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetPaletteIndex(ref TPixel pixel, ref Rgba32 rgba) => this.root.GetPaletteIndex(ref pixel, 0, ref rgba);

            /// <summary>
            /// Keep track of the previous node that was quantized
            /// </summary>
            /// <param name="node">
            /// The node last quantized
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected void TrackPrevious(OctreeNode node) => this.previousNode = node;

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
            protected class OctreeNode
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
                /// <param name="level">
                /// The level in the tree = 0 - 7
                /// </param>
                /// <param name="colorBits">
                /// The number of significant color bits in the image
                /// </param>
                /// <param name="octree">
                /// The tree to which this node belongs
                /// </param>
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
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get;
                }

                /// <summary>
                /// Add a color into the tree
                /// </summary>
                /// <param name="pixel">The pixel color</param>
                /// <param name="colorBits">The number of significant color bits</param>
                /// <param name="level">The level in the tree</param>
                /// <param name="octree">The tree to which this node belongs</param>
                /// <param name="rgba">The color to map to.</param>
                public void AddColor(ref TPixel pixel, int colorBits, int level, Octree octree, ref Rgba32 rgba)
                {
                    // Update the color information if this is a leaf
                    if (this.leaf)
                    {
                        this.Increment(ref pixel, ref rgba);

                        // Setup the previous node
                        octree.TrackPrevious(this);
                    }
                    else
                    {
                        // Go to the next level down in the tree
                        int shift = 7 - level;
                        pixel.ToRgba32(ref rgba);

                        int index = ((rgba.B & Mask[level]) >> (shift - 2))
                                    | ((rgba.G & Mask[level]) >> (shift - 1))
                                    | ((rgba.R & Mask[level]) >> shift);

                        OctreeNode child = this.children[index];
                        if (child is null)
                        {
                            // Create a new child node and store it in the array
                            child = new OctreeNode(level + 1, colorBits, octree);
                            this.children[index] = child;
                        }

                        // Add the color to the child node
                        child.AddColor(ref pixel, colorBits, level + 1, octree, ref rgba);
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
                [MethodImpl(MethodImplOptions.NoInlining)]
                public void ConstructPalette(TPixel[] palette, ref int index)
                {
                    if (this.leaf)
                    {
                        // Set the color of the palette entry
                        var vector = Vector3.Clamp(new Vector3(this.red, this.green, this.blue) / this.pixelCount, Vector3.Zero, new Vector3(255));
                        TPixel pixel = default;
                        pixel.PackFromRgba32(new Rgba32((byte)vector.X, (byte)vector.Y, (byte)vector.Z, byte.MaxValue));
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
                /// <param name="rgba">The color to map to.</param>
                /// <returns>
                /// The <see cref="int"/> representing the index of the pixel in the palette.
                /// </returns>
                [MethodImpl(MethodImplOptions.NoInlining)]
                public int GetPaletteIndex(ref TPixel pixel, int level, ref Rgba32 rgba)
                {
                    int index = this.paletteIndex;

                    if (!this.leaf)
                    {
                        int shift = 7 - level;
                        pixel.ToRgba32(ref rgba);

                        int pixelIndex = ((rgba.B & Mask[level]) >> (shift - 2))
                                         | ((rgba.G & Mask[level]) >> (shift - 1))
                                         | ((rgba.R & Mask[level]) >> shift);

                        OctreeNode child = this.children[pixelIndex];
                        if (child != null)
                        {
                            index = child.GetPaletteIndex(ref pixel, level + 1, ref rgba);
                        }
                        else
                        {
                            throw new Exception($"Cannot retrieve a pixel at the given index {pixelIndex}.");
                        }
                    }

                    return index;
                }

                /// <summary>
                /// Increment the pixel count and add to the color information
                /// </summary>
                /// <param name="pixel">The pixel to add.</param>
                /// <param name="rgba">The color to map to.</param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Increment(ref TPixel pixel, ref Rgba32 rgba)
                {
                    pixel.ToRgba32(ref rgba);
                    this.pixelCount++;
                    this.red += rgba.R;
                    this.green += rgba.G;
                    this.blue += rgba.B;
                }
            }
        }
    }
}