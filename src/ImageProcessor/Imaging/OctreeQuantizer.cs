// -----------------------------------------------------------------------
// <copyright file="OctreeQuantizer.cs" company="James South">
// TODO: Update copyright text.
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
            /// <summary>
            /// Initializes a new instance of the <see cref="T:ImageProcessor.Imaging.OctreeQuantizer.Octree">Octree</see> class. 
            /// </summary>
            /// <param name="maxColorBits">The maximum number of significant bits in the image</param>
            public Octree(int maxColorBits)
            {
                this._maxColorBits = maxColorBits;
                this._leafCount = 0;
                this._reducibleNodes = new OctreeNode[9];
                this._root = new OctreeNode(0, this._maxColorBits, this);
                this._previousColor = 0;
                this._previousNode = null;
            }

            /// <summary>
            /// Add a given colour value to the octree
            /// </summary>
            /// <param name="pixel">
            /// The color value to add.
            /// </param>
            public void AddColor(Color32 pixel)
            {
                // Check if this request is for the same colour as the last
                if (this._previousColor == pixel.ARGB)
                {
                    // If so, check if I have a previous node setup. This will only occur if the first colour in the image
                    // happens to be black, with an alpha component of zero.
                    if (null == this._previousNode)
                    {
                        this._previousColor = pixel.ARGB;
                        this._root.AddColor(pixel, this._maxColorBits, 0, this);
                    }
                    else
                    {
                        // Just update the previous node
                        this._previousNode.Increment(pixel);
                    }
                }
                else
                {
                    this._previousColor = pixel.ARGB;
                    this._root.AddColor(pixel, this._maxColorBits, 0, this);
                }
            }

            /// <summary>
            /// Reduce the depth of the tree
            /// </summary>
            public void Reduce()
            {
                // Find the deepest level containing at least one reducible node
                int index = this._maxColorBits - 1;
                while ((index > 0) && (this._reducibleNodes[index] == null))
                {
                    index--;
                }
                
                // Reduce the node most recently added to the list at level 'index'
                OctreeNode node = this._reducibleNodes[index];
                this._reducibleNodes[index] = node.NextReducible;

                // Decrement the leaf count after reducing the node
                this._leafCount -= node.Reduce();

                // And just in case I've reduced the last color to be added, and the next color to
                // be added is the same, invalidate the previousNode...
                this._previousNode = null;
            }

            /// <summary>
            /// Get or sets the number of leaves in the tree
            /// </summary>
            public int Leaves
            {
                get { return this._leafCount; }
                set { this._leafCount = value; }
            }

            /// <summary>
            /// Return the array of reducible nodes
            /// </summary>
            protected OctreeNode[] ReducibleNodes
            {
                get { return this._reducibleNodes; }
            }

            /// <summary>
            /// Keep track of the previous node that was quantized
            /// </summary>
            /// <param name="node">The node last quantized</param>
            protected void TrackPrevious(OctreeNode node)
            {
                this._previousNode = node;
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
                this._root.ConstructPalette(palette, ref paletteIndex);

                // And return the palette
                return palette;
            }

            /// <summary>
            /// Get the palette index for the passed colour
            /// </summary>
            /// <param name="pixel"></param>
            /// <returns></returns>
            public int GetPaletteIndex(Color32 pixel)
            {
                return this._root.GetPaletteIndex(pixel, 0);
            }

            /// <summary>
            /// Mask used when getting the appropriate pixels for a given node
            /// </summary>
            private static int[] mask = new int[8] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

            /// <summary>
            /// The root of the octree
            /// </summary>
            private OctreeNode _root;

            /// <summary>
            /// Number of leaves in the tree
            /// </summary>
            private int _leafCount;

            /// <summary>
            /// Array of reducible nodes
            /// </summary>
            private OctreeNode[] _reducibleNodes;

            /// <summary>
            /// Maximum number of significant bits in the image
            /// </summary>
            private int _maxColorBits;

            /// <summary>
            /// Store the last node quantized
            /// </summary>
            private OctreeNode _previousNode;

            /// <summary>
            /// Cache the previous color quantized
            /// </summary>
            private int _previousColor;

            /// <summary>
            /// Class which encapsulates each node in the tree
            /// </summary>
            protected class OctreeNode
            {
                /// <summary>
                /// Construct the node
                /// </summary>
                /// <param name="level">The level in the tree = 0 - 7</param>
                /// <param name="colorBits">The number of significant color bits in the image</param>
                /// <param name="octree">The tree to which this node belongs</param>
                public OctreeNode(int level, int colorBits, Octree octree)
                {
                    // Construct the new node
                    this._leaf = (level == colorBits);

                    this._red = _green = _blue = 0;
                    this._pixelCount = 0;

                    // If a leaf, increment the leaf count
                    if (this._leaf)
                    {
                        octree.Leaves++;
                        this._nextReducible = null;
                        this._children = null;
                    }
                    else
                    {
                        // Otherwise add this to the reducible nodes
                        this._nextReducible = octree.ReducibleNodes[level];
                        octree.ReducibleNodes[level] = this;
                        this._children = new OctreeNode[8];
                    }
                }

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
                    if (this._leaf)
                    {
                        Increment(pixel);
                        // Setup the previous node
                        octree.TrackPrevious(this);
                    }
                    else
                    {
                        // Go to the next level down in the tree
                        int shift = 7 - level;
                        int index = ((pixel.Red & mask[level]) >> (shift - 2)) |
                            ((pixel.Green & mask[level]) >> (shift - 1)) |
                            ((pixel.Blue & mask[level]) >> (shift));

                        OctreeNode child = this._children[index];

                        if (null == child)
                        {
                            // Create a new child node & store in the array
                            child = new OctreeNode(level + 1, colorBits, octree);
                            this._children[index] = child;
                        }

                        // Add the color to the child node
                        child.AddColor(pixel, colorBits, level + 1, octree);
                    }

                }

                /// <summary>
                /// Get or Sets the next reducible node
                /// </summary>
                public OctreeNode NextReducible
                {
                    get { return _nextReducible; }
                    set { _nextReducible = value; }
                }

                /// <summary>
                /// Return the child nodes
                /// </summary>
                public OctreeNode[] Children
                {
                    get { return _children; }
                }

                /// <summary>
                /// Reduce this node by removing all of its children
                /// </summary>
                /// <returns>The number of leaves removed</returns>
                public int Reduce()
                {
                    this._red = this._green = this._blue = 0;
                    int children = 0;

                    // Loop through all children and add their information to this node
                    for (int index = 0; index < 8; index++)
                    {
                        if (null != this._children[index])
                        {
                            this._red += this._children[index]._red;
                            this._green += this._children[index]._green;
                            this._blue += this._children[index]._blue;
                            this._pixelCount += this._children[index]._pixelCount;
                            ++children;
                            this._children[index] = null;
                        }
                    }

                    // Now change this to a leaf node
                    this._leaf = true;

                    // Return the number of nodes to decrement the leaf count by
                    return children - 1;
                }

                /// <summary>
                /// Traverse the tree, building up the color palette
                /// </summary>
                /// <param name="palette">The palette</param>
                /// <param name="paletteIndex">The current palette index</param>
                public void ConstructPalette(ArrayList palette, ref int paletteIndex)
                {
                    if (_leaf)
                    {
                        // Consume the next palette index
                        _paletteIndex = paletteIndex++;

                        // And set the color of the palette entry
                        palette.Add(Color.FromArgb(_red / _pixelCount, _green / _pixelCount, _blue / _pixelCount));
                    }
                    else
                    {
                        // Loop through children looking for leaves
                        for (int index = 0; index < 8; index++)
                        {
                            if (null != _children[index])
                                _children[index].ConstructPalette(palette, ref paletteIndex);
                        }
                    }
                }

                /// <summary>
                /// Return the palette index for the passed color
                /// </summary>
                public int GetPaletteIndex(Color32 pixel, int level)
                {
                    int paletteIndex = _paletteIndex;

                    if (!_leaf)
                    {
                        int shift = 7 - level;
                        int index = ((pixel.Red & mask[level]) >> (shift - 2)) |
                            ((pixel.Green & mask[level]) >> (shift - 1)) |
                            ((pixel.Blue & mask[level]) >> (shift));

                        if (null != _children[index])
                        {
                            paletteIndex = _children[index].GetPaletteIndex(pixel, level + 1);
                        }
                        else
                        {
                            throw new Exception("Didn't expect this!");
                        }
                    }

                    return paletteIndex;
                }

                /// <summary>
                /// Increment the pixel count and add to the color information
                /// </summary>
                public void Increment(Color32 pixel)
                {
                    this._pixelCount++;
                    this._red += pixel.Red;
                    this._green += pixel.Green;
                    this._blue += pixel.Blue;
                }

                /// <summary>
                /// Flag indicating that this is a leaf node
                /// </summary>
                private bool _leaf;

                /// <summary>
                /// Number of pixels in this node
                /// </summary>
                private int _pixelCount;

                /// <summary>
                /// Red component
                /// </summary>
                private int _red;

                /// <summary>
                /// Green Component
                /// </summary>
                private int _green;

                /// <summary>
                /// Blue component
                /// </summary>
                private int _blue;

                /// <summary>
                /// Pointers to any child nodes
                /// </summary>
                private OctreeNode[] _children;

                /// <summary>
                /// Pointer to next reducible node
                /// </summary>
                private OctreeNode _nextReducible;

                /// <summary>
                /// The index of this node in the palette
                /// </summary>
                private int _paletteIndex;
            }
        }
    }
}
