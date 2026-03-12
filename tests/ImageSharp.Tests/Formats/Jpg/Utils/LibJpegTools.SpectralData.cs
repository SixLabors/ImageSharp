// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

internal static partial class LibJpegTools
{
    /// <summary>
    /// Stores spectral jpeg component data in libjpeg-compatible style.
    /// </summary>
    public class SpectralData : IEquatable<SpectralData>
    {
        public int ComponentCount { get; }

        public ComponentData[] Components { get; }

        internal SpectralData(ComponentData[] components)
        {
            this.ComponentCount = components.Length;
            this.Components = components;
        }

        public Image<Rgba32> TryCreateRGBSpectralImage()
        {
            if (this.ComponentCount != 3)
            {
                return null;
            }

            ComponentData c0 = this.Components[0];
            ComponentData c1 = this.Components[1];
            ComponentData c2 = this.Components[2];

            if (c0.Size != c1.Size || c1.Size != c2.Size)
            {
                return null;
            }

            Image<Rgba32> result = new(c0.WidthInBlocks * 8, c0.HeightInBlocks * 8);

            for (int by = 0; by < c0.HeightInBlocks; by++)
            {
                for (int bx = 0; bx < c0.WidthInBlocks; bx++)
                {
                    this.WriteToImage(bx, by, result);
                }
            }

            return result;
        }

        internal void WriteToImage(int bx, int by, Image<Rgba32> image)
        {
            ComponentData c0 = this.Components[0];
            ComponentData c1 = this.Components[1];
            ComponentData c2 = this.Components[2];

            Block8x8 block0 = c0.SpectralBlocks[bx, by];
            Block8x8 block1 = c1.SpectralBlocks[bx, by];
            Block8x8 block2 = c2.SpectralBlocks[bx, by];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float val0 = c0.GetBlockValue(block0, x, y);
                    float val1 = c0.GetBlockValue(block1, x, y);
                    float val2 = c0.GetBlockValue(block2, x, y);

                    Vector4 v = new(val0, val1, val2, 1);
                    Rgba32 color = Rgba32.FromVector4(v);

                    int yy = (by * 8) + y;
                    int xx = (bx * 8) + x;
                    image[xx, yy] = color;
                }
            }
        }

        public bool Equals(SpectralData other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.ComponentCount != other.ComponentCount)
            {
                return false;
            }

            for (int i = 0; i < this.ComponentCount; i++)
            {
                ComponentData a = this.Components[i];
                ComponentData b = other.Components[i];
                if (!a.Equals(b))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => obj is SpectralData other && this.Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.ComponentCount * 397) ^ (this.Components?[0].GetHashCode() ?? 0);
            }
        }

        public static bool operator ==(SpectralData left, SpectralData right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left.Equals(right);
        }

        public static bool operator !=(SpectralData left, SpectralData right) => !(left == right);
    }
}
