using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.Formats.Dds.Emums;

namespace SixLabors.ImageSharp.Formats.Dds
{
    internal class DdsTools
    {
        public static int GetBitsPerPixel(D3dFormat d3dFormat)
        {
            switch (d3dFormat)
            {
                case D3dFormat.A1:
                    return 1;
                case D3dFormat.DXT1:
                case D3dFormat.BC4S:
                case D3dFormat.BC4U:
                case D3dFormat.ATI1:
                    return 4;
                case D3dFormat.R3G3B2:
                case D3dFormat.A8:
                case D3dFormat.P8:
                case D3dFormat.L8:
                case D3dFormat.A4L4:
                case D3dFormat.S8_Lockable:
                case D3dFormat.DXT2:
                case D3dFormat.DXT3:
                case D3dFormat.DXT4:
                case D3dFormat.DXT5:
                case D3dFormat.BC5S:
                case D3dFormat.BC5U:
                case D3dFormat.ATI2:
                    return 8;
                case D3dFormat.R5G6B5:
                case D3dFormat.X1R5G5B5:
                case D3dFormat.A1R5G5B5:
                case D3dFormat.A4R4G4B4:
                case D3dFormat.A8R3G3B2:
                case D3dFormat.X4R4G4B4:
                case D3dFormat.A8P8:
                case D3dFormat.A8L8:
                case D3dFormat.V8U8:
                case D3dFormat.L6V5U5:
                case D3dFormat.UYVY:
                case D3dFormat.YUY2:
                case D3dFormat.R8G8_B8G8:
                case D3dFormat.G8R8_G8B8:
                case D3dFormat.D16:
                case D3dFormat.D16_Lockable:
                case D3dFormat.D15S1:
                case D3dFormat.L16:
                case D3dFormat.Index16:
                case D3dFormat.R16F:
                case D3dFormat.CxV8U8:
                    return 16;
                case D3dFormat.R8G8B8:
                    return 24;
                case D3dFormat.A8R8G8B8:
                case D3dFormat.X8R8G8B8:
                case D3dFormat.A2B10G10R10:
                case D3dFormat.A8B8G8R8:
                case D3dFormat.X8B8G8R8:
                case D3dFormat.G16R16:
                case D3dFormat.A2R10G10B10:
                case D3dFormat.X8L8V8U8:
                case D3dFormat.DQ8W8V8U8:
                case D3dFormat.V16U16:
                case D3dFormat.A2W10V10U10:
                case D3dFormat.D32:
                case D3dFormat.D32_Lockable:
                case D3dFormat.D32F_Lockable:
                case D3dFormat.D24S8:
                case D3dFormat.D24X8:
                case D3dFormat.D24X4S4:
                case D3dFormat.D24FS8:
                case D3dFormat.Index32:
                case D3dFormat.G16R16F:
                case D3dFormat.R32F:
                case D3dFormat.A2B10G10R10_XR_Bias:
                case D3dFormat.Multi2_ARGB8:
                    return 32;
                case D3dFormat.A16B16G16R16:
                case D3dFormat.A16B16G16R16F:
                case D3dFormat.Q16W16V16U16:
                case D3dFormat.G32R32F:
                    return 64;
                case D3dFormat.A32B32G32R32F:
                    return 128;
                case D3dFormat.Unknown:
                case D3dFormat.VertexData:
                case D3dFormat.BinaryBuffer:
                    return 0;
                default:
                    return 0;
            }
        }

