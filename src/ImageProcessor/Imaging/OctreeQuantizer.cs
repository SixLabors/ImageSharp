// -----------------------------------------------------------------------
// <copyright file="OctreeQuantizer.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System;
    using System.Collections;
    using System.Drawing;
    using System.Drawing.Imaging;
    #endregion

    /// <summary>
    /// Encapsulates methods to calculate the colour palette if an image using an octree pattern.
    /// </summary>
    internal class OctreeQuantizer : Quantizer
    {
        #region Fields
        /// <summary>
        /// Stores the tree.
        /// </summary>
        private readonly Octree octree;

        /// <summary>
        /// The maximum allowed color depth.
        /// </summary>
        private readonly int maxColors;
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ImageProcessor.Imaging.OctreeQuantizer">OctreeQuantizer</see> class. 
        /// </summary>
        /// <remarks>
        /// The Octree quantizer is a two pass algorithm. The initial pass sets up the octree,
        /// the second pass quantizes a colour based on the nodes in the tree
        /// </remarks>
        /// <param name="maxColors">The maximum number of colours to return, maximum 255.</param>
        /// <param name="maxColorBits">The number of significant bits minimum 1, maximum 8.</param>
        public OctreeQuantizer(int maxColors, int maxColorBits)
            : base(false)
        {
            if (maxColors > 255)
            {
                throw new ArgumentOutOfRangeException("maxColors", maxColors, "The number of colours should be less than 256");
            }

            if ((maxColorBits < 1) | (maxColorBits > 8))
            {
                throw new ArgumentOutOfRangeException("maxColorBits", maxColorBits, "This should be between 1 and 8");
            }

            // Construct the octree
            this.octree = new Octree(maxColorBits);
            this.maxColors = maxColors;
        }

        /// <summary>
        /// Process the pixel in the first pass of the algorithm.
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected override void InitialQuantizePixel(Color32 pixel)
        {
            // Add the colour to the octree
            this.octree.AddColor(pixel);
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm.
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>The quantized value.</returns>
        protected override byte QuantizePixel(Color32 pixel)
        {
            // The colour at [this.maxColors] is set to transparent
            byte paletteIndex;

            // Get the palette index if this non-transparent
            if (pixel.Alpha > 0)
            {
                paletteIndex = (byte)this.octree.GetPaletteIndex(pixel);
            }
            else
            {
                paletteIndex = (byte)this.maxColors;
            }

            return paletteIndex;
        }

        /// <summary>
        /// Retrieve the palette for the quantized image
        /// </summary>
        /// <param name="original">Any old palette, this is overwritten</param>
        /// <returns>The new colour palette</returns>
        protected override ColorPalette GetPalette(ColorPalette original)
        {
            // First off convert the octree to this.maxColors colours
            ArrayList palette = this.octree.Palletize(this.maxColors - 1);

            // Then convert the palette based on those colours
            for (int index = 0; index < palette.Count; index++)
            {
                original.Entries[index] = (Color)palette[index];
            }

            // Add the transparent colour
            original.Entries[this.maxColors] = Color.FromArgb(0, 0, 0, 0);

            return original;
        }

        /// <summary>
        /// Describes a tree data structure in which each internal node has exactly eight children.
        /// </summary>
        private class Octree
        {
            #region Fields
            /// <summary>
            /// Mask used when getting the appropriate pixels for a given node
            /// </summary>
            private static int[] mask = new int[8] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

            /// <summary>
            /// The root of the octree
            /// </summary>
            private OctreeNode root;

            /// <summary>
            /// Number of leaves in the tree
            /// </summary>
            private int leafCount;

            /// <summary>
            /// Array of reducible nodes
            /// </summary>
            private OctreeNode[] reducibleNodes;

            /// <summary>
            /// Maximum number of significant bits in the image
            /// </summary>
            private int maxColorBits;

            /// <summary>
            /// Store the last node quantized
            /// </summary>
            private OctreeNode previousNode;

            /// <summary>
            /// Cache the previous color quantized
            /// </summary>
            private int previousColor; 
            #endregion

            #region Constructors
            /// <summary>
            /// Initializes a new instance of the <see cref="T:ImageProcessor.Imaging.OctreeQuantizer.Octree">Octree</see> class. 
            /// </summary>
            /// <param name="maxBits">The maximum number of significant bits in the image</param>
            public Octree(int maxBits)
            {
                this.maxColorBits = maxBits;
                this.leafCount = 0;
                this.reducibleNodes = new OctreeNode[9];
                this.root = new OctreeNode(0, this.maxColorBits, this);
                this.previousColor = 0;
                this.previousNode = null;
            } 
            #endregion

            #region Properties
            /// <summary>
            /// Gets or sets the number of leaves in the tree
            /// </summary>
            public int Leaves
            {
                get { return this.leafCount; }
                set { this.leafCount = value; }
            }

            /// <summary>
            /// Gets the array of reducible nodes
            /// </summary>
            protected OctreeNode[] ReducibleNodes
            {
                get { return this.reducibleNodes; }
            } 
            #endregion

            /// <summary>
            /// Add a given colour value to the octree
            /// </summary>
            /// <param name="pixel">
            /// The color value to add.
            /// </param>
            public void AddColor(Color32 pixel)
            {
                // Check if this request is for the same colour as the last
                if (this.previousColor == pixel.ARGB)
                {
                    // If so, check if I have a previous node setup. This will only occur if the first colour in the image
                    // happens to be black, with an alpha component of zero.
                    if (null == this.previousNode)
                    {
                        this.previousColor = pixel.ARGB;
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
                    this.previousColor = pixel.ARGB;
                    this.root.AddColor(pixel, this.maxColorBits, 0, this);
                }
            }

            /// <summary>
            /// Reduce the depth of the tree
            /// </summary>
            public void Reduce()
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
                this.leafCount -= node.Reduce();

                // And just in case I've reduced the last color to be added, and the next color to
                // be added is the same, invalidate the previousNode...
                this.previousNode = null;
            }

            /// <summary>
            /// Convert the nodes in the octree to a palette with a maximum of colorCount colours
            /// </summary>
            /// <param name="colorCount">The maximum number of colours</param>
            /// <returns>An array list with the palletized colours</returns>
            public ArrayList Palletize(int colorCount)
            {
                while (this.Leaves > colorCount)
                {
                    this.Reduce();
                }

                // Now palletize the nodes
                ArrayList palette = new ArrayList(this.Leaves);
                int paletteIndex = 0;
                this.root.ConstructPalette(palette, ref paletteIndex);

                // And return the palette
                return palette;
            }

            /// <summary>
            /// Get the palette index for the passed colour.
            /// </summary>
            /// <param name="pixel">
            /// The color to return the palette index for.
            /// </param>
            /// <returns>
            /// The palette index for the passed colour.
            /// </returns>
            public int GetPaletteIndex(Color32 pixel)
            {
                return this.root.GetPaletteIndex(pixel, 0);
            }

            /// <summary>
            /// Keep track of the previous node that was quantized
            /// </summary>
            /// <param name="node">The node last quantized</param>
            protected void TrackPrevious(OctreeNode node)
            {
                this.previousNode = node;
            }

            /// <summary>
            /// Class which encapsulates each node in the tree
            /// </summary>
            protected class OctreeNode
            {
                #region Fields
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
                /// Pointers to any child nodes
                /// </summary>
                private OctreeNode[] children;

                /// <summary>
                /// The index of this node in the palette
                /// </summary>
                private int paletteIndex;
                #endregion

                #region Constructors
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
                #endregion

                #region Properties

                /// <summary>
                /// Gets or the next reducible node
                /// </summary>
                public OctreeNode NextReducible { get; private set; }

                /// <summary>
                /// Gets the child nodes
                /// </summary>
                private OctreeNode[] Children
                {
                    get { return this.children; }
                } 
                #endregion

                #region Methods
                /// <summary>
                /// Add a color into the tree
                /// </summary>
                /// <param name="pixel">The color</param>
                /// <param name="colorBits">The number of significant color bits</param>
                /// <param name="level">The level in the tree</param>
                /// <param name="octree">The tree to which this node belongs</param>
                public void AddColor(Color32 pixel, int colorBits, int level, Octree octree)
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
                        int index = ((pixel.Red & mask[level]) >> (shift - 2)) |
                            ((pixel.Green & mask[level]) >> (shift - 1)) |
                            ((pixel.Blue & mask[level]) >> shift);

                        OctreeNode child = this.Children[index];

                        if (null == child)
                        {
                            // Create a new child node & store in the array
                            child = new OctreeNode(level + 1, colorBits, octree);
                            this.Children[index] = child;
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
                    this.red = this.green = this.blue = 0;
                    int childPosition = 0;

                    // Loop through all children and add their information to this node
                    for (int index = 0; index < 8; index++)
                    {
                        if (null != this.Children[index])
                        {
                            this.red += this.Children[index].red;
                            this.green += this.Children[index].green;
                            this.blue += this.Children[index].blue;
                            this.pixelCount += this.Children[index].pixelCount;
                            ++childPosition;
                            this.Children[index] = null;
                        }
                    }

                    // Now change this to a leaf node
                    this.leaf = true;

                    // Return the number of nodes to decrement the leaf count by
                    return childPosition - 1;
                }

                /// <summary>
                /// Traverse the tree, building up the color palette
                /// </summary>
                /// <param name="palette">The palette</param>
                /// <param name="currentPaletteIndex">The current palette index</param>
                public void ConstructPalette(ArrayList palette, ref int currentPaletteIndex)
                {
                    if (this.leaf)
                    {
                        // Consume the next palette index
                        this.paletteIndex = currentPaletteIndex++;

                        // And set the color of the palette entry
                        palette.Add(Color.FromArgb(this.red / this.pixelCount, this.green / this.pixelCount, this.blue / this.pixelCount));
                    }
                    else
                    {
                        // Loop through children looking for leaves
                        for (int index = 0; index < 8; index++)
                        {
                            if (null != this.children[index])
                            {
                                this.children[index].ConstructPalette(palette, ref currentPaletteIndex);
                            }
                        }
                    }
                }

                /// <summary>
                /// Return the palette index for the passed color.
                /// </summary>
                /// <param name="pixel">
                /// The pixel.
                /// </param>
                /// <param name="level">
                /// The level.
                /// </param>
                /// <returns>
                /// The palette index for the passed color.
                /// </returns>
                public int GetPaletteIndex(Color32 pixel, int level)
                {
                    int currentPaletteIndex = this.paletteIndex;

                    if (!this.leaf)
                    {
                        int shift = 7 - level;
                        int index = ((pixel.Red & mask[level]) >> (shift - 2)) |
                            ((pixel.Green & mask[level]) >> (shift - 1)) |
                            ((pixel.Blue & mask[level]) >> shift);

                        if (null != this.children[index])
                        {
                            currentPaletteIndex = this.children[index].GetPaletteIndex(pixel, level + 1);
                        }
                        else
                        {
                            throw new Exception("Didn't expect this!");
                        }
                    }

                    return currentPaletteIndex;
                }

                /// <summary>
                /// Increment the pixel count and add to the color information
                /// </summary>
                /// <param name="pixel">
                /// The pixel.
                /// </param>
                public void Increment(Color32 pixel)
                {
                    this.pixelCount++;
                    this.red += pixel.Red;
                    this.green += pixel.Green;
                    this.blue += pixel.Blue;
                } 
                #endregion
            }
        }
    }
}
