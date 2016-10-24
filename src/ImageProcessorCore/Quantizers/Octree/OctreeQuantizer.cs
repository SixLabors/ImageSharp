// <copyright file="OctreeQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates methods to calculate the colour palette if an image using an Octree pattern.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public sealed class OctreeQuantizer<TColor, TPacked> : Quantizer<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Stores the tree
        /// </summary>
        private Octree octree;

        /// <summary>
        /// Maximum allowed color depth
        /// </summary>
        private int colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer{TColor, TPacked}"/> class.
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
        public override QuantizedImage<TColor, TPacked> Quantize(ImageBase<TColor, TPacked> image, int maxColors)
        {
            this.colors = maxColors.Clamp(1, 256);

            if (this.octree == null)
            {
                // Construct the Octree
                this.octree = new Octree(this.GetBitsNeededForColorDepth(maxColors));
            }

            return base.Quantize(image, maxColors);
        }

        /// <summary>
        /// Process the pixel in the first pass of the algorithm
        /// </summary>
        /// <param name="pixel">
        /// The pixel to quantize
        /// </param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected override void InitialQuantizePixel(TColor pixel)
        {
            // Add the color to the Octree
            this.octree.AddColor(pixel);
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>
        /// The quantized value
        /// </returns>
        protected override byte QuantizePixel(TColor pixel)
        {
            return (byte)this.octree.GetPaletteIndex(pixel);
        }

        /// <summary>
        /// Retrieve the palette for the quantized image.
        /// </summary>
        /// <returns>
        /// The new color palette
        /// </returns>
        protected override List<TColor> GetPalette()
        {
            return this.octree.Palletize(Math.Max(this.colors, 1));
        }

        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colorCount">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
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
            private static readonly int[] Mask = { 0x100, 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

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
            private TPacked previousColor;

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
                this.previousColor = default(TPacked);
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
            /// <param name="pixel">
            /// The <see cref="TColor"/>containing color information to add.
            /// </param>
            public void AddColor(TColor pixel)
            {
                TPacked packed = pixel.PackedValue;

                // Check if this request is for the same color as the last
                if (this.previousColor.Equals(packed))
                {
                    // If so, check if I have a previous node setup. This will only occur if the first color in the image
                    // happens to be black, with an alpha component of zero.
                    if (this.previousNode == null)
                    {
                        this.previousColor = packed;
                        this.root.AddColor(pixel, this.maxColorBits, 0, this);
                    }
                    else
                    {
                        // Just update the previous node
                        this.previousNode.Increment(pixel);
                    }
                }
                else
                {
                    this.previousColor = packed;
                    this.root.AddColor(pixel, this.maxColorBits, 0, this);
                }
            }

            /// <summary>
            /// Convert the nodes in the Octree to a palette with a maximum of colorCount colors
            /// </summary>
            /// <param name="colorCount">The maximum number of colors</param>
            /// <returns>
            /// An <see cref="List{TColor}"/> with the palletized colors
            /// </returns>
            public List<TColor> Palletize(int colorCount)
            {
                while (this.Leaves > colorCount)
                {
                    this.Reduce();
                }

                // Now palletize the nodes
                List<TColor> palette = new List<TColor>(this.Leaves);
                int paletteIndex = 0;
                this.root.ConstructPalette(palette, ref paletteIndex);

                // And return the palette
                return palette;
            }

            /// <summary>
            /// Get the palette index for the passed color
            /// </summary>
            /// <param name="pixel">The <see cref="TColor"/> containing the pixel data.</param>
            /// <returns>
            /// The index of the given structure.
            /// </returns>
            public int GetPaletteIndex(TColor pixel)
            {
                return this.root.GetPaletteIndex(pixel, 0);
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
                public void AddColor(TColor pixel, int colorBits, int level, Octree octree)
                {
                    // Update the color information if this is a leaf
                    if (this.leaf)
                    {
                        this.Increment(pixel);

                        // Setup the previous node
                        octree.TrackPrevious(this);
                    }
                    else
                    {
                        // Go to the next level down in the tree
                        int shift = 7 - level;
                        Color color = new Color(pixel.ToVector4());
                        int index = ((color.A & Mask[0]) >> (shift - 3)) |
                                    ((color.B & Mask[level + 1]) >> (shift - 2)) |
                                    ((color.G & Mask[level + 1]) >> (shift - 1)) |
                                    ((color.R & Mask[level + 1]) >> shift);

                        OctreeNode child = this.children[index];

                        if (child == null)
                        {
                            // Create a new child node and store it in the array
                            child = new OctreeNode(level + 1, colorBits, octree);
                            this.children[index] = child;
                        }

                        // Add the color to the child node
                        child.AddColor(pixel, colorBits, level + 1, octree);
                    }
                }

                /// <summary>
                /// Reduce this node by removing all of its children
                /// </summary>
                /// <returns>The number of leaves removed</returns>
                public int Reduce()
                {
                    this.red = this.green = this.blue = this.alpha = 0;
                    int childNodes = 0;

                    // Loop through all children and add their information to this node
                    for (int index = 0; index < 8; index++)
                    {
                        if (this.children[index] != null)
                        {
                            this.red += this.children[index].red;
                            this.green += this.children[index].green;
                            this.blue += this.children[index].blue;
                            this.alpha += this.children[index].alpha;
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
                public void ConstructPalette(List<TColor> palette, ref int index)
                {
                    if (this.leaf)
                    {
                        // Consume the next palette index
                        this.paletteIndex = index++;

                        byte r = (this.red / this.pixelCount).ToByte();
                        byte g = (this.green / this.pixelCount).ToByte();
                        byte b = (this.blue / this.pixelCount).ToByte();
                        byte a = (this.alpha / this.pixelCount).ToByte();

                        // And set the color of the palette entry
                        TColor pixel = default(TColor);
                        pixel.PackFromVector4(new Color(r, g, b, a).ToVector4());
                        palette.Add(pixel);
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
                /// <param name="pixel">The <see cref="TColor"/> representing the pixel.</param>
                /// <param name="level">The level.</param>
                /// <returns>
                /// The <see cref="int"/> representing the index of the pixel in the palette.
                /// </returns>
                public int GetPaletteIndex(TColor pixel, int level)
                {
                    int index = this.paletteIndex;

                    if (!this.leaf)
                    {
                        int shift = 7 - level;
                        Color color = new Color(pixel.ToVector4());
                        int pixelIndex = ((color.A & Mask[0]) >> (shift - 3)) |
                                         ((color.B & Mask[level + 1]) >> (shift - 2)) |
                                         ((color.G & Mask[level + 1]) >> (shift - 1)) |
                                         ((color.R & Mask[level + 1]) >> shift);

                        if (this.children[pixelIndex] != null)
                        {
                            index = this.children[pixelIndex].GetPaletteIndex(pixel, level + 1);
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
                /// <param name="pixel">
                /// The pixel to add.
                /// </param>
                public void Increment(TColor pixel)
                {
                    this.pixelCount++;
                    Color color = new Color(pixel.ToVector4());
                    this.red += color.R;
                    this.green += color.G;
                    this.blue += color.B;
                    this.alpha += color.A;
                }
            }
        }
    }
}
