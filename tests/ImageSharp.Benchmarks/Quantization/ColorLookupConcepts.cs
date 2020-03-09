using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup;
using SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup.Brian;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Quantization
{
    public class ColorLookupConcepts
    {
        private LinearRgba32Map linearRgba;
        private LinearVector4Map linearVector4Map;
        private HashMap hashMap;
        private ConcurrentHashMap concurrentMap;
        private BasicMap basicMap;
        private WuMap wuMap;
        private HeapKdTreePaletteLookup<Rgba32> heapKdMap;
        private KdTreePixelMap<Rgba32> kdMap;
        private LinearBoxesMap linearBoxes;

        private Rgba32 color1;
        private Rgba32 color2;
        private Rgba32 color3;
        private OctreeFrameQuantizer<Rgba32> octree;

        [GlobalSetup]
        public void Setup()
        {
            this.color1 = new Rgba32(128, 128, 128, 255);
            this.color2 = new Rgba32(199, 163, 51, 255);

            this.linearRgba = LinearRgba32Map.Create();
            this.linearVector4Map = LinearVector4Map.Create();
            this.hashMap = HashMap.Create();
            this.concurrentMap = ConcurrentHashMap.Create();
            this.basicMap = BasicMap.Create();
            this.wuMap = WuMap.Create();
            this.linearBoxes = LinearBoxesMap.Create();

            Rgba32[] testPalette = CreateTestPalette();
            this.color3 = testPalette[161];
            this.heapKdMap = new HeapKdTreePaletteLookup<Rgba32>(Configuration.Default, testPalette);
            this.kdMap = new KdTreePixelMap<Rgba32>(testPalette);
            this.octree = CreateOctreeFrameQuantizer();
        }

        [Benchmark]
        public int LinearVector4()
        {
            return this.linearVector4Map.Match(this.color1) +
                   this.linearVector4Map.Match(this.color2) +
                   this.linearVector4Map.Match(this.color3);
        }

        [Benchmark]
        public int LinearRgba()
        {
            return this.linearRgba.Match(this.color1) +
                   this.linearRgba.Match(this.color2) +
                   this.linearRgba.Match(this.color3);
        }

        // [Benchmark]
        public int AntonsHeapKdTree()
        {
            return this.heapKdMap.GetPaletteIndex(this.color1) +
                   this.heapKdMap.GetPaletteIndex(this.color2) +
                   this.heapKdMap.GetPaletteIndex(this.color3);
        }

        // [Benchmark]
        public int BriansNormalKdTree()
        {
            return this.kdMap.GetPaletteIndex(this.color1) +
                   this.kdMap.GetPaletteIndex(this.color2) +
                   this.kdMap.GetPaletteIndex(this.color3);
        }

        [Benchmark(Baseline = true)]
        public int Dictionary()
        {
            return this.hashMap.Match(this.color1) +
                   this.hashMap.Match(this.color2) +
                   this.hashMap.Match(this.color3);
        }

        [Benchmark]
        public int ConcurrentDictionary()
        {
            return this.concurrentMap.Match(this.color1) +
                   this.concurrentMap.Match(this.color2) +
                   this.concurrentMap.Match(this.color3);
        }

        [Benchmark]
        public int BasicBitShift()
        {
            return this.basicMap.Match(this.color1) +
                   this.basicMap.Match(this.color2) +
                   this.basicMap.Match(this.color3);
        }

        [Benchmark]
        public int BasicBitShift_NoAlpha()
        {
            return this.basicMap.MatchNoAlpha(this.color1) +
                   this.basicMap.MatchNoAlpha(this.color2) +
                   this.basicMap.MatchNoAlpha(this.color3);
        }

        [Benchmark]
        public int Octree()
        {
            return this.octree.GetQuantizedColor(this.color1, out Rgba32 _) +
                   this.octree.GetQuantizedColor(this.color2, out _) +
                   this.octree.GetQuantizedColor(this.color3, out _);
        }

        [Benchmark]
        public int Wu()
        {
            return this.wuMap.Match(this.color1) +
                   this.wuMap.Match(this.color2) +
                   this.wuMap.Match(this.color3);
        }

        [Benchmark]
        public int LinearBoxes()
        {
            return this.linearBoxes.Match(this.color1) +
                   this.linearBoxes.Match(this.color2) +
                   this.linearBoxes.Match(this.color3);
        }

        struct LinearRgba32Map
        {
            private Rgba32[] palette;

            public static LinearRgba32Map Create()
            {
                LinearRgba32Map result = default;
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

        private static OctreeFrameQuantizer<Rgba32> CreateOctreeFrameQuantizer()
        {
            string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Png.Rgb48Bpp);
            using var img = Image.Load<Rgba32>(path);
            var frameQuantizer = new OctreeFrameQuantizer<Rgba32>(Configuration.Default, new QuantizerOptions());
            frameQuantizer.BuildPalette(img.Frames.RootFrame, img.Bounds());
            return frameQuantizer;
        }

        private static HeapKdTreePaletteLookup<Rgba32> CreateKdLookup()
        {
            return new HeapKdTreePaletteLookup<Rgba32>(Configuration.Default, CreateTestPalette());
        }

        private static Rgba32[] CreateTestPalette()
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

            return palette;
        }

        internal struct LinearVector4Map
        {
            private readonly Vector4[] paletteVectors;

            private LinearVector4Map(Configuration configuration, ReadOnlyMemory<Rgba32> palette)
            {
                this.paletteVectors = new Vector4[palette.Length];
                PixelOperations<Rgba32>.Instance.ToVector4(configuration, palette.Span, this.paletteVectors);
            }

            public static LinearVector4Map Create()
            {
                Rgba32[] palette = CreateTestPalette();

                return new LinearVector4Map(Configuration.Default, palette);
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

        struct ConcurrentHashMap
        {
            private ConcurrentDictionary<Rgba32, byte> map;

            public static ConcurrentHashMap Create()
            {
                ConcurrentHashMap result = default;

                var map = new ConcurrentDictionary<Rgba32, byte>();

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

            public byte MatchNoAlpha(Rgba32 rgba)
            {
                int r = ((rgba.R >> 3) + 4) & 31;
                int g = ((rgba.G >> 2) + 2) & 63;
                int b = ((rgba.B >> 3) + 4) & 31;

                int idx =
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

        struct LinearBoxesMap
        {
            private const int Rbits = 5;
            private const int Gbits = 5;
            private const int Bbits = 5;

            private const int RBoxes = 1 << Rbits;
            private const int GBoxes = 1 << Gbits;
            private const int BBoxes = 1 << Bbits;

            private const int TotalBoxes = RBoxes * GBoxes * BBoxes;

            private Box[] map;

            public static LinearBoxesMap Create()
            {
                LinearBoxesMap result = default;
                result.map = new Box[TotalBoxes];

                Rgba32[] palette = CreateTestPalette();

                for (int i = 0; i < palette.Length; i++)
                {
                    result.Add(palette[i], i);
                }

                return result;
            }

            public byte Match(Rgba32 c)
            {
                int r = c.R >> Rbits;
                int g = c.G >> Gbits;
                int b = c.B >> Bbits;

                int idx =
                    (b << (Gbits + Rbits)) +
                    (g << Rbits) +
                    r;
                ref Box box = ref this.map[idx];

                return (byte)(box.Match(c) >> 1);
            }

            private void Add(Rgba32 c, int paletteIndex)
            {
                int r = c.R >> Rbits;
                int g = c.G >> Gbits;
                int b = c.B >> Bbits;

                int idx =
                    (b << (Gbits + Rbits)) +
                    (g << Rbits) +
                    r;
                ref Box box = ref this.map[idx];
                byte bIdx = (byte)(paletteIndex << 1);
                box.Add(c, bIdx);
            }

            struct Box
            {
                public Rgba32 Color0;
                public short Index0;

                public Rgba32 Color1;
                public short Index1;

                public Rgba32 Color2;
                public short Index2;

                public Rgba32 Color3;
                public short Index3;

                public void Add(Rgba32 c, byte index)
                {
                    if (this.Index0 == 0)
                    {
                        this.Color0 = c;
                        this.Index0 = index;
                        return;
                    }

                    if (this.Index1 == 0)
                    {
                        this.Color1 = c;
                        this.Index1 = index;
                        return;
                    }

                    if (this.Index2 == 0)
                    {
                        this.Color2 = c;
                        this.Index2 = index;
                        return;
                    }

                    if (this.Index3 == 0)
                    {
                        this.Color3 = c;
                        this.Index3 = index;
                        return;
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public short Match(Rgba32 c)
                {
                    if (this.Index0 == 0)
                    {
                        return 0;
                    }

                    if (this.Color0.Equals(c))
                    {
                        return this.Index0;
                    }

                    if (this.Index1 == 0)
                    {
                        return 0;
                    }

                    if (this.Color1.Equals(c))
                    {
                        return this.Index1;
                    }

                    if (this.Index2 == 0)
                    {
                        return 0;
                    }

                    if (this.Color2.Equals(c))
                    {
                        return this.Index2;
                    }

                    if (this.Index3 == 0)
                    {
                        return 0;
                    }

                    if (this.Color2.Equals(c))
                    {
                        return this.Index3;
                    }

                    return 0;
                }
            }
        }
    }
}
