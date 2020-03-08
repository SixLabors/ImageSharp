using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Quantization
{
    public class ColorLookupConcepts
    {
        private LinearRgba32 linearRgba;
        private LinearVector4 linearVector4;
        private HashMap hashMap;
        private BasicMap basicMap;
        private WuMap wuMap;
        private Rgba32 color2Match;

        [GlobalSetup]
        public void Setup()
        {
            this.color2Match = new Rgba32(128, 128, 128, 255);

            this.linearRgba = LinearRgba32.Create();
            this.linearVector4 = LinearVector4.Create();
            this.hashMap = HashMap.Create();
            this.basicMap = BasicMap.Create();
            this.wuMap = WuMap.Create();
        }

        [Benchmark]
        public byte UseLinearVector4() => this.linearVector4.Match(this.color2Match);

        [Benchmark]
        public byte UseLinearRgba() => this.linearRgba.Match(this.color2Match);

        [Benchmark(Baseline = true)]
        public byte UseHashMap() => this.hashMap.Match(this.color2Match);

        [Benchmark]
        public byte UseBasicMap() => this.basicMap.Match(this.color2Match);

        [Benchmark]
        public byte UseWuMap() => this.wuMap.Match(this.color2Match);

        struct LinearRgba32
        {
            private Rgba32[] palette;

            public static LinearRgba32 Create()
            {
                LinearRgba32 result = default;
                var palette = new Rgba32[256];
                int idx = 0;
                for (int r = 0; r < 256; r += 32)
                {
                    for (int g = 0; g < 256; g += 32)
                    {
                        for (int b = 0; b < 256; b += 64)
                        {
                            palette[idx] = new Rgba32(r, g, b, 255);
                            idx++;
                        }
                    }
                }

                result.palette = palette;
                return result;
            }

            public byte Match(Rgba32 rgba)
            {
                Rgba32[] pal = this.palette;
                int minDistance = int.MaxValue;
                int minIdx = -1;

                var lookFor = new Rgba128(rgba);

                for (int i = 0; i < pal.Length; i++)
                {
                    Rgba32 paletteVec = pal[i];
                    int distance = lookFor.Distance(paletteVec);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        minIdx = i;
                    }
                }

                return (byte)minIdx;
            }

            private struct Rgba128
            {
                public int R;
                public int G;
                public int B;
                public int A;

                public Rgba128(Rgba32 x)
                {
                    this.R = x.R;
                    this.G = x.G;
                    this.B = x.B;
                    this.A = x.A;
                }

                public int Distance(Rgba32 x)
                {
                    int r = this.R - x.R;
                    int g = this.G - x.G;
                    int b = this.B - x.B;
                    int a = this.A - x.A;
                    return (r * r) + (g * g) + (b * b) + (a * a);
                }
            }
        }

        internal struct LinearVector4
        {
            private readonly Vector4[] paletteVectors;

            private LinearVector4(Configuration configuration, ReadOnlyMemory<Rgba32> palette)
            {
                this.paletteVectors = new Vector4[palette.Length];
                PixelOperations<Rgba32>.Instance.ToVector4(configuration, palette.Span, this.paletteVectors);
            }

            public static LinearVector4 Create()
            {
                var palette = new Rgba32[256];
                int idx = 0;
                for (int r = 0; r < 256; r += 32)
                {
                    for (int g = 0; g < 256; g += 32)
                    {
                        for (int b = 0; b < 256; b += 64)
                        {
                            palette[idx] = new Rgba32(r, g, b, 255);
                            idx++;
                        }
                    }
                }

                return new LinearVector4(Configuration.Default, palette);
            }

            public byte Match(Rgba32 pixel)
            {
                Vector4[] palette = this.paletteVectors;
                float minDistance = float.MaxValue;
                var pixelVec = pixel.ToVector4();
                int minIdx = -1;

                for (int i = 0; i < this.paletteVectors.Length; i++)
                {
                    Vector4 paletteVec = palette[i];
                    float distance = Vector4.DistanceSquared(pixelVec, paletteVec);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        minIdx = i;
                    }
                }

                return (byte)minIdx;
            }
        }

        struct HashMap
        {
            private Dictionary<Rgba32, byte> map;

            public static HashMap Create()
            {
                HashMap result = default;

                var map = new Dictionary<Rgba32, byte>();

                int cnt = 0;
                for (int r = 0; r < 256; r += 4)
                {
                    for (int g = 0; g < 256; g += 4)
                    {
                        for (int b = 0; b < 256; b += 4)
                        {
                            var rgba = new Rgba32(r, g, b, 255);
                            map[rgba] = (byte)cnt;
                            cnt++;
                            cnt %= 256;
                        }
                    }
                }

                result.map = map;
                return result;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public byte Match(Rgba32 rgba)
            {
                this.map.TryGetValue(rgba, out byte result);
                return result;
            }
        }

        struct BasicMap
        {
            private const int Rbits = 5;
            private const int Gbits = 6;
            private const int Bbits = 5;
            private const int Abits = 5;

            private const int Rval = 1 << Rbits;
            private const int Gval = 1 << Gbits;
            private const int Bval = 1 << Bbits;
            private const int Aval = 1 << Abits;

            private const int Rhalf = (Rval >> 1) - 1;
            private const int Ghalf = (Gval >> 1) - 1;
            private const int Bhalf = (Bval >> 1) - 1;
            private const int Ahalf = (Aval >> 1) - 1;

            private const int TableLength = Rval * Gval * Bval * Aval;

            private byte[] table;

            public static BasicMap Create()
            {
                BasicMap result = default;
                result.table = new byte[TableLength];
                return result;
            }

            public byte Match(Rgba32 rgba)
            {
                int r = ((rgba.R >> 3) + 4) & 31;
                int g = ((rgba.G >> 2) + 2) & 63;
                int b = ((rgba.B >> 3) + 4) & 31;
                int a = ((rgba.A >> 3) + 4) & 31;

                int idx =
                    (a << (Bbits + Gbits + Rbits)) +
                    (b << (Gbits + Rbits)) +
                    (g << Rbits) +
                    r;

                return this.table[idx];
            }
        }

        struct WuMap
        {
            private const int IndexBits = 5;
            private const int IndexAlphaBits = 5;
            private const int IndexCount = (1 << IndexBits) + 1;
            private const int IndexAlphaCount = (1 << IndexAlphaBits) + 1;
            private const int TableLength = IndexCount * IndexCount * IndexCount * IndexAlphaCount;

            private byte[] table;

            public static WuMap Create()
            {
                WuMap result = default;
                result.table = new byte[TableLength];
                return result;
            }

            public byte Match(Rgba32 rgba)
            {
                int r = rgba.R >> (8 - IndexBits);
                int g = rgba.G >> (8 - IndexBits);
                int b = rgba.B >> (8 - IndexBits);
                int a = rgba.A >> (8 - IndexAlphaBits);
                int idx = GetPaletteIndex(r, g, b, a);
                return this.table[idx];
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static int GetPaletteIndex(int r, int g, int b, int a)
            {
                return (r << ((IndexBits * 2) + IndexAlphaBits))
                       + (r << (IndexBits + IndexAlphaBits + 1))
                       + (g << (IndexBits + IndexAlphaBits))
                       + (r << (IndexBits * 2))
                       + (r << (IndexBits + 1))
                       + (g << IndexBits)
                       + ((r + g + b) << IndexAlphaBits)
                       + r + g + b + a;
            }
        }
    }
}
