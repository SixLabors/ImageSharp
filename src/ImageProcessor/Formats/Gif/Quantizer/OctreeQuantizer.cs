// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OctreeQuantizer.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to calculate the color palette of an image using an Octree pattern.
//   <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image using an Octree pattern.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    public class OctreeQuantizer : Quantizer
    {
        /// <summary>
        /// Maximum allowed color depth
        /// </summary>
        private readonly int maxColors;

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class.
        /// </summary>
        /// <remarks>
        /// The Octree quantizer is a two pass algorithm. The initial pass sets up the Octree,
        /// the second pass quantizes a color based on the nodes in the tree.
        /// <para>
        /// Defaults to return a maximum of 255 colors plus transparency with 8 significant bits.
        /// </para>
        /// </remarks>
        public OctreeQuantizer()
            : this(255, 8)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class. 
        /// </summary>
        /// <remarks>
        /// The Octree quantizer is a two pass algorithm. The initial pass sets up the Octree,
        /// the second pass quantizes a color based on the nodes in the tree
        /// </remarks>
        /// <param name="maxColors">The maximum number of colors to return</param>
        /// <param name="maxColorBits">The number of significant bits</param>
        public OctreeQuantizer(int maxColors, int maxColorBits)
            : base(false)
        {
            Guard.LessEquals(maxColors, 255, "maxColors");
        }
    }
}