        public static int GetBitsPerPixel(DxgiFormat dxgiFormat)
        {
            switch (dxgiFormat)
            {
                case DxgiFormat.R32G32B32A32_Typeless:
                case DxgiFormat.R32G32B32A32_Float:
                case DxgiFormat.R32G32B32A32_UInt:
                case DxgiFormat.R32G32B32A32_SInt:
                    return 128;
                case DxgiFormat.R32G32B32_Typeless:
                case DxgiFormat.R32G32B32_Float:
                case DxgiFormat.R32G32B32_UInt:
                case DxgiFormat.R32G32B32_SInt:
                    return 96;
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
                    return 64;
                case DxgiFormat.R10G10B10A2_Typeless:
                case DxgiFormat.R10G10B10A2_UNorm:
                case DxgiFormat.R10G10B10A2_UInt:
                case DxgiFormat.R11G11B10_Float:
                case DxgiFormat.R8G8B8A8_Typeless:
                case DxgiFormat.R8G8B8A8_UNorm:
                case DxgiFormat.R8G8B8A8_UNorm_SRGB:
                case DxgiFormat.R8G8B8A8_UInt:
                case DxgiFormat.R8G8B8A8_SNorm:
                case DxgiFormat.R8G8B8A8_SInt:
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
                case DxgiFormat.R9G9B9E5_SHAREDEXP:
                case DxgiFormat.R8G8_B8G8_UNorm:
                case DxgiFormat.G8R8_G8B8_UNorm:
                case DxgiFormat.B8G8R8A8_UNorm:
                case DxgiFormat.B8G8R8X8_UNorm:
                case DxgiFormat.R10G10B10_XR_BIAS_A2_UNorm:
                case DxgiFormat.B8G8R8A8_Typeless:
                case DxgiFormat.B8G8R8A8_UNorm_SRGB:
                case DxgiFormat.B8G8R8X8_Typeless:
                case DxgiFormat.B8G8R8X8_UNorm_SRGB:
                    return 32;
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
                case DxgiFormat.B5G6R5_UNorm:
                case DxgiFormat.B5G5R5A1_UNorm:
                case DxgiFormat.B4G4R4A4_UNorm:
                    return 16;
                case DxgiFormat.R8_Typeless:
                case DxgiFormat.R8_UNorm:
                case DxgiFormat.R8_UInt:
                case DxgiFormat.R8_SNorm:
                case DxgiFormat.R8_SInt:
                case DxgiFormat.A8_UNorm:
                    return 8;
                case DxgiFormat.R1_UNorm:
                    return 1;
                case DxgiFormat.BC1_Typeless:
                case DxgiFormat.BC1_UNorm:
                case DxgiFormat.BC1_UNorm_SRGB:
                case DxgiFormat.BC4_Typeless:
                case DxgiFormat.BC4_UNorm:
                case DxgiFormat.BC4_SNorm:
                    return 4;
                case DxgiFormat.BC2_Typeless:
                case DxgiFormat.BC2_UNorm:
                case DxgiFormat.BC2_UNorm_SRGB:
                case DxgiFormat.BC3_Typeless:
                case DxgiFormat.BC3_UNorm:
                case DxgiFormat.BC3_UNorm_SRGB:
                case DxgiFormat.BC5_Typeless:
                case DxgiFormat.BC5_UNorm:
                case DxgiFormat.BC5_SNorm:
                case DxgiFormat.BC6H_Typeless:
                case DxgiFormat.BC6H_UF16:
                case DxgiFormat.BC6H_SF16:
                case DxgiFormat.BC7_Typeless:
                case DxgiFormat.BC7_UNorm:
                case DxgiFormat.BC7_UNorm_SRGB:
                    return 8;
                default:
                    return 0;
            }
        }

        public static int GetBitsPerPixel(D3dFormat d3dFormat, DxgiFormat dxgiFormat)
        {
            if (dxgiFormat != DxgiFormat.Unknown)
            {
                return GetBitsPerPixel(dxgiFormat);
            }

            return GetBitsPerPixel(d3dFormat);
        }

        public static long ComputePitch(uint dimension, D3dFormat formatD3D, DxgiFormat formatDxgi, int defaultPitchOrLinearSize = 0)
        {
            int bitsPerPixel = GetBitsPerPixel(formatD3D, formatDxgi);
            if (IsBlockCompressedFormat(formatD3D) || IsBlockCompressedFormat(formatDxgi))
            {
                return ComputeBCPitch(dimension, bitsPerPixel * 2);
            }

            if (formatD3D == D3dFormat.R8G8_B8G8 || formatD3D == D3dFormat.G8R8_G8B8 || formatD3D == D3dFormat.UYVY || formatD3D == D3dFormat.YUY2)
            {
                return Math.Max(1, (dimension + 1) >> 1) * 4;
            }

            if (IsPackedFormat(formatDxgi))
            {
                return defaultPitchOrLinearSize;
            }

            return ComputeUncompressedPitch(dimension, bitsPerPixel);
        }

