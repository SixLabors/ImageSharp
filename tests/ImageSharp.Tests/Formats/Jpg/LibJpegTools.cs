namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;

    using BitMiracle.LibJpeg.Classic;

    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder;
    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;
    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    using Xunit;

    internal static class LibJpegTools
    {
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

            internal SpectralData(ComponentData[] components)
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

            public static SpectralData LoadFromImageSharpDecoder(OldJpegDecoderCore decoder)
            {
                OldComponent[] srcComponents = decoder.Components;
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

                Image<Rgba32> result = new Image<Rgba32>(c0.WidthInBlocks * 8, c0.HeightInBlocks * 8);

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

                Block8x8 block0 = c0.Blocks[bx, by];
                Block8x8 block1 = c1.Blocks[bx, by];
                Block8x8 block2 = c2.Blocks[bx, by];

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

        public class ComponentData : IEquatable<ComponentData>, IJpegComponent
        {
            public ComponentData(int heightInBlocks, int widthInBlocks, int index)
            {
                this.HeightInBlocks = heightInBlocks;
                this.WidthInBlocks = widthInBlocks;
                this.Index = index;
                this.Blocks = new Buffer2D<Block8x8>(this.WidthInBlocks, this.HeightInBlocks);
            }

            public Size Size => new Size(this.WidthInBlocks, this.HeightInBlocks);

            public int Index { get; }

            public int HeightInBlocks { get; }

            public int WidthInBlocks { get; }

            public Buffer2D<Block8x8> Blocks { get; private set; }

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

            internal void MakeBlock(short[] data, int y, int x)
            {
                this.MinVal = Math.Min(this.MinVal, data.Min());
                this.MaxVal = Math.Max(this.MaxVal, data.Max());
                this.Blocks[x, y] = new Block8x8(data);
            }

            public static ComponentData Load(FrameComponent c, int index)
            {
                var result = new ComponentData(
                    c.BlocksPerColumnForMcu,
                    c.BlocksPerLineForMcu,
                    index
                    );

                for (int y = 0; y < result.HeightInBlocks; y++)
                {
                    for (int x = 0; x < result.WidthInBlocks; x++)
                    {
                        short[] data = c.GetBlockBuffer(y, x).ToArray();
                        result.MakeBlock(data, y, x);
                    }
                }

                return result;
            }

            public static ComponentData Load(OldComponent c)
            {
                var result = new ComponentData(
                    c.HeightInBlocks,
                    c.WidthInBlocks,
                    c.Index
                );

                for (int y = 0; y < result.HeightInBlocks; y++)
                {
                    for (int x = 0; x < result.WidthInBlocks; x++)
                    {
                        short[] data = c.GetBlockReference(x, y).ToArray();
                        result.MakeBlock(data, y, x);
                    }
                }

                return result;
            }

            public Image<Rgba32> CreateGrayScaleImage()
            {
                Image<Rgba32> result = new Image<Rgba32>(this.WidthInBlocks * 8, this.HeightInBlocks * 8);
                
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
                Block8x8 block = this.Blocks[bx, by];
                
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

            internal float GetBlockValue(Block8x8 block, int x, int y)
            {
                float d = (this.MaxVal - this.MinVal);
                float val = block.GetValueAt(x, y);
                val -= this.MinVal;
                val /= d;
                return val;
            }

            public bool Equals(ComponentData other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                bool ok = this.Index == other.Index && this.HeightInBlocks == other.HeightInBlocks
                          && this.WidthInBlocks == other.WidthInBlocks;
                       //&& this.MinVal == other.MinVal
                       //&& this.MaxVal == other.MaxVal;
                if (!ok) return false;

                for (int y = 0; y < this.HeightInBlocks; y++)
                {
                    for (int x = 0; x < this.WidthInBlocks; x++)
                    {
                        Block8x8 a = this.Blocks[x, y];
                        Block8x8 b = other.Blocks[x, y];
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
                    hashCode = (hashCode * 397) ^ this.HeightInBlocks;
                    hashCode = (hashCode * 397) ^ this.WidthInBlocks;
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

        public static double CalculateAverageDifference(ComponentData a, ComponentData b)
        {
            BigInteger totalDiff = 0;
            if (a.Size != b.Size)
            {
                throw new Exception("a.Size != b.Size");
            }

            int count = a.Blocks.Length;

            for (int i = 0; i < count; i++)
            {
                Block8x8 aa = a.Blocks[i];
                Block8x8 bb = b.Blocks[i];

                long diff = Block8x8.TotalDifference(ref aa, ref bb);
                totalDiff += diff;
            }

            double result = (double)totalDiff;
            return result / (count * Block8x8.Size);
        }

        private static string DumpToolFullPath => Path.Combine(
            TestEnvironment.ToolsDirectoryFullPath,
            @"jpeg\dump-jpeg-coeffs.exe");

        public static void RunDumpJpegCoeffsTool(string sourceFile, string destFile)
        {
            string args = $@"""{sourceFile}"" ""{destFile}""";
            var process = Process.Start(DumpToolFullPath, args);
            process.WaitForExit();
        }

        public static SpectralData ExtractSpectralData(string inputFile)
        {
            TestFile testFile = TestFile.Create(inputFile);

            string outDir = TestEnvironment.CreateOutputDirectory(".Temp", $"JpegCoeffs");
            string fn = $"{Path.GetFileName(inputFile)}-{new Random().Next(1000)}.dctcoeffs";
            string coeffFileFullPath = Path.Combine(outDir, fn);

            try
            {
                RunDumpJpegCoeffsTool(testFile.FullPath, coeffFileFullPath);
                
                using (var dumpStream = new FileStream(coeffFileFullPath, FileMode.Open))
                using (var rdr = new BinaryReader(dumpStream))
                {
                    int componentCount = rdr.ReadInt16();
                    ComponentData[] result = new ComponentData[componentCount];

                    for (int i = 0; i < componentCount; i++)
                    {
                        int widthInBlocks = rdr.ReadInt16();
                        int heightInBlocks = rdr.ReadInt16();
                        ComponentData resultComponent = new ComponentData(heightInBlocks, widthInBlocks, i);
                        result[i] = resultComponent;
                    }

                    byte[] buffer = new byte[64*sizeof(short)];

                    for (int i = 0; i < result.Length; i++)
                    {
                        ComponentData c = result[i];

                        for (int y = 0; y < c.HeightInBlocks; y++)
                        {
                            for (int x = 0; x < c.WidthInBlocks; x++)
                            {
                                rdr.Read(buffer, 0, buffer.Length);

                                short[] block = buffer.AsSpan().NonPortableCast<byte, short>().ToArray();
                                c.MakeBlock(block, y, x);
                            }
                        }
                    }

                    return new SpectralData(result);
                }
            }
            finally
            {
                if (File.Exists(coeffFileFullPath))
                {
                    File.Delete(coeffFileFullPath);
                }
            }
        }
    }
}