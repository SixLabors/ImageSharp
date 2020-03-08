// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup
{
    interface IPaletteMap<TPixel>
    {
        byte GetPaletteIndex(TPixel pixel);
    }

    internal struct HeapKdTreePaletteLookup<TPixel> : IPaletteMap<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private const int MaxPalette = 256;
        private readonly Vector4[] paletteVectors;
        private int treeEnd;

        internal readonly ushort[] PaletteIndices;

        public HeapKdTreePaletteLookup(Configuration configuration, ReadOnlyMemory<TPixel> palette)
        {
            Guard.MustBeLessThanOrEqualTo(palette.Length, MaxPalette, "palette.Length");

            var originalPaletteVectors = new Vector4[palette.Length];
            PixelOperations<TPixel>.Instance.ToVector4(configuration, palette.Span, originalPaletteVectors);
            this.paletteVectors = new Vector4[palette.Length * 2];
            this.Palette = palette;
            this.PaletteIndices = new ushort[palette.Length * 2];
            this.PaletteIndices.AsSpan().Fill(ushort.MaxValue);
            this.treeEnd = default;
            this.BuildTree(originalPaletteVectors);
        }

        public ReadOnlyMemory<TPixel> Palette { get; }

        public byte GetPaletteIndex(TPixel pixel)
        {
            var finder = new NearestNeighbourFinder(this.paletteVectors, this.PaletteIndices, pixel, this.treeEnd);
            finder.SearchSubtree(0, -1);
            return (byte)this.PaletteIndices[finder.MinIdx];
        }

        public byte GetPaletteIndex(TPixel pixel, out int steps)
        {
            var finder = new NearestNeighbourFinder(this.paletteVectors, this.PaletteIndices, pixel, this.treeEnd);
            finder.SearchSubtree(0, -1);
            steps = finder.Steps;
            return (byte)this.PaletteIndices[finder.MinIdx];
        }

        private void BuildTree(Vector4[] originalPalette)
        {
            var initial = Segment.CreateInitial(originalPalette);
            this.BuildSegment(ref initial, 0, 0);
        }

        private void BuildSegment(ref Segment segment, int currentCoord, int currentPaletteIndex)
        {
            this.treeEnd = Math.Max(this.treeEnd, currentPaletteIndex);
            (Vector4 median, int medianIndex, Segment left, Segment right) = segment.Partitionate(currentCoord);
            segment.Dispose(); // We'll no longer use this segment, return it's array to the pool

            this.paletteVectors[currentPaletteIndex] = median;
            this.PaletteIndices[currentPaletteIndex] = (byte)medianIndex;
            currentCoord = ImageMaths.Modulo4(currentCoord + 1);

            if (left.Length > 0)
            {
                this.BuildSegment(ref left, currentCoord, (currentPaletteIndex * 2) + 1);
            }

            if (right.Length > 0)
            {
                this.BuildSegment(ref right, currentCoord, (currentPaletteIndex * 2) + 2);
            }
        }

        private static float GetValue(ref Vector4 v, int coord) =>
            Unsafe.Add(ref Unsafe.As<Vector4, float>(ref v), coord);

        private struct Segment : IDisposable
        {
            private readonly Vector4[] values;
            private readonly int[] indices;

            private Segment(Vector4[] values, int[] indices,  int length)
            {
                this.values = values;
                this.indices = indices;
                this.Length = length;
            }

            public int Length { get; }

            public static Segment CreateInitial(Vector4[] originalPalette)
            {
                // Using ArrayPool, since the arrays are small:
                Vector4[] values = ArrayPool<Vector4>.Shared.Rent(originalPalette.Length);
                Array.Copy(originalPalette, values, originalPalette.Length);

                int[] indices = ArrayPool<int>.Shared.Rent(originalPalette.Length);
                for (int i = 0; i < indices.Length; i++)
                {
                    indices[i] = i;
                }

                return new Segment(values, indices, originalPalette.Length);
            }

            public (Vector4 median, int medianIndex, Segment left, Segment right) Partitionate(int coord)
            {
                DebugGuard.MustBeGreaterThan(this.Length, 0, "this.Length");

                this.SortByCoord(coord);
                int median = this.Length / 2;

                Segment left = this.Slice(0, median);
                int rightStart = median + 1;
                Segment right = this.Slice(rightStart, this.Length - rightStart);
                return (this.values[median], this.indices[median], left, right);
            }

            private Segment Slice(int start, int length)
            {
                if (length > 0)
                {
                    Vector4[] sliceValues = ArrayPool<Vector4>.Shared.Rent(length);
                    int[] sliceIndices = ArrayPool<int>.Shared.Rent(length);
                    Array.Copy(this.values, start, sliceValues, 0, length);
                    Array.Copy(this.indices, start, sliceIndices, 0, length);
                    return new Segment(sliceValues, sliceIndices, length);
                }

                return default;
            }

            private void SortByCoord(int coord)
            {
                ByCoordVectorComparer comparer = ByCoordVectorComparer.Comparers[coord];
                Array.Sort(this.values, this.indices, 0, this.Length, comparer);
            }

            public override string ToString()
            {
                var bld = new StringBuilder();
                bld.Append($"L={this.Length}|");
                Rgba32 p = default;
                for (int i = 0; i < this.Length; i++)
                {
                    p.FromVector4(this.values[i]);
                    bld.Append($"[{p.R},{p.G},{p.B},{p.A}] ");
                }

                return bld.ToString();
            }

            public void Dispose()
            {
                if (this.Length == 0)
                {
                    return;
                }

                ArrayPool<Vector4>.Shared.Return(this.values);
                ArrayPool<int>.Shared.Return(this.indices);
            }
        }

        private ref struct NearestNeighbourFinder
        {
            private readonly Span<Vector4> paletteTree;
            private readonly Span<ushort> paletteIndices;
            private Vector4 searchColor;
            private float minDist;
            private int minIdx;
            private readonly int treeEnd;

            public NearestNeighbourFinder(Vector4[] paletteTree, Span<ushort> paletteIndices, TPixel pixel, int treeEnd)
            {
                this.paletteTree = paletteTree;
                this.paletteIndices = paletteIndices;
                this.treeEnd = treeEnd;
                this.searchColor = pixel.ToVector4();
                this.minDist = float.MaxValue;
                this.minIdx = -1;
                this.Steps = 0;
            }

            public int MinIdx => this.minIdx;

            public int Steps { get; private set; }

            public void SearchSubtree(int currentIdx, int currentCoord)
            {
                if (currentIdx > this.treeEnd || this.paletteIndices[currentIdx] == ushort.MaxValue)
                {
                    return;
                }

                this.Steps++;

                currentCoord = ImageMaths.Modulo4(currentCoord + 1);

                ref Vector4 v = ref this.paletteTree[currentIdx];
                float d = Vector4.DistanceSquared(v, this.searchColor);

                if (d < this.minDist)
                {
                    this.minDist = d;
                    this.minIdx = currentIdx;
                }

                float searchVal = GetValue(ref this.searchColor, currentCoord);
                float paletteVal = GetValue(ref v, currentCoord);
                float coordDiff = searchVal - paletteVal;

                float dd = coordDiff * coordDiff;

                if (coordDiff < 0)
                {
                    // go left first:
                    this.SearchSubtree((currentIdx * 2) + 1, currentCoord);

                    // need to check both subtrees, if hypersphere intersects hyperplane:
                    if (this.minDist > dd)
                    {
                        this.SearchSubtree((currentIdx * 2) + 2, currentCoord);
                    }
                }
                else
                {
                    // go right first:
                    this.SearchSubtree((currentIdx * 2) + 2, currentCoord);

                    // need to check both subtrees, if hypersphere intersects hyperplane:
                    if (this.minDist > dd)
                    {
                        this.SearchSubtree((currentIdx * 2) + 1, currentCoord);
                    }
                }
            }
        }

        private class ByCoordVectorComparer : IComparer<Vector4>
        {
            private int coord;

            private ByCoordVectorComparer(int coord)
            {
                this.coord = coord;
            }

            public static ByCoordVectorComparer[] Comparers { get; } =
            {
                new ByCoordVectorComparer(0),
                new ByCoordVectorComparer(1),
                new ByCoordVectorComparer(2),
                new ByCoordVectorComparer(3),
            };

            public int Compare(Vector4 x, Vector4 y)
            {
                float a = GetValue(ref x, this.coord);
                float b = GetValue(ref y, this.coord);
                return Compare(a, b);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int Compare(float a, float b)
            {
                if (a < b)
                {
                    return -1;
                }

                if (a > b)
                {
                    return 1;
                }

                return 0;
            }
        }
    }
}
