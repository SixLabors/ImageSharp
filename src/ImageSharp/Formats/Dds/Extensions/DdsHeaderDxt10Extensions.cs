// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Dds.Emums;

namespace SixLabors.ImageSharp.Formats.Dds.Extensions
{
    internal static class DdsHeaderDxt10Extensions
    {
        public static int GetBitsPerPixel(this DdsHeaderDxt10 ddsHeaderDxt10)
        {
            switch (ddsHeaderDxt10.DxgiFormat)
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
    }
}
