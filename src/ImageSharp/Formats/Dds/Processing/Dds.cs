using System;
using System.IO;
using Pfim;
using Pfim.dds;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Emums;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Dds.Processing
{
    /// <summary>
    /// Class that represents direct draw surfaces
    /// </summary>
    internal abstract class Dds
    {
        public abstract int BitsPerPixel { get; }

        public int BytesPerPixel => BitsPerPixel / 8;

        public virtual int Stride => CalcStride((int)DdsHeader.Width, BitsPerPixel);

        public DdsHeader DdsHeader { get; }

        public DdsHeaderDxt10 DdsHeaderDxt10 { get; }

        public abstract ImageFormat Format { get; }

        public abstract MipMapOffset[] MipMaps { get; }

        protected abstract Image[] Decode(Stream stream);

        public Dds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
        {
            DdsHeader = ddsHeader;
            DdsHeaderDxt10 = ddsHeaderDxt10;
        }

        public static int CalcStride(int width, int pixelDepth)
        {
            if (width <= 0)
            {
                throw new ArgumentException("Width must be greater than zero", nameof(width));
            }
            else if (pixelDepth <= 0)
            {
                throw new ArgumentException("Pixel depth must be greater than zero", nameof(pixelDepth));
            }

            int bytesPerPixel = (pixelDepth + 7) / 8;

            // Windows GDI+ requires that the stride be a multiple of four.
            // Even if not being used for Windows GDI+ there isn't a anything
            // bad with having extra space.
            return 4 * (((width * bytesPerPixel) + 3) / 4);
        }

        public static Image[] DecodeDds(Stream stream, DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
        {
            Dds dds;
            switch (ddsHeader.PixelFormat.FourCC)
            {
                case DdsFourCC.DXT1:
                    dds = new Dxt1Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.DXT2:
                case DdsFourCC.DXT4:
                    throw new ArgumentException("Cannot support DXT2 or DXT4");
                case DdsFourCC.DXT3:
                    dds = new Dxt3Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.DXT5:
                    dds = new Dxt5Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.None:
                    dds = new UncompressedDds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.DX10:
                    dds = GetDx10Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.ATI1:
                case DdsFourCC.BC4U:
                    dds = new Bc4Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.BC4S:
                    dds = new Bc4sDds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.ATI2:
                case DdsFourCC.BC5U:
                    dds = new Bc5Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DdsFourCC.BC5S:
                    dds = new Bc5sDds(ddsHeader, ddsHeaderDxt10);
                    break;
                default:
                    throw new ArgumentException($"FourCC: {ddsHeader.PixelFormat.FourCC} not supported.");
            }

            return dds.Decode(stream);
        }

        private static Dds GetDx10Dds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
        {
            Dds dds;
            switch (ddsHeaderDxt10.DxgiFormat)
            {
                case DxgiFormat.BC1_Typeless:
                case DxgiFormat.BC1_UNorm_SRGB:
                case DxgiFormat.BC1_UNorm:
                    dds = new Dxt1Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC2_Typeless:
                case DxgiFormat.BC2_UNorm:
                case DxgiFormat.BC2_UNorm_SRGB:
                    dds = new Dxt3Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC3_Typeless:
                case DxgiFormat.BC3_UNorm:
                case DxgiFormat.BC3_UNorm_SRGB:
                    dds = new Dxt5Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC4_Typeless:
                case DxgiFormat.BC4_UNorm:
                    dds = new Bc4Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC4_SNorm:
                    dds = new Bc4sDds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC5_Typeless:
                case DxgiFormat.BC5_UNorm:
                    dds = new Bc5Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC5_SNorm:
                    dds = new Bc5sDds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC6H_Typeless:
                case DxgiFormat.BC6H_UF16:
                case DxgiFormat.BC6H_SF16:
                    dds = new Bc6hDds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.BC7_Typeless:
                case DxgiFormat.BC7_UNorm:
                case DxgiFormat.BC7_UNorm_SRGB:
                    dds = new Bc7Dds(ddsHeader, ddsHeaderDxt10);
                    break;
                case DxgiFormat.R8G8B8A8_Typeless:
                case DxgiFormat.R8G8B8A8_UNorm:
                case DxgiFormat.R8G8B8A8_UNorm_SRGB:
                case DxgiFormat.R8G8B8A8_UInt:
                case DxgiFormat.R8G8B8A8_SNorm:
                case DxgiFormat.R8G8B8A8_SInt:
                    dds = new UncompressedDds(ddsHeader, ddsHeaderDxt10, 32, true);
                    break;
                case DxgiFormat.B8G8R8A8_Typeless:
                case DxgiFormat.B8G8R8A8_UNorm:
                case DxgiFormat.B8G8R8A8_UNorm_SRGB:
                    dds = new UncompressedDds(ddsHeader, ddsHeaderDxt10, 32, false);
                    break;
                case DxgiFormat.Unknown:
                case DxgiFormat.R32G32B32A32_Typeless:
                case DxgiFormat.R32G32B32A32_Float:
                case DxgiFormat.R32G32B32A32_UInt:
                case DxgiFormat.R32G32B32A32_SInt:
                case DxgiFormat.R32G32B32_Typeless:
                case DxgiFormat.R32G32B32_Float:
                case DxgiFormat.R32G32B32_UInt:
                case DxgiFormat.R32G32B32_SInt:
                case DxgiFormat.R16G16B16A16_Typeless:
                case DxgiFormat.R16G16B16A16_Float:
                case DxgiFormat.R16G16B16A16_UNorm:
                case DxgiFormat.R16G16B16A16_UInt:
                case DxgiFormat.R16G16B16A16_SNorm:
                case DxgiFormat.R16G16B16A16_SInt:
                case DxgiFormat.R32G32_Typeless:
                case DxgiFormat.R32G32_Float:
                case DxgiFormat.R32G32_UInt:
                case DxgiFormat.R32G32_SInt:
                case DxgiFormat.R32G8X24_Typeless:
                case DxgiFormat.D32_Float_S8X24_UInt:
                case DxgiFormat.R32_Float_X8X24_Typeless:
                case DxgiFormat.X32_Typeless_G8X24_UInt:
                case DxgiFormat.R10G10B10A2_Typeless:
                case DxgiFormat.R10G10B10A2_UNorm:
                case DxgiFormat.R10G10B10A2_UInt:
                case DxgiFormat.R11G11B10_Float:
                case DxgiFormat.R16G16_Typeless:
                case DxgiFormat.R16G16_Float:
                case DxgiFormat.R16G16_UNorm:
                case DxgiFormat.R16G16_UInt:
                case DxgiFormat.R16G16_SNorm:
                case DxgiFormat.R16G16_SInt:
                case DxgiFormat.R32_Typeless:
                case DxgiFormat.D32_Float:
                case DxgiFormat.R32_Float:
                case DxgiFormat.R32_UInt:
                case DxgiFormat.R32_SInt:
                case DxgiFormat.R24G8_Typeless:
                case DxgiFormat.D24_UNorm_S8_UInt:
                case DxgiFormat.R24_UNorm_X8_Typeless:
                case DxgiFormat.X24_Typeless_G8_UInt:
                case DxgiFormat.R8G8_Typeless:
                case DxgiFormat.R8G8_UNorm:
                case DxgiFormat.R8G8_UInt:
                case DxgiFormat.R8G8_SNorm:
                case DxgiFormat.R8G8_SInt:
                case DxgiFormat.R16_Typeless:
                case DxgiFormat.R16_Float:
                case DxgiFormat.D16_UNorm:
                case DxgiFormat.R16_UNorm:
                case DxgiFormat.R16_UInt:
                case DxgiFormat.R16_SNorm:
                case DxgiFormat.R16_SInt:
                case DxgiFormat.R8_Typeless:
                case DxgiFormat.R8_UNorm:
                case DxgiFormat.R8_UInt:
                case DxgiFormat.R8_SNorm:
                case DxgiFormat.R8_SInt:
                case DxgiFormat.A8_UNorm:
                case DxgiFormat.R1_UNorm:
                case DxgiFormat.R9G9B9E5_SharedExp:
                case DxgiFormat.R8G8_B8G8_UNorm:
                case DxgiFormat.G8R8_G8B8_UNorm:
                case DxgiFormat.B8G8R8X8_UNorm:
                case DxgiFormat.R10G10B10_XR_BIAS_A2_UNorm:
                case DxgiFormat.B8G8R8X8_Typeless:
                case DxgiFormat.B8G8R8X8_UNorm_SRGB:
                case DxgiFormat.NV12:
                case DxgiFormat.P010:
                case DxgiFormat.P016:
                case DxgiFormat.Opaque_420:
                case DxgiFormat.YUY2:
                case DxgiFormat.Y210:
                case DxgiFormat.Y216:
                case DxgiFormat.NV11:
                case DxgiFormat.AI44:
                case DxgiFormat.IA44:
                case DxgiFormat.P8:
                case DxgiFormat.A8P8:
                case DxgiFormat.B4G4R4A4_UNorm:
                case DxgiFormat.P208:
                case DxgiFormat.V208:
                case DxgiFormat.V408:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return dds;
        }
    }
}
