// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Quantizers.Base;

namespace SixLabors.ImageSharp.Quantizers
{
    /// <summary>
    /// Encapsulates methods to calculate the color palette if an image using an Octree pattern.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class OctreeQuantizer<TPixel> : QuantizerBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<TPixel, byte> colorMap = new Dictionary<TPixel, byte>();

        /// <summary>
        /// The pixel buffer, used to reduce allocations.
        /// </summary>
        private readonly byte[] pixelBuffer = new byte[4];

        /// <summary>
        /// Stores the tree
        /// </summary>
        private Octree octree;

        /// <summary>
        /// Maximum allowed color depth
        /// </summary>
        private byte colors;

        /// <summary>
        /// The reduced image palette
        /// </summary>
        private TPixel[] palette;

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer{TPixel}"/> class.
        /// </summary>
        /// <remarks>
        /// The Octree quantizer is a two pass algorithm. The initial pass sets up the Octree,
        /// the second pass quantizes a color based on the nodes in the tree
        /// </remarks>
        public OctreeQuantizer()
            : base(false)
        {
        }

        /// <inheritdoc/>
        public override QuantizedImage<TPixel> Quantize(ImageFrame<TPixel> image, int maxColors)
        {
            this.colors = (byte)maxColors.Clamp(1, 255);
            this.octree = new Octree(this.GetBitsNeededForColorDepth(this.colors));
            this.palette = null;
            this.colorMap.Clear();

            return base.Quantize(image, this.colors);
        }

        /// <inheritdoc/>
        protected override void SecondPass(ImageFrame<TPixel> source, byte[] output, int width, int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TPixel sourcePixel = source[0, 0];
            TPixel previousPixel = sourcePixel;
            byte pixelValue = this.QuantizePixel(sourcePixel);
            TPixel[] colorPalette = this.GetPalette();
            TPixel transformedPixel = colorPalette[pixelValue];

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
                        pixelValue = this.QuantizePixel(sourcePixel);

                        // And setup the previous pointer
                        previousPixel = sourcePixel;

                        if (this.Dither)
                        {
                            transformedPixel = colorPalette[pixelValue];
                        }
                    }

                    if (this.Dither)
                    {
                        // Apply the dithering matrix. We have to reapply the value now as the original has changed.
                        this.DitherType.Dither(source, sourcePixel, transformedPixel, x, y, 0, 0, width, height, false);
                    }

