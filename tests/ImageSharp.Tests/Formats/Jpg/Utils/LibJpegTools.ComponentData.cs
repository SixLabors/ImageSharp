// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

internal static partial class LibJpegTools
{
    /// <summary>
    /// Stores spectral blocks for jpeg components.
    /// </summary>
    public class ComponentData : IEquatable<ComponentData>, IJpegComponent
    {
        public ComponentData(int widthInBlocks, int heightInBlocks, int index)
        {
            this.HeightInBlocks = heightInBlocks;
            this.WidthInBlocks = widthInBlocks;
            this.Index = index;
            this.SpectralBlocks = Configuration.Default.MemoryAllocator.Allocate2D<Block8x8>(this.WidthInBlocks, this.HeightInBlocks);
        }

        public byte Id { get; }

        public Size Size => new(this.WidthInBlocks, this.HeightInBlocks);

        public int Index { get; }

        public Size SizeInBlocks => new(this.WidthInBlocks, this.HeightInBlocks);

        public Size SamplingFactors => throw new NotSupportedException();

        public Size SubSamplingDivisors => throw new NotSupportedException();

        public int HeightInBlocks { get; }

        public int WidthInBlocks { get; }

        public int QuantizationTableIndex => throw new NotSupportedException();

        public Buffer2D<Block8x8> SpectralBlocks { get; }

        public short MinVal { get; private set; } = short.MaxValue;

        public short MaxVal { get; private set; } = short.MinValue;

        public int HorizontalSamplingFactor => throw new NotImplementedException();

        public int VerticalSamplingFactor => throw new NotImplementedException();

        public int DcPredictor { get; set; }

        public int DcTableId { get; set; }

        public int AcTableId { get; set; }

        internal void MakeBlock(Block8x8 block, int y, int x)
        {
            block.TransposeInplace();
            this.MakeBlock(block.ToArray(), y, x);
        }

        internal void MakeBlock(short[] data, int y, int x)
        {
            this.MinVal = Math.Min(this.MinVal, data.Min());
            this.MaxVal = Math.Max(this.MaxVal, data.Max());
            this.SpectralBlocks[x, y] = Block8x8.Load(data);
        }

        public void LoadSpectralStride(Buffer2D<Block8x8> data, int strideIndex)
        {
            int startIndex = strideIndex * data.Height;

            int endIndex = Math.Min(this.HeightInBlocks, startIndex + data.Height);

            for (int y = startIndex; y < endIndex; y++)
            {
                Span<Block8x8> blockRow = data.DangerousGetRowSpan(y - startIndex);
                for (int x = 0; x < this.WidthInBlocks; x++)
                {
                    this.MakeBlock(blockRow[x], y, x);
                }
            }
        }

        public void LoadSpectral(IJpegComponent c)
        {
            Buffer2D<Block8x8> data = c.SpectralBlocks;
            for (int y = 0; y < this.HeightInBlocks; y++)
            {
                Span<Block8x8> blockRow = data.DangerousGetRowSpan(y);
                for (int x = 0; x < this.WidthInBlocks; x++)
                {
                    this.MakeBlock(blockRow[x], y, x);
                }
            }
        }

        public static ComponentData Load(JpegComponent c, int index)
        {
            ComponentData result = new(
                c.WidthInBlocks,
                c.HeightInBlocks,
                index);

            result.LoadSpectral(c);
            return result;
        }

        public Image<Rgba32> CreateGrayScaleImage()
        {
            Image<Rgba32> result = new(this.WidthInBlocks * 8, this.HeightInBlocks * 8);

            for (int by = 0; by < this.HeightInBlocks; by++)
            {
                for (int bx = 0; bx < this.WidthInBlocks; bx++)
                {
                    this.WriteToImage(bx, by, result);
                }
            }

            return result;
        }

        internal void WriteToImage(int bx, int by, Image<Rgba32> image)
        {
            Block8x8 block = this.SpectralBlocks[bx, by];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float val = this.GetBlockValue(block, x, y);

                    Vector4 v = new(val, val, val, 1);
                    Rgba32 color = Rgba32.FromVector4(v);

                    int yy = (by * 8) + y;
                    int xx = (bx * 8) + x;
                    image[xx, yy] = color;
                }
            }
        }

        internal float GetBlockValue(Block8x8 block, int x, int y)
        {
            float d = this.MaxVal - this.MinVal;
            float val = block[y, x];
            val -= this.MinVal;
            val /= d;
            return val;
        }

        public bool Equals(ComponentData other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            bool ok = this.Index == other.Index && this.HeightInBlocks == other.HeightInBlocks
                      && this.WidthInBlocks == other.WidthInBlocks;
            if (!ok)
            {
                return false;
            }

            for (int y = 0; y < this.HeightInBlocks; y++)
            {
                for (int x = 0; x < this.WidthInBlocks; x++)
                {
                    Block8x8 a = this.SpectralBlocks[x, y];
                    Block8x8 b = other.SpectralBlocks[x, y];
                    if (!a.Equals(b))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((ComponentData)obj);
        }

        public override int GetHashCode() => HashCode.Combine(this.Index, this.HeightInBlocks, this.WidthInBlocks, this.MinVal, this.MaxVal);

        public ref Block8x8 GetBlockReference(int column, int row) => throw new NotImplementedException();

        public void Init(int maxSubFactorH, int maxSubFactorV) => throw new NotImplementedException();

        public void AllocateSpectral(bool fullScan) => throw new NotImplementedException();

        public void Dispose() => throw new NotImplementedException();

        public static bool operator ==(ComponentData left, ComponentData right) => Equals(left, right);

        public static bool operator !=(ComponentData left, ComponentData right) => !Equals(left, right);
    }
}
