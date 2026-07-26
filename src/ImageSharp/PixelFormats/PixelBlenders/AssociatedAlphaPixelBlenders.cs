// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders;

/// <summary>
/// Provides pixel blenders for formats that store associated alpha.
/// </summary>
internal static partial class AssociatedAlphaPixelBlenders<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the blender for the requested color blending and alpha composition modes.
    /// </summary>
    /// <param name="colorMode">The color blending mode.</param>
    /// <param name="alphaMode">The alpha composition mode.</param>
    /// <returns>The pixel blender.</returns>
    public static PixelBlender<TPixel> GetPixelBlender(PixelColorBlendingMode colorMode, PixelAlphaCompositionMode alphaMode)
    {
        return alphaMode switch
        {
            PixelAlphaCompositionMode.Src => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplySrc,
                PixelColorBlendingMode.Add => AddSrc,
                PixelColorBlendingMode.Subtract => SubtractSrc,
                PixelColorBlendingMode.Screen => ScreenSrc,
                PixelColorBlendingMode.Darken => DarkenSrc,
                PixelColorBlendingMode.Lighten => LightenSrc,
                PixelColorBlendingMode.Overlay => OverlaySrc,
                PixelColorBlendingMode.HardLight => HardLightSrc,
                _ => NormalSrc,
            },
            PixelAlphaCompositionMode.SrcAtop => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplySrcAtop,
                PixelColorBlendingMode.Add => AddSrcAtop,
                PixelColorBlendingMode.Subtract => SubtractSrcAtop,
                PixelColorBlendingMode.Screen => ScreenSrcAtop,
                PixelColorBlendingMode.Darken => DarkenSrcAtop,
                PixelColorBlendingMode.Lighten => LightenSrcAtop,
                PixelColorBlendingMode.Overlay => OverlaySrcAtop,
                PixelColorBlendingMode.HardLight => HardLightSrcAtop,
                _ => NormalSrcAtop,
            },
            PixelAlphaCompositionMode.SrcIn => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplySrcIn,
                PixelColorBlendingMode.Add => AddSrcIn,
                PixelColorBlendingMode.Subtract => SubtractSrcIn,
                PixelColorBlendingMode.Screen => ScreenSrcIn,
                PixelColorBlendingMode.Darken => DarkenSrcIn,
                PixelColorBlendingMode.Lighten => LightenSrcIn,
                PixelColorBlendingMode.Overlay => OverlaySrcIn,
                PixelColorBlendingMode.HardLight => HardLightSrcIn,
                _ => NormalSrcIn,
            },
            PixelAlphaCompositionMode.SrcOut => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplySrcOut,
                PixelColorBlendingMode.Add => AddSrcOut,
                PixelColorBlendingMode.Subtract => SubtractSrcOut,
                PixelColorBlendingMode.Screen => ScreenSrcOut,
                PixelColorBlendingMode.Darken => DarkenSrcOut,
                PixelColorBlendingMode.Lighten => LightenSrcOut,
                PixelColorBlendingMode.Overlay => OverlaySrcOut,
                PixelColorBlendingMode.HardLight => HardLightSrcOut,
                _ => NormalSrcOut,
            },
            PixelAlphaCompositionMode.Dest => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplyDest,
                PixelColorBlendingMode.Add => AddDest,
                PixelColorBlendingMode.Subtract => SubtractDest,
                PixelColorBlendingMode.Screen => ScreenDest,
                PixelColorBlendingMode.Darken => DarkenDest,
                PixelColorBlendingMode.Lighten => LightenDest,
                PixelColorBlendingMode.Overlay => OverlayDest,
                PixelColorBlendingMode.HardLight => HardLightDest,
                _ => NormalDest,
            },
            PixelAlphaCompositionMode.DestAtop => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplyDestAtop,
                PixelColorBlendingMode.Add => AddDestAtop,
                PixelColorBlendingMode.Subtract => SubtractDestAtop,
                PixelColorBlendingMode.Screen => ScreenDestAtop,
                PixelColorBlendingMode.Darken => DarkenDestAtop,
                PixelColorBlendingMode.Lighten => LightenDestAtop,
                PixelColorBlendingMode.Overlay => OverlayDestAtop,
                PixelColorBlendingMode.HardLight => HardLightDestAtop,
                _ => NormalDestAtop,
            },
            PixelAlphaCompositionMode.DestOver => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplyDestOver,
                PixelColorBlendingMode.Add => AddDestOver,
                PixelColorBlendingMode.Subtract => SubtractDestOver,
                PixelColorBlendingMode.Screen => ScreenDestOver,
                PixelColorBlendingMode.Darken => DarkenDestOver,
                PixelColorBlendingMode.Lighten => LightenDestOver,
                PixelColorBlendingMode.Overlay => OverlayDestOver,
                PixelColorBlendingMode.HardLight => HardLightDestOver,
                _ => NormalDestOver,
            },
            PixelAlphaCompositionMode.DestIn => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplyDestIn,
                PixelColorBlendingMode.Add => AddDestIn,
                PixelColorBlendingMode.Subtract => SubtractDestIn,
                PixelColorBlendingMode.Screen => ScreenDestIn,
                PixelColorBlendingMode.Darken => DarkenDestIn,
                PixelColorBlendingMode.Lighten => LightenDestIn,
                PixelColorBlendingMode.Overlay => OverlayDestIn,
                PixelColorBlendingMode.HardLight => HardLightDestIn,
                _ => NormalDestIn,
            },
            PixelAlphaCompositionMode.DestOut => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplyDestOut,
                PixelColorBlendingMode.Add => AddDestOut,
                PixelColorBlendingMode.Subtract => SubtractDestOut,
                PixelColorBlendingMode.Screen => ScreenDestOut,
                PixelColorBlendingMode.Darken => DarkenDestOut,
                PixelColorBlendingMode.Lighten => LightenDestOut,
                PixelColorBlendingMode.Overlay => OverlayDestOut,
                PixelColorBlendingMode.HardLight => HardLightDestOut,
                _ => NormalDestOut,
            },
            PixelAlphaCompositionMode.Clear => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplyClear,
                PixelColorBlendingMode.Add => AddClear,
                PixelColorBlendingMode.Subtract => SubtractClear,
                PixelColorBlendingMode.Screen => ScreenClear,
                PixelColorBlendingMode.Darken => DarkenClear,
                PixelColorBlendingMode.Lighten => LightenClear,
                PixelColorBlendingMode.Overlay => OverlayClear,
                PixelColorBlendingMode.HardLight => HardLightClear,
                _ => NormalClear,
            },
            PixelAlphaCompositionMode.Xor => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplyXor,
                PixelColorBlendingMode.Add => AddXor,
                PixelColorBlendingMode.Subtract => SubtractXor,
                PixelColorBlendingMode.Screen => ScreenXor,
                PixelColorBlendingMode.Darken => DarkenXor,
                PixelColorBlendingMode.Lighten => LightenXor,
                PixelColorBlendingMode.Overlay => OverlayXor,
                PixelColorBlendingMode.HardLight => HardLightXor,
                _ => NormalXor,
            },
            _ => colorMode switch
            {
                PixelColorBlendingMode.Multiply => MultiplySrcOver,
                PixelColorBlendingMode.Add => AddSrcOver,
                PixelColorBlendingMode.Subtract => SubtractSrcOver,
                PixelColorBlendingMode.Screen => ScreenSrcOver,
                PixelColorBlendingMode.Darken => DarkenSrcOver,
                PixelColorBlendingMode.Lighten => LightenSrcOver,
                PixelColorBlendingMode.Overlay => OverlaySrcOver,
                PixelColorBlendingMode.HardLight => HardLightSrcOver,
                _ => NormalSrcOver,
            },
        };
    }
}
