namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;

    using BitMiracle.LibJpeg.Classic;

    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    using Xunit;

    internal static class LibJpegTools
    {
        public unsafe struct Block : IEquatable<Block>
        {
            public Block(short[] data)
            {
                this.Data = data;
            }

            public short[] Data { get; }
            
            public short this[int x, int y]
            {
                get => this.Data[y * 8 + x];
                set => this.Data[y * 8 + x] = value;
            }

            public bool Equals(Block other)
            {
                for (int i = 0; i < 64; i++)
                {
                    if (this.Data[i] != other.Data[i]) return false;
                }
                return true;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is Block && Equals((Block)obj);
            }

            public override int GetHashCode()
            {
                return (this.Data != null ? this.Data.GetHashCode() : 0);
            }

            public static bool operator ==(Block left, Block right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Block left, Block right)
            {
                return !left.Equals(right);
            }
        }

        public class SpectralData : IEquatable<SpectralData>
        {
            public int ComponentCount { get; private set; }

            public ComponentData[] Components { get; private set; }

            private SpectralData(Array wholeImage)
            {
                this.ComponentCount = 0;

                for (int i = 0; i < wholeImage.Length && wholeImage.GetValue(i) != null; i++)
                {
                    this.ComponentCount++;
                }

                this.Components = new ComponentData[this.ComponentCount];

                for (int i = 0; i < this.ComponentCount; i++)
                {
                    object jVirtArray = wholeImage.GetValue(i);
                    Array bloxSource = (Array)GetNonPublicMember(jVirtArray, "m_buffer");

                    this.Components[i] = ComponentData.Load(bloxSource, i);
                }
            }

            private SpectralData(ComponentData[] components)
            {
                this.ComponentCount = components.Length;
                this.Components = components;
            }

            public static SpectralData Load(jpeg_decompress_struct cinfo)
            {
                //short[][][] result = new short[cinfo.Image_height][][];
                //int blockPerMcu = (int)GetNonPublicMember(cinfo, "m_blocks_in_MCU");
                //int mcuPerRow = (int)GetNonPublicMember(cinfo, "m_MCUs_per_row");
                //int mcuRows = (int)GetNonPublicMember(cinfo, "m_MCU_rows_in_scan");

                object coefController = GetNonPublicMember(cinfo, "m_coef");
                Array wholeImage = (Array)GetNonPublicMember(coefController, "m_whole_image");

                var result = new SpectralData(wholeImage);

                return result;
            }

            public static SpectralData Load(Stream fileStream)
            {
                jpeg_error_mgr err = new jpeg_error_mgr();
                jpeg_decompress_struct cinfo = new jpeg_decompress_struct(err);

                cinfo.jpeg_stdio_src(fileStream);
                cinfo.jpeg_read_header(true);
                cinfo.Buffered_image = true;
                cinfo.Do_block_smoothing = false;

                cinfo.jpeg_start_decompress();

                var output = CreateOutputArray(cinfo);
                for (int scan = 0; scan < cinfo.Input_scan_number; scan++)
                {
                    cinfo.jpeg_start_output(scan);
                    for (int i = 0; i < cinfo.Image_height; i++)
                    {
                        int numScanlines = cinfo.jpeg_read_scanlines(output, 1);
                        if (numScanlines != 1) throw new Exception("?");
                    }
                }

                var result = SpectralData.Load(cinfo);
                return result;
            }
            
            private static byte[][] CreateOutputArray(jpeg_decompress_struct cinfo)
            {
                byte[][] output = new byte[cinfo.Image_height][];
                for (int i = 0; i < cinfo.Image_height; i++)
                {
                    output[i] = new byte[cinfo.Image_width * cinfo.Num_components];
                }
                return output;
            }

            public static SpectralData LoadFromImageSharpDecoder(JpegDecoderCore decoder)
            {
                FrameComponent[] srcComponents = decoder.Frame.Components;

                ComponentData[] destComponents = srcComponents.Select(ComponentData.Load).ToArray();

                return new SpectralData(destComponents);
            }

            public Image<Rgba32> TryCreateRGBSpectralImage()
            {
                if (this.ComponentCount != 3) return null;

                ComponentData c0 = this.Components[0];
                ComponentData c1 = this.Components[1];
                ComponentData c2 = this.Components[2];

                if (c0.Size != c1.Size || c1.Size != c2.Size)
                {
                    return null;
                }

                Image<Rgba32> result = new Image<Rgba32>(c0.XCount * 8, c0.YCount * 8);

                for (int by = 0; by < c0.YCount; by++)
                {
                    for (int bx = 0; bx < c0.XCount; bx++)
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

                Block block0 = c0.Blocks[by, bx];
                Block block1 = c1.Blocks[by, bx];
                Block block2 = c2.Blocks[by, bx];

                float d0 = (c0.MaxVal - c0.MinVal);
                float d1 = (c1.MaxVal - c1.MinVal);
                float d2 = (c2.MaxVal - c2.MinVal);

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        float val0 = c0.GetBlockValue(block0, x, y);
                        float val1 = c0.GetBlockValue(block1, x, y);
                        float val2 = c0.GetBlockValue(block2, x, y);

                        Vector4 v = new Vector4(val0, val1, val2, 1);
                        Rgba32 color = default(Rgba32);
                        color.PackFromVector4(v);

                        int yy = by * 8 + y;
                        int xx = bx * 8 + x;
                        image[xx, yy] = color;
                    }
                }
            }

            public bool Equals(SpectralData other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                if (this.ComponentCount != other.ComponentCount)
                {
                    return false;
                }

                for (int i = 0; i < this.ComponentCount; i++)
                {
                    ComponentData a = this.Components[i];
                    ComponentData b = other.Components[i];
                    if (!a.Equals(b)) return false;
                }
                return true;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((SpectralData)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (this.ComponentCount * 397) ^ (this.Components != null ? this.Components[0].GetHashCode() : 0);
                }
            }

            public static bool operator ==(SpectralData left, SpectralData right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(SpectralData left, SpectralData right)
            {
                return !Equals(left, right);
            }
        }

        public class ComponentData : IEquatable<ComponentData>
        {
            public ComponentData(int yCount, int xCount, int index)
            {
                this.YCount = yCount;
                this.XCount = xCount;
                this.Index = index;
                this.Blocks = new Block[this.YCount, this.XCount];
            }

            public Size Size => new Size(this.XCount, this.YCount);

            public int Index { get; }

            public int YCount { get; }

            public int XCount { get; }

            public Block[,] Blocks { get; private set; }

            public short MinVal { get; private set; } = short.MaxValue;

            public short MaxVal { get; private set; } = short.MinValue;

            public static ComponentData Load(Array bloxSource, int index)
            {
                int yCount = bloxSource.Length;
                Array row0 = (Array)bloxSource.GetValue(0);
                int xCount = row0.Length;
                ComponentData result = new ComponentData(yCount, xCount, index);
                result.Init(bloxSource);
                return result;
            }

            private void Init(Array bloxSource)
            {
                for (int y = 0; y < bloxSource.Length; y++)
                {
                    Array row = (Array)bloxSource.GetValue(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        object jBlock = row.GetValue(x);
                        short[] data = (short[])GetNonPublicMember(jBlock, "data");
                        this.MakeBlock(data, y, x);
                    }
                }
            }

            private void MakeBlock(short[] data, int y, int x)
            {
                this.MinVal = Math.Min(this.MinVal, data.Min());
                this.MaxVal = Math.Max(this.MaxVal, data.Max());
                this.Blocks[y, x] = new Block(data);
            }

            public static ComponentData Load(FrameComponent sc, int index)
            {
                var result = new ComponentData(
                    sc.BlocksPerColumnForMcu,
                    sc.BlocksPerLineForMcu,
                    index
                    );
                result.Init(sc);
                return result;
            }

            private void Init(FrameComponent sc)
            {
                for (int y = 0; y < this.YCount; y++)
                {
                    for (int x = 0; x < this.XCount; x++)
                    {
                        short[] data = sc.GetBlockBuffer(y, x).ToArray();
                        this.MakeBlock(data, y, x);
                    }
                }
            }

            public Image<Rgba32> CreateGrayScaleImage()
            {
                Image<Rgba32> result = new Image<Rgba32>(this.XCount * 8, this.YCount * 8);
                
                for (int by = 0; by < this.YCount; by++)
                {
                    for (int bx = 0; bx < this.XCount; bx++)
                    {
                        this.WriteToImage(bx, by, result);
                    }
                }
                return result;
            }

            internal void WriteToImage(int bx, int by, Image<Rgba32> image)
            {
                Block block = this.Blocks[by, bx];
                
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        var val = this.GetBlockValue(block, x, y);

                        Vector4 v = new Vector4(val, val, val, 1);
                        Rgba32 color = default(Rgba32);
                        color.PackFromVector4(v);

                        int yy = by * 8 + y;
                        int xx = bx * 8 + x;
                        image[xx, yy] = color;
                    }
                }
            }

            internal float GetBlockValue(Block block, int x, int y)
            {
                float d = (this.MaxVal - this.MinVal);
                float val = block[x, y];
                val -= this.MinVal;
                val /= d;
                return val;
            }

            public bool Equals(ComponentData other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                bool ok = this.Index == other.Index && this.YCount == other.YCount && this.XCount == other.XCount
                       && this.MinVal == other.MinVal
                       && this.MaxVal == other.MaxVal;
                if (!ok) return false;

                for (int i = 0; i < this.YCount; i++)
                {
                    for (int j = 0; j < this.XCount; j++)
                    {
                        Block a = this.Blocks[i, j];
                        Block b = other.Blocks[i, j];
                        if (!a.Equals(b)) return false;
                    }
                }
                return true;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ComponentData)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = this.Index;
                    hashCode = (hashCode * 397) ^ this.YCount;
                    hashCode = (hashCode * 397) ^ this.XCount;
                    hashCode = (hashCode * 397) ^ this.MinVal.GetHashCode();
                    hashCode = (hashCode * 397) ^ this.MaxVal.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(ComponentData left, ComponentData right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(ComponentData left, ComponentData right)
            {
                return !Equals(left, right);
            }
        }

        internal static FieldInfo GetNonPublicField(object obj, string fieldName)
        {
            Type type = obj.GetType();
            return type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        internal static object GetNonPublicMember(object obj, string fieldName)
        {
            FieldInfo fi = GetNonPublicField(obj, fieldName);
            return fi.GetValue(obj);
        }
    }
}