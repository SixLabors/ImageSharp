// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorQuantizer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The color quantizer.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;
    #endregion

    /// <summary>
    /// The color quantizer.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
    public static class ColorQuantizer
    {
        #region Quantize methods
        /// <summary>The quantize.</summary>
        /// <param name="image">The image.</param>
        /// <param name="bitmapPixelFormat">The bitmap pixel format.</param>
        /// <returns>The quantized image with the recalculated color palette.</returns>
        public static Bitmap Quantize(Image image, PixelFormat bitmapPixelFormat)
        {
            // Use dither by default
            return Quantize(image, bitmapPixelFormat, true);
        }

        /// <summary>The quantize.</summary>
        /// <param name="image">The image.</param>
        /// <param name="pixelFormat">The pixel format.</param>
        /// <param name="useDither">The use dither.</param>
        /// <returns>The quantized image with the recalculated color palette.</returns>
        public static Bitmap Quantize(Image image, PixelFormat pixelFormat, bool useDither)
        {
            Bitmap tryBitmap = image as Bitmap;

            if (tryBitmap != null && tryBitmap.PixelFormat == PixelFormat.Format32bppArgb)
            {
                // The image passed to us is ALREADY a bitmap in the right format. No need to create
                // a copy and work from there.
                return DoQuantize(tryBitmap, pixelFormat, useDither);
            }

            // We use these values a lot
            int width = image.Width;
            int height = image.Height;
            Rectangle sourceRect = Rectangle.FromLTRB(0, 0, width, height);

            // Create a 24-bit rgb version of the source image
            using (Bitmap bitmapSource = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics grfx = Graphics.FromImage(bitmapSource))
                {
                    grfx.DrawImage(image, sourceRect, 0, 0, width, height, GraphicsUnit.Pixel);
                }

                return DoQuantize(bitmapSource, pixelFormat, useDither);
            }
        }

        /// <summary>
        /// Does the quantize.
        /// </summary>
        /// <param name="bitmapSource">The bitmap source.</param>
        /// <param name="pixelFormat">The pixel format.</param>
        /// <param name="useDither">if set to <c>true</c> [use dither].</param>
        /// <returns>The quantized image with the recalculated color palette.</returns>
        private static Bitmap DoQuantize(Bitmap bitmapSource, PixelFormat pixelFormat, bool useDither)
        {
            // We use these values a lot
            int width = bitmapSource.Width;
            int height = bitmapSource.Height;
            Rectangle sourceRect = Rectangle.FromLTRB(0, 0, width, height);

            Bitmap bitmapOptimized = null;

            try
            {
                // Create a bitmap with the same dimensions and the desired format
                bitmapOptimized = new Bitmap(width, height, pixelFormat);

                // Lock the bits of the source image for reading.
                // we will need to write if we do the dither.
                BitmapData bitmapDataSource = bitmapSource.LockBits(
                    sourceRect,
                    ImageLockMode.ReadWrite,
                    PixelFormat.Format32bppArgb);

                try
                {
                    // Perform the first pass, which generates the octree data
                    // Create an Octree
                    Octree octree = new Octree(pixelFormat);

                    // Stride might be negative, indicating inverted row order.
                    // Allocate a managed buffer for the pixel data, and copy it from the unmanaged pointer.
                    int strideSource = Math.Abs(bitmapDataSource.Stride);
                    byte[] sourceDataBuffer = new byte[strideSource * height];
                    Marshal.Copy(bitmapDataSource.Scan0, sourceDataBuffer, 0, sourceDataBuffer.Length);

                    // We could skip every other row and/or every other column when sampling the colors
                    // of the source image, rather than hitting every other pixel. It doesn't seem to
                    // degrade the resulting image too much. But it doesn't really help the performance
                    // too much because the majority of the time seems to be spent in other places.

                    // For every row
                    int rowStartSource = 0;
                    for (int ndxRow = 0; ndxRow < height; ndxRow += 1)
                    {
                        // For each column
                        for (int ndxCol = 0; ndxCol < width; ndxCol += 1)
                        {
                            // Add the color (4 bytes per pixel - ARGB)
                            Pixel pixel = GetSourcePixel(sourceDataBuffer, rowStartSource, ndxCol);
                            octree.AddColor(pixel);
                        }

                        rowStartSource += strideSource;
                    }

                    // Get the optimized colors
                    Color[] colors = octree.GetPaletteColors();

                    // Set the palette from the octree
                    ColorPalette palette = bitmapOptimized.Palette;
                    for (var ndx = 0; ndx < palette.Entries.Length; ++ndx)
                    {
                        // Use the colors we calculated
                        // for the rest, just set to transparent
                        palette.Entries[ndx] = (ndx < colors.Length)
                            ? colors[ndx]
                            : Color.Transparent;
                    }

                    bitmapOptimized.Palette = palette;

                    // Lock the bits of the optimized bitmap for writing.
                    // we will also need to read if we are doing 1bpp or 4bpp
                    BitmapData bitmapDataOutput = bitmapOptimized.LockBits(sourceRect, ImageLockMode.ReadWrite, pixelFormat);
                    try
                    {
                        // Create a managed array for the destination bytes given the desired color depth
                        // and marshal the unmanaged data to the managed array
                        int strideOutput = Math.Abs(bitmapDataOutput.Stride);
                        byte[] bitmapOutputBuffer = new byte[strideOutput * height];

                        // For each source pixel, compute the appropriate color index
                        rowStartSource = 0;
                        int rowStartOutput = 0;

                        for (int ndxRow = 0; ndxRow < height; ++ndxRow)
                        {
                            // For each column
                            for (int ndxCol = 0; ndxCol < width; ++ndxCol)
                            {
                                // Get the source color
                                Pixel pixel = GetSourcePixel(sourceDataBuffer, rowStartSource, ndxCol);

                                // Get the closest palette index
                                int paletteIndex = octree.GetPaletteIndex(pixel);

                                // If we want to dither and this isn't the transparent pixel
                                if (useDither && pixel.Alpha != 0)
                                {
                                    // Calculate the error
                                    Color paletteColor = colors[paletteIndex];
                                    int deltaRed = pixel.Red - paletteColor.R;
                                    int deltaGreen = pixel.Green - paletteColor.G;
                                    int deltaBlue = pixel.Blue - paletteColor.B;

                                    // Propagate the dither error. 
                                    // we'll use a standard Floyd-Steinberg matrix (1/16):
                                    // | 0 0 0 |
                                    // | 0 x 7 |
                                    // | 3 5 1 |

                                    // Make sure we're not on the right-hand edge
                                    if (ndxCol + 1 < width)
                                    {
                                        DitherSourcePixel(sourceDataBuffer, rowStartSource, ndxCol + 1, deltaRed, deltaGreen, deltaBlue, 7);
                                    }

                                    // Make sure we're not already on the bottom row
                                    if (ndxRow + 1 < height)
                                    {
                                        int nextRow = rowStartSource + strideSource;

                                        // Make sure we're not on the left-hand column
                                        if (ndxCol > 0)
                                        {
                                            // Down one row, but back one pixel
                                            DitherSourcePixel(sourceDataBuffer, nextRow, ndxCol - 1, deltaRed, deltaGreen, deltaBlue, 3);
                                        }

                                        // pixel directly below us
                                        DitherSourcePixel(sourceDataBuffer, nextRow, ndxCol, deltaRed, deltaGreen, deltaBlue, 5);

                                        // Make sure we're not on the right-hand column
                                        if (ndxCol + 1 < width)
                                        {
                                            // Down one row, but right one pixel
                                            DitherSourcePixel(sourceDataBuffer, nextRow, ndxCol + 1, deltaRed, deltaGreen, deltaBlue, 1);
                                        }
                                    }
                                }

                                // Set the bitmap index based on the format
                                switch (pixelFormat)
                                {
                                    case PixelFormat.Format8bppIndexed:
                                        // Each byte is a palette index
                                        bitmapOutputBuffer[rowStartOutput + ndxCol] = (byte)paletteIndex;
                                        break;

                                    case PixelFormat.Format4bppIndexed:
                                        // Each byte contains two pixels
                                        bitmapOutputBuffer[rowStartOutput + (ndxCol >> 1)] |= ((ndxCol & 1) == 1)
                                            ? (byte)(paletteIndex & 0x0f) // lower nibble
                                            : (byte)(paletteIndex << 4);  // upper nibble
                                        break;

                                    case PixelFormat.Format1bppIndexed:
                                        // Each byte contains eight pixels
                                        if (paletteIndex != 0)
                                        {
                                            bitmapOutputBuffer[rowStartOutput + (ndxCol >> 3)] |= (byte)(0x80 >> (ndxCol & 0x07));
                                        }

                                        break;
                                }
                            }

                            rowStartSource += strideSource;
                            rowStartOutput += strideOutput;
                        }

                        // Now copy the calculated pixel bytes from the managed array to the unmanaged bitmap
                        Marshal.Copy(bitmapOutputBuffer, 0, bitmapDataOutput.Scan0, bitmapOutputBuffer.Length);
                    }
                    finally
                    {
                        bitmapOptimized.UnlockBits(bitmapDataOutput);
                        bitmapDataOutput = null;
                    }
                }
                finally
                {
                    bitmapSource.UnlockBits(bitmapDataSource);
                    bitmapDataSource = null;
                }
            }
            catch (Exception)
            {
                // If any exception is thrown, dispose of the bitmap object
                // we've been working on before we rethrow and bail
                if (bitmapOptimized != null)
                {
                    bitmapOptimized.Dispose();
                }

                throw;
            }

            // Caller is responsible for disposing of this bitmap!
            return bitmapOptimized;
        }

        /// <summary>
        /// Dithers the source pixel.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="rowStart">The row start.</param>
        /// <param name="col">The column.</param>
        /// <param name="deltaRed">The delta red.</param>
        /// <param name="deltaGreen">The delta green.</param>
        /// <param name="deltaBlue">The delta blue.</param>
        /// <param name="weight">The weight.</param>
        private static void DitherSourcePixel(byte[] buffer, int rowStart, int col, int deltaRed, int deltaGreen, int deltaBlue, int weight)
        {
            int colorIndex = rowStart + (col * 4);
            buffer[colorIndex + 2] = ChannelAdjustment(buffer[colorIndex + 2], (deltaRed * weight) >> 4);
            buffer[colorIndex + 1] = ChannelAdjustment(buffer[colorIndex + 1], (deltaGreen * weight) >> 4);
            buffer[colorIndex] = ChannelAdjustment(buffer[colorIndex], (deltaBlue * weight) >> 4);
        }

        /// <summary>
        /// Gets the source pixel.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="rowStart">The row start.</param>
        /// <param name="col">The column.</param>
        /// <returns>The source pixel.</returns>
        private static Pixel GetSourcePixel(byte[] buffer, int rowStart, int col)
        {
            int colorIndex = rowStart + (col * 4);
            return new Pixel
            {
                Alpha = buffer[colorIndex + 3],
                Red = buffer[colorIndex + 2],
                Green = buffer[colorIndex + 1],
                Blue = buffer[colorIndex]
            };
        }

        #endregion

        /// <summary>Gets the channel adjustment.</summary>
        /// <param name="current">The current.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The channel adjustment.</returns>
        private static byte ChannelAdjustment(byte current, int offset)
        {
            return (byte)Math.Min(255, Math.Max(0, current + offset));
        }

        #region Octree class

        /// <summary>data structure for storing and reducing colors used in the source image</summary>
        private class Octree
        {
            /// <summary>The m_max colors.</summary>
            private readonly int octreeMaxColors;

            /// <summary>The m_reducible nodes.</summary>
            private readonly OctreeNode[] octreeReducibleNodes;

            /// <summary>The m_color count.</summary>
            private int octreeColorCount;

            /// <summary>The m_has transparent.</summary>
            private bool octreeHasTransparent;

            /// <summary>The m_last argb.</summary>
            private int octreeLastArgb;

            /// <summary>The m_last node.</summary>
            private OctreeNode octreeLastNode;

            /// <summary>The m_palette.</summary>
            private Color[] octreePalette;

            /// <summary>The m_root.</summary>
            private OctreeNode octreeRoot;

            /// <summary>Initializes a new instance of the <see cref="Octree"/> class. Constructor</summary>
            /// <param name="pixelFormat">desired pixel format</param>
            internal Octree(PixelFormat pixelFormat)
            {
                // figure out the maximum colors from the pixel format passed in
                switch (pixelFormat)
                {
                    case PixelFormat.Format1bppIndexed:
                        this.octreeMaxColors = 2;
                        break;

                    case PixelFormat.Format4bppIndexed:
                        this.octreeMaxColors = 16;
                        break;

                    case PixelFormat.Format8bppIndexed:
                        this.octreeMaxColors = 256;
                        break;

                    default:
                        throw new ArgumentException("Invalid Pixel Format", "pixelFormat");
                }

                // we need a list for each level that may have reducible nodes.
                // since the last level (level 7) is only made up of leaf nodes,
                // we don't need an array entry for it.
                this.octreeReducibleNodes = new OctreeNode[7];

                // add the initial level-0 root node
                this.octreeRoot = new OctreeNode(0, this);
            }

            /// <summary>Add the given pixel color to the octree</summary>
            /// <param name="pixel">points to the pixel color we want to add</param>
            internal void AddColor(Pixel pixel)
            {
                // If the A value is non-zero (ignore the transparent color)
                if (pixel.Alpha > 0)
                {
                    // If we have a previous node and this color is the same as the last...
                    if (this.octreeLastNode != null && pixel.Argb == this.octreeLastArgb)
                    {
                        // Just add this color to the same last node
                        this.octreeLastNode.AddColor(pixel);
                    }
                    else
                    {
                        // Just start at the root. If a new color is added,
                        // add one to the count (otherwise 0).
                        this.octreeColorCount += this.octreeRoot.AddColor(pixel) ? 1 : 0;
                    }
                }
                else
                {
                    // Flag that we have a transparent color.
                    this.octreeHasTransparent = true;
                }
            }

            /// <summary>
            /// Given a pixel color, return the index of the palette entry
            /// we want to use in the reduced image. If the color is not in the 
            /// octree, OctreeNode.GetPaletteIndex will return a negative number.
            /// In that case, we will have to calculate the palette index the brute-force
            /// method by computing the least distance to each color in the palette array.
            /// </summary>
            /// <param name="pixel">pointer to the pixel color we want to look up</param>
            /// <returns>index of the palette entry we want to use for this color</returns>
            internal int GetPaletteIndex(Pixel pixel)
            {
                int paletteIndex = 0;

                // transparent is always the first entry, so if this is transparent,
                // don't do anything.
                if (pixel.Alpha > 0)
                {
                    paletteIndex = this.octreeRoot.GetPaletteIndex(pixel);

                    // returns -1 if this value isn't in the octree.
                    if (paletteIndex < 0)
                    {
                        // Use the brute-force method of calculating the closest color
                        // in the palette to the one we want
                        int minDistance = int.MaxValue;
                        for (int ndx = 0; ndx < this.octreePalette.Length; ++ndx)
                        {
                            Color paletteColor = this.octreePalette[ndx];

                            // Calculate the delta for each channel
                            int deltaRed = pixel.Red - paletteColor.R;
                            int deltaGreen = pixel.Green - paletteColor.G;
                            int deltaBlue = pixel.Blue - paletteColor.B;

                            // Calculate the distance-squared by summing each channel's square
                            int distance = (deltaRed * deltaRed) + (deltaGreen * deltaGreen) + (deltaBlue * deltaBlue);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                paletteIndex = ndx;
                            }
                        }
                    }
                }

                return paletteIndex;
            }

            /// <summary>
            /// Return a color palette for the computed octree.
            /// </summary>
            /// <returns>A color palette for the computed octree</returns>
            internal Color[] GetPaletteColors()
            {
                // If we haven't already computed it, compute it now
                if (this.octreePalette == null)
                {
                    // Start at the second-to-last reducible level
                    int reductionLevel = this.octreeReducibleNodes.Length - 1;

                    // We want to subtract one from the target if we have a transparent
                    // bit because we want to save room for that special color
                    int targetCount = this.octreeMaxColors - (this.octreeHasTransparent ? 1 : 0);

                    // While we still have more colors than the target...
                    while (this.octreeColorCount > targetCount)
                    {
                        // Find the first reducible node, starting with the last level
                        // that can have reducible nodes
                        while (reductionLevel > 0 && this.octreeReducibleNodes[reductionLevel] == null)
                        {
                            --reductionLevel;
                        }

                        if (this.octreeReducibleNodes[reductionLevel] == null)
                        {
                            // Shouldn't get here
                            break;
                        }

                        // We should have a node now
                        OctreeNode newLeaf = this.octreeReducibleNodes[reductionLevel];
                        this.octreeReducibleNodes[reductionLevel] = newLeaf.NextReducibleNode;
                        this.octreeColorCount -= newLeaf.Reduce() - 1;
                    }

                    if (reductionLevel == 0 && !this.octreeHasTransparent)
                    {
                        // If this was the top-most level, we now only have a single color
                        // representing the average. That's not what we want.
                        // use just black and white
                        this.octreePalette = new Color[2];
                        this.octreePalette[0] = Color.Black;
                        this.octreePalette[1] = Color.White;

                        // And empty the octree so it always picks the closer of the black and white entries
                        this.octreeRoot = new OctreeNode(0, this);
                    }
                    else
                    {
                        // Now walk the tree, adding all the remaining colors to the list
                        int paletteIndex = 0;
                        this.octreePalette = new Color[this.octreeColorCount + (this.octreeHasTransparent ? 1 : 0)];

                        // Add the transparent color if we need it
                        if (this.octreeHasTransparent)
                        {
                            this.octreePalette[paletteIndex++] = Color.Transparent;
                        }

                        // Have the nodes insert their leaf colors
                        this.octreeRoot.AddColorsToPalette(this.octreePalette, ref paletteIndex);
                    }
                }

                return this.octreePalette;
            }

            /// <summary>set up the values we need to reuse the given pointer if the next color is argb</summary>
            /// <param name="node">last node to which we added a color</param>
            /// <param name="argb">last color we added</param>
            private void SetLastNode(OctreeNode node, int argb)
            {
                this.octreeLastNode = node;
                this.octreeLastArgb = argb;
            }

            /// <summary>When a reducible node is added, this method is called to add it to the appropriate
            /// reducible node list (given its level)</summary>
            /// <param name="reducibleNode">node to add to a reducible list</param>
            private void AddReducibleNode(OctreeNode reducibleNode)
            {
                // hook this node into the front of the list. 
                // this means the last one added will be the first in the list.
                reducibleNode.NextReducibleNode = this.octreeReducibleNodes[reducibleNode.Level];
                this.octreeReducibleNodes[reducibleNode.Level] = reducibleNode;
            }

            #region OctreeNode class

            /// <summary>Node for an Octree structure</summary>
            private class OctreeNode
            {
                /// <summary>The s_level masks.</summary>
                private static readonly byte[] NodeLevelMasks = { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

                /// <summary>The m_level.</summary>
                private readonly int nodeLevel;

                /// <summary>The m_octree.</summary>
                private readonly Octree nodeOctree;

                /// <summary>The m_blue sum.</summary>
                private int nodeBlueSum;

                /// <summary>The m_child nodes.</summary>
                private OctreeNode[] nodeChildNodes;

                /// <summary>The m_green sum.</summary>
                private int nodeGreenSum;

                /// <summary>The m_is leaf.</summary>
                private bool nodeIsLeaf;

                /// <summary>The m_palette index.</summary>
                private int nodePaletteIndex;

                /// <summary>
                /// The pixel count.Information we need to calculate the average color for a set of pixels
                /// </summary>
                private int nodePixelCount;

                /// <summary>The m_red sum.</summary>
                private int nodeRedSum;

                /// <summary>Initializes a new instance of the <see cref="OctreeNode"/> class. Constructor</summary>
                /// <param name="level">level for this node</param>
                /// <param name="octree">owning octree</param>
                internal OctreeNode(int level, Octree octree)
                {
                    this.nodeOctree = octree;
                    this.nodeLevel = level;

                    // Since there are only eight levels, if we get to level 7
                    // We automatically make this a leaf node
                    this.nodeIsLeaf = level == 7;

                    if (!this.nodeIsLeaf)
                    {
                        // Create the child array
                        this.nodeChildNodes = new OctreeNode[8];

                        // Add it to the tree's reducible node list
                        this.nodeOctree.AddReducibleNode(this);
                    }
                }

                /// <summary>Gets Level.</summary>
                internal int Level
                {
                    get { return this.nodeLevel; }
                }

                /// <summary>
                /// Gets or sets NextReducibleNode.
                /// Once we compute a palette, this will be set
                /// to the palette index associated with this leaf node.
                /// Nodes are arranged in linked lists of reducible nodes for a given level.
                /// this field and property is used to traverse that list.
                /// </summary>
                internal OctreeNode NextReducibleNode { get; set; }

                /// <summary>
                /// Gets the average color for this node.
                /// </summary>
                private Color NodeColor
                {
                    get
                    {
                        // Average color is the sum of each channel divided by the pixel count
                        return Color.FromArgb(
                            this.nodeRedSum / this.nodePixelCount,
                            this.nodeGreenSum / this.nodePixelCount,
                            this.nodeBlueSum / this.nodePixelCount);
                    }
                }

                /// <summary>
                /// Add the given color to this node if it is a leaf, otherwise recurse 
                /// down the appropriate child
                /// </summary>
                /// <param name="pixel">color to add</param>
                /// <returns>true if a new color was added to the octree</returns>
                internal bool AddColor(Pixel pixel)
                {
                    bool colorAdded;
                    if (this.nodeIsLeaf)
                    {
                        // Increase the pixel count for this node, and if
                        // the result is 1, then this is a new color
                        colorAdded = ++this.nodePixelCount == 1;

                        // Add the color to the running sum for this node
                        this.nodeRedSum += pixel.Red;
                        this.nodeGreenSum += pixel.Green;
                        this.nodeBlueSum += pixel.Blue;

                        // Set the last node so we can quickly process adjacent pixels
                        // with the same color
                        this.nodeOctree.SetLastNode(this, pixel.Argb);
                    }
                    else
                    {
                        // Get the index at this level for the rgb values
                        int childIndex = this.GetChildIndex(pixel);

                        // If there is no child, add one now to the next level
                        if (this.nodeChildNodes[childIndex] == null)
                        {
                            this.nodeChildNodes[childIndex] = new OctreeNode(this.nodeLevel + 1, this.nodeOctree);
                        }

                        // Recurse...
                        colorAdded = this.nodeChildNodes[childIndex].AddColor(pixel);
                    }

                    return colorAdded;
                }

                /// <summary>
                /// Given a source color, return the palette index to use for the reduced image.
                /// Returns -1 if the color is not represented in the octree (this happens if
                /// the color has been dithered into a new color that did not appear in the 
                /// original image when the octree was formed in pass 1.
                /// </summary>
                /// <param name="pixel">source color to look up</param>
                /// <returns>palette index to use</returns>
                internal int GetPaletteIndex(Pixel pixel)
                {
                    int paletteIndex = -1;
                    if (this.nodeIsLeaf)
                    {
                        // Use this leaf node's palette index
                        paletteIndex = this.nodePaletteIndex;
                    }
                    else
                    {
                        // Get the index at this level for the rgb values
                        var childIndex = this.GetChildIndex(pixel);
                        if (this.nodeChildNodes[childIndex] != null)
                        {
                            // Recurse...
                            paletteIndex = this.nodeChildNodes[childIndex].GetPaletteIndex(pixel);
                        }
                    }

                    return paletteIndex;
                }

                /// <summary>Reduce this node by combining all child nodes</summary>
                /// <returns>number of nodes removed</returns>
                internal int Reduce()
                {
                    int numReduced = 0;
                    if (!this.nodeIsLeaf)
                    {
                        // For each child
                        foreach (OctreeNode node in this.nodeChildNodes)
                        {
                            if (node != null)
                            {
                                OctreeNode childNode = node;

                                // add the pixel count from the child
                                this.nodePixelCount += childNode.nodePixelCount;

                                // add the running color sums from the child
                                this.nodeRedSum += childNode.nodeRedSum;
                                this.nodeGreenSum += childNode.nodeGreenSum;
                                this.nodeBlueSum += childNode.nodeBlueSum;

                                ++numReduced;
                            }
                        }

                        this.nodeChildNodes = null;
                        this.nodeIsLeaf = true;
                    }

                    return numReduced;
                }

                /// <summary>
                /// If this is a leaf node, add its color to the palette array at the given index
                /// and increment the index. If not a leaf, recurse the children nodes.
                /// </summary>
                /// <param name="colorArray">array of colors</param>
                /// <param name="paletteIndex">index of the next empty slot in the array</param>
                internal void AddColorsToPalette(Color[] colorArray, ref int paletteIndex)
                {
                    if (this.nodeIsLeaf)
                    {
                        // Save our index and increment the running index
                        this.nodePaletteIndex = paletteIndex++;

                        // The color for this node is the average color, which is created by
                        // dividing the running sums for each channel by the pixel count
                        colorArray[this.nodePaletteIndex] = this.NodeColor;
                    }
                    else
                    {
                        // Just run through all the non-null children and recurse
                        foreach (OctreeNode node in this.nodeChildNodes)
                        {
                            if (node != null)
                            {
                                node.AddColorsToPalette(colorArray, ref paletteIndex);
                            }
                        }
                    }
                }

                /// <summary>
                /// Return the child index for a given color.
                /// Depends on which level this node is in.
                /// </summary>
                /// <param name="pixel">color pixel to compute</param>
                /// <returns>child index (0-7)</returns>
                private int GetChildIndex(Pixel pixel)
                {
                    // lvl: 0 1 2 3 4 5 6 7
                    // bit: 7 6 5 4 3 2 1 0
                    var shift = 7 - this.nodeLevel;
                    int mask = NodeLevelMasks[this.nodeLevel];
                    return ((pixel.Red & mask) >> (shift - 2)) |
                           ((pixel.Green & mask) >> (shift - 1)) |
                           ((pixel.Blue & mask) >> shift);
                }
            }
            #endregion
        }
        #endregion

        #region Pixel class for ARGB values
        /// <summary>
        /// Structure of a Format32bppArgb pixel in memory.
        /// </summary>
        private class Pixel
        {
            /// <summary>
            /// Gets or sets the blue component of the pixel.
            /// </summary>
            public byte Blue { get; set; }

            /// <summary>
            /// Gets or sets the green component of the pixel.
            /// </summary>
            public byte Green { get; set; }

            /// <summary>
            /// Gets or sets the red component of the pixel.
            /// </summary>
            public byte Red { get; set; }

            /// <summary>
            /// Gets or sets the alpha component of the pixel.
            /// </summary>
            public byte Alpha { get; set; }

            /// <summary>
            /// Gets the argb combination of the pixel.
            /// </summary>
            public int Argb
            {
                get
                {
                    return this.Alpha << 24 | this.Red << 16 | this.Green << 8 | this.Blue;
                }
            }
        }
        #endregion
    }
}
