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

    using Xunit;

    internal static class LibJpegTools
    {
        public unsafe struct Block
        {
            public Block(short[] data)
            {
                this.Data = data;
            }

            public short[] Data { get; }

            //public fixed short Data[64];

            //public Block8x8(short[] data)
            //{
            //    fixed (short* p = Data)
            //    {
            //        for (int i = 0; i < 64; i++)
            //        {
            //            p[i] = data[i];
            //        }
            //    }
            //}

            public short this[int x, int y]
            {
                get => this.Data[y * 8 + x];
                set => this.Data[y * 8 + x] = value;
            }
        }

        public class SpectralData
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
                
                ComponentData[] destComponents = new ComponentData[srcComponents.Length];
                throw new NotImplementedException();
            }
        }

        public class ComponentData
        {
            public ComponentData(int yCount, int xCount, int index)
            {
                this.YCount = yCount;
                this.XCount = xCount;
                this.Index = index;
                this.Blocks = new Block[this.YCount, this.XCount];
            }

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
                for (int i = 0; i < bloxSource.Length; i++)
                {
                    Array row = (Array)bloxSource.GetValue(i);
                    for (int j = 0; j < row.Length; j++)
                    {
                        object jBlock = row.GetValue(j);
                        short[] data = (short[])GetNonPublicMember(jBlock, "data");
                        this.MinVal = Math.Min(this.MinVal, data.Min());
                        this.MaxVal = Math.Max(this.MaxVal, data.Max());
                        this.Blocks[i, j] = new Block(data);
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
                        WriteToImage(this.Blocks[by, bx], bx, by, result);
                    }
                }
                return result;
            }

            private void WriteToImage(Block block, int bx, int by, Image<Rgba32> image)
            {
                float d = (this.MaxVal - this.MinVal);
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        int yy = by * 8 + y;
                        int xx = bx * 8 + x;
                        float val = block[x, y];
                        val -= this.MinVal;
                        val /= d;

                        Vector4 v = new Vector4(val, val, val, 1);
                        Rgba32 color = default(Rgba32);
                        color.PackFromVector4(v);

                        image[xx, yy] = color;
                    }
                }
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