        public static bool IsBlockCompressedFormat(D3dFormat format)
        {
            switch (format)
            {
                case D3dFormat.DXT1:
                case D3dFormat.DXT2:
                case D3dFormat.DXT3:
                case D3dFormat.DXT4:
                case D3dFormat.DXT5:
                case D3dFormat.BC4U:
                case D3dFormat.BC4S:
                case D3dFormat.BC5S:
                case D3dFormat.BC5U:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsBlockCompressedFormat(DxgiFormat format)
        {
            switch (format)
            {
                case DxgiFormat.BC1_Typeless:
                case DxgiFormat.BC1_UNorm:
                case DxgiFormat.BC1_UNorm_SRGB:
                case DxgiFormat.BC2_Typeless:
                case DxgiFormat.BC2_UNorm:
                case DxgiFormat.BC2_UNorm_SRGB:
                case DxgiFormat.BC3_Typeless:
                case DxgiFormat.BC3_UNorm:
                case DxgiFormat.BC3_UNorm_SRGB:
                case DxgiFormat.BC4_Typeless:
                case DxgiFormat.BC4_UNorm:
                case DxgiFormat.BC4_SNorm:
                case DxgiFormat.BC5_Typeless:
                case DxgiFormat.BC5_UNorm:
                case DxgiFormat.BC5_SNorm:
                case DxgiFormat.BC6H_Typeless:
                case DxgiFormat.BC6H_SF16:
                case DxgiFormat.BC6H_UF16:
                case DxgiFormat.BC7_Typeless:
                case DxgiFormat.BC7_UNorm:
                case DxgiFormat.BC7_UNorm_SRGB:
                    return true;
                default:
                    return false;
            }
        }

        internal static int ComputeBCPitch(uint dimension, int bytesPerBlock)
        {
            return (int)Math.Max(1, (dimension + 3) / 4) * bytesPerBlock;
        }

        public static bool IsPackedFormat(DxgiFormat format)
        {
            switch (format)
            {
                case DxgiFormat.YUY2:
                case DxgiFormat.Y210:
                case DxgiFormat.Y216:
                case DxgiFormat.Y410:
                case DxgiFormat.Y416:
                case DxgiFormat.Opaque_420:
                case DxgiFormat.AI44:
                case DxgiFormat.AYUV:
                case DxgiFormat.IA44:
                case DxgiFormat.NV11:
                case DxgiFormat.NV12:
                case DxgiFormat.P010:
                case DxgiFormat.P016:
                case DxgiFormat.R8G8_B8G8_UNorm:
                case DxgiFormat.G8R8_G8B8_UNorm:
                    return true;
                default:
                    return false;
            }
        }

        internal static int ComputeUncompressedPitch(uint dimension, int bitsPerPixel)
        {
            return (int)Math.Max(1, ((dimension * bitsPerPixel) + 7) / 8);
        }

        public static long ComputeLinearSize(uint width, uint height, D3dFormat formatD3D, DxgiFormat formatDxgi, int defaultPitchOrLinearSize = 0)
        {
            int bitsPerPixel = GetBitsPerPixel(formatD3D, formatDxgi);
            if (IsBlockCompressedFormat(formatD3D) || IsBlockCompressedFormat(formatDxgi))
            {
                return ComputeBCLinearSize(width, height, bitsPerPixel * 2);
            }

            // These packed formats get a special handling...
            if (formatD3D == D3dFormat.R8G8_B8G8 || formatD3D == D3dFormat.G8R8_G8B8 ||
                formatD3D == D3dFormat.UYVY || formatD3D == D3dFormat.YUY2)
            {
                return ((width + 1) >> 1) * 4 * height;
            }

            if (IsPackedFormat(formatDxgi))
            {
                return defaultPitchOrLinearSize * height;
            }

            return ComputeUncompressedPitch(width, bitsPerPixel) * height;
        }

        internal static long ComputeBCLinearSize(uint width, uint height, int bytesPerBlock)
        {
            return ComputeBCPitch(width, bytesPerBlock) * Math.Max(1, (height + 3) / 4);
        }
    }
}
