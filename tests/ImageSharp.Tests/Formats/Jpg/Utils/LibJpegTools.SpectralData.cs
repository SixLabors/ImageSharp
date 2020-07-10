// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    internal static partial class LibJpegTools
    {
        /// <summary>
        /// Stores spectral jpeg component data in libjpeg-compatible style.
        /// </summary>
        public class SpectralData : IEquatable<SpectralData>
        {
            public int ComponentCount { get; private set; }

            public LibJpegTools.ComponentData[] Components { get; private set; }

            internal SpectralData(LibJpegTools.ComponentData[] components)
            {
                this.ComponentCount = components.Length;
                this.Components = components;
            }

            public static SpectralData LoadFromImageSharpDecoder(JpegDecoderCore decoder)
            {
                JpegComponent[] srcComponents = decoder.Frame.Components;
                LibJpegTools.ComponentData[] destComponents = srcComponents.Select(LibJpegTools.ComponentData.Load).ToArray();

                return new SpectralData(destComponents);
            }

            public Image<Rgba32> TryCreateRGBSpectralImage()
            {
                if (this.ComponentCount != 3)
                {
                    return null;
                }

                LibJpegTools.ComponentData c0 = this.Components[0];
                LibJpegTools.ComponentData c1 = this.Components[1];
                LibJpegTools.ComponentData c2 = this.Components[2];

                if (c0.Size != c1.Size || c1.Size != c2.Size)
                {
                    return null;
                }

                var result = new Image<Rgba32>(c0.WidthInBlocks * 8, c0.HeightInBlocks * 8);

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
                LibJpegTools.ComponentData c0 = this.Components[0];
                LibJpegTools.ComponentData c1 = this.Components[1];
                LibJpegTools.ComponentData c2 = this.Components[2];

                Block8x8 block0 = c0.SpectralBlocks[bx, by];
                Block8x8 block1 = c1.SpectralBlocks[bx, by];
                Block8x8 block2 = c2.SpectralBlocks[bx, by];

                float d0 = c0.MaxVal - c0.MinVal;
                float d1 = c1.MaxVal - c1.MinVal;
                float d2 = c2.MaxVal - c2.MinVal;

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        float val0 = c0.GetBlockValue(block0, x, y);
                        float val1 = c0.GetBlockValue(block1, x, y);
                        float val2 = c0.GetBlockValue(block2, x, y);

                        var v = new Vector4(val0, val1, val2, 1);
                        Rgba32 color = default;
                        color.FromVector4(v);

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
                    LibJpegTools.ComponentData a = this.Components[i];
                    LibJpegTools.ComponentData b = other.Components[i];
                    if (!a.Equals(b))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is SpectralData other && this.Equals(other);
            }

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

            public static bool operator !=(SpectralData left, SpectralData right)
            {
                return !(left == right);
            }
        }
    }
}
