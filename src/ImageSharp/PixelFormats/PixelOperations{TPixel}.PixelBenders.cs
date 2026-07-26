// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides access to pixel blenders
/// </content>
public partial class PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Find an instance of the pixel blender.
    /// </summary>
    /// <param name="options">the blending and composition to apply</param>
    /// <returns>A <see cref="PixelBlender{TPixel}"/>.</returns>
    public PixelBlender<TPixel> GetPixelBlender(GraphicsOptions options)
    {
        return this.GetPixelBlender(options.ColorBlendingMode, options.AlphaCompositionMode);
    }

    /// <summary>
    /// Find an instance of the pixel blender.
    /// </summary>
    /// <param name="colorMode">The color blending mode to apply</param>
    /// <param name="alphaMode">The alpha composition mode to apply</param>
    /// <returns>A <see cref="PixelBlender{TPixel}"/>.</returns>
    public virtual PixelBlender<TPixel> GetPixelBlender(PixelColorBlendingMode colorMode, PixelAlphaCompositionMode alphaMode)
    {
        switch (alphaMode)
        {
            case PixelAlphaCompositionMode.Clear:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyClear;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddClear;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractClear;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenClear;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenClear;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenClear;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayClear;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightClear;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalClear;
                }

            case PixelAlphaCompositionMode.Xor:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyXor;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddXor;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractXor;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenXor;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenXor;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenXor;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayXor;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightXor;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalXor;
                }

            case PixelAlphaCompositionMode.Src:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrc;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrc;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrc;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrc;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrc;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrc;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrc;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrc;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalSrc;
                }

            case PixelAlphaCompositionMode.SrcAtop:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcAtop;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcAtop;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcAtop;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcAtop;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcAtop;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcAtop;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcAtop;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcAtop;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalSrcAtop;
                }

            case PixelAlphaCompositionMode.SrcIn:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcIn;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcIn;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcIn;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcIn;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcIn;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcIn;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcIn;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcIn;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalSrcIn;
                }

            case PixelAlphaCompositionMode.SrcOut:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcOut;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcOut;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcOut;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcOut;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcOut;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcOut;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcOut;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcOut;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalSrcOut;
                }

            case PixelAlphaCompositionMode.Dest:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDest;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDest;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDest;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDest;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDest;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDest;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDest;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDest;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalDest;
                }

            case PixelAlphaCompositionMode.DestAtop:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestAtop;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestAtop;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestAtop;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestAtop;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestAtop;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestAtop;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestAtop;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestAtop;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalDestAtop;
                }

            case PixelAlphaCompositionMode.DestIn:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestIn;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestIn;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestIn;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestIn;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestIn;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestIn;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestIn;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestIn;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalDestIn;
                }

            case PixelAlphaCompositionMode.DestOut:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestOut;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestOut;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestOut;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestOut;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestOut;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestOut;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestOut;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestOut;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalDestOut;
                }

            case PixelAlphaCompositionMode.DestOver:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestOver;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestOver;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestOver;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestOver;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestOver;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestOver;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestOver;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestOver;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalDestOver;
                }

            case PixelAlphaCompositionMode.SrcOver:
            default:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcOver;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcOver;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcOver;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcOver;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcOver;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcOver;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcOver;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcOver;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalSrcOver;
                }
        }
    }
}