                    output[(y * source.Width) + x] = pixelValue;
                }
            }
        }

        /// <inheritdoc/>
        protected override void InitialQuantizePixel(TPixel pixel)
        {
            // Add the color to the Octree
            this.octree.AddColor(pixel, this.pixelBuffer);
        }

        /// <inheritdoc/>
        protected override TPixel[] GetPalette()
        {
            return this.palette ?? (this.palette = this.octree.Palletize(Math.Max(this.colors, (byte)1)));
        }

        /// <summary>
        /// Process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>
        /// The quantized value
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte QuantizePixel(TPixel pixel)
        {
            if (this.Dither)
            {
                // The colors have changed so we need to use Euclidean distance caclulation to find the closest value.
                // This palette can never be null here.
                return this.GetClosestPixel(pixel, this.palette, this.colorMap);
            }

            pixel.ToXyzwBytes(this.pixelBuffer, 0);
            if (this.pixelBuffer[3] == 0)
            {
                return this.colors;
            }

            return (byte)this.octree.GetPaletteIndex(pixel, this.pixelBuffer);
        }

        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colorCount">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBitsNeededForColorDepth(int colorCount)
        {
            return (int)Math.Ceiling(Math.Log(colorCount, 2));
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
            /// Array of reducible nodes
            /// </summary>
            private readonly OctreeNode[] reducibleNodes;

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
                this.reducibleNodes = new OctreeNode[9];
                this.root = new OctreeNode(0, this.maxColorBits, this);
                this.previousColor = default(TPixel);
                this.previousNode = null;
            }

            /// <summary>
            /// Gets or sets the number of leaves in the tree
            /// </summary>
            private int Leaves { get; set; }

            /// <summary>
            /// Gets the array of reducible nodes
            /// </summary>
            private OctreeNode[] ReducibleNodes => this.reducibleNodes;

            /// <summary>
            /// Add a given color value to the Octree
            /// </summary>
            /// <param name="pixel">The pixel data.</param>
            /// <param name="buffer">The buffer array.</param>
            public void AddColor(TPixel pixel, byte[] buffer)
            {
                // Check if this request is for the same color as the last
                if (this.previousColor.Equals(pixel))
                {
                    // If so, check if I have a previous node setup. This will only occur if the first color in the image
                    // happens to be black, with an alpha component of zero.
                    if (this.previousNode == null)
                    {
                        this.previousColor = pixel;
                        this.root.AddColor(pixel, this.maxColorBits, 0, this, buffer);
                    }
                    else
                    {
                        // Just update the previous node
                        this.previousNode.Increment(pixel, buffer);
                    }
                }
                else
                {
                    this.previousColor = pixel;
                    this.root.AddColor(pixel, this.maxColorBits, 0, this, buffer);
                }
            }

            /// <summary>
            /// Convert the nodes in the Octree to a palette with a maximum of colorCount colors
            /// </summary>
            /// <param name="colorCount">The maximum number of colors</param>
            /// <returns>
            /// An <see cref="List{TPixel}"/> with the palletized colors
            /// </returns>
            public TPixel[] Palletize(int colorCount)
            {
                while (this.Leaves > colorCount)
                {
                    this.Reduce();
                }

                // Now palletize the nodes
                TPixel[] palette = new TPixel[colorCount + 1];

                int paletteIndex = 0;
                this.root.ConstructPalette(palette, ref paletteIndex);

                // And return the palette
                return palette;
            }

            /// <summary>
            /// Get the palette index for the passed color
            /// </summary>
            /// <param name="pixel">The pixel data.</param>
            /// <param name="buffer">The buffer array.</param>
            /// <returns>
            /// The <see cref="int"/>.
            /// </returns>
            public int GetPaletteIndex(TPixel pixel, byte[] buffer)
            {
                return this.root.GetPaletteIndex(pixel, 0, buffer);
            }

            /// <summary>
            /// Keep track of the previous node that was quantized
            /// </summary>
            /// <param name="node">
            /// The node last quantized
            /// </param>
            protected void TrackPrevious(OctreeNode node)
            {
                this.previousNode = node;
            }

            /// <summary>
            /// Reduce the depth of the tree
            /// </summary>
            private void Reduce()
            {
                // Find the deepest level containing at least one reducible node
                int index = this.maxColorBits - 1;
                while ((index > 0) && (this.reducibleNodes[index] == null))
                {
                    index--;
                }

                // Reduce the node most recently added to the list at level 'index'
                OctreeNode node = this.reducibleNodes[index];
                this.reducibleNodes[index] = node.NextReducible;

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
                public OctreeNode NextReducible { get; }

                /// <summary>
                /// Add a color into the tree
                /// </summary>
                /// <param name="pixel">The color</param>
                /// <param name="colorBits">The number of significant color bits</param>
                /// <param name="level">The level in the tree</param>
                /// <param name="octree">The tree to which this node belongs</param>
                /// <param name="buffer">The buffer array.</param>
                public void AddColor(TPixel pixel, int colorBits, int level, Octree octree, byte[] buffer)
                {
                    // Update the color information if this is a leaf
                    if (this.leaf)
                    {
                        this.Increment(pixel, buffer);

                        // Setup the previous node
                        octree.TrackPrevious(this);
                    }
                    else
                    {
                        // Go to the next level down in the tree
                        int shift = 7 - level;
                        pixel.ToXyzwBytes(buffer, 0);

                        int index = ((buffer[2] & Mask[level]) >> (shift - 2)) |
                                    ((buffer[1] & Mask[level]) >> (shift - 1)) |
                                    ((buffer[0] & Mask[level]) >> shift);

                        OctreeNode child = this.children[index];

                        if (child == null)
                        {
                            // Create a new child node and store it in the array
                            child = new OctreeNode(level + 1, colorBits, octree);
                            this.children[index] = child;
                        }

                        // Add the color to the child node
                        child.AddColor(pixel, colorBits, level + 1, octree, buffer);
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
                        if (this.children[index] != null)
                        {
                            this.red += this.children[index].red;
                            this.green += this.children[index].green;
                            this.blue += this.children[index].blue;
                            this.pixelCount += this.children[index].pixelCount;
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
                public void ConstructPalette(TPixel[] palette, ref int index)
                {
                    if (this.leaf)
                    {
                        // This seems faster than using Vector4
                        byte r = (this.red / this.pixelCount).ToByte();
                        byte g = (this.green / this.pixelCount).ToByte();
                        byte b = (this.blue / this.pixelCount).ToByte();

                        // And set the color of the palette entry
                        var pixel = default(TPixel);
                        pixel.PackFromRgba32(new Rgba32(r, g, b, 255));
                        palette[index] = pixel;

                        // Consume the next palette index
                        this.paletteIndex = index++;
                    }
                    else
                    {
                        // Loop through children looking for leaves
                        for (int i = 0; i < 8; i++)
                        {
                            if (this.children[i] != null)
                            {
                                this.children[i].ConstructPalette(palette, ref index);
                            }
                        }
                    }
                }

                /// <summary>
                /// Return the palette index for the passed color
                /// </summary>
                /// <param name="pixel">The pixel data.</param>
                /// <param name="level">The level.</param>
                /// <param name="buffer">The buffer array.</param>
                /// <returns>
                /// The <see cref="int"/> representing the index of the pixel in the palette.
                /// </returns>
                public int GetPaletteIndex(TPixel pixel, int level, byte[] buffer)
                {
                    int index = this.paletteIndex;

                    if (!this.leaf)
                    {
                        int shift = 7 - level;
                        pixel.ToXyzwBytes(buffer, 0);

                        int pixelIndex = ((buffer[2] & Mask[level]) >> (shift - 2)) |
                                         ((buffer[1] & Mask[level]) >> (shift - 1)) |
                                         ((buffer[0] & Mask[level]) >> shift);

                        if (this.children[pixelIndex] != null)
                        {
                            index = this.children[pixelIndex].GetPaletteIndex(pixel, level + 1, buffer);
                        }
                        else
                        {
                            throw new Exception($"Cannot retrive a pixel at the given index {pixelIndex}.");
                        }
                    }

                    return index;
                }

                /// <summary>
                /// Increment the pixel count and add to the color information
                /// </summary>
                /// <param name="pixel">The pixel to add.</param>
                /// <param name="buffer">The buffer array.</param>
                public void Increment(TPixel pixel, byte[] buffer)
                {
                    pixel.ToXyzwBytes(buffer, 0);
                    this.pixelCount++;
                    this.red += buffer[0];
                    this.green += buffer[1];
                    this.blue += buffer[2];
                }
            }
        }
    }
}