using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup.Brian
{
    internal struct KdTreePixelMap<TPixel> : IPaletteMap<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly KdTree tree;

        /// <summary>
        /// Initializes a new instance of the <see cref="EuclideanPixelMap{TPixel}"/> struct.
        /// </summary>
        /// <param name="palette">The color palette to map from.</param>
        public KdTreePixelMap(ReadOnlyMemory<TPixel> palette)
        {
            Guard.MustBeGreaterThan(palette.Length, 0, nameof(palette));

            this.Palette = palette;
            ReadOnlySpan<TPixel> paletteSpan = this.Palette.Span;

            var points = new List<KdTree.ColorWithIndex>();
            for (int i = 0; i < paletteSpan.Length; i++)
            {
                points.Add(new KdTree.ColorWithIndex(paletteSpan[i].ToScaledVector4(), i));
            }

            this.tree = new KdTree(points);
            var tmp = this.tree.GetPointList();
        }

        public ReadOnlyMemory<TPixel> Palette { get; }


        [MethodImpl(InliningOptions.ShortMethod)]
        public byte GetPaletteIndex(TPixel color)
        {
            Vector4 vector = color.ToScaledVector4();
            KdTree.Node node = KdTree.FindNearestNeighbour(vector, this.tree.Root, out KdTree.Node snnNode);
            return (byte)node.Point.Index;
        }
    }
}
