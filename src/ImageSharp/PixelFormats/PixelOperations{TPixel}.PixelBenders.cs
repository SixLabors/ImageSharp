// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.PixelFormats
{
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
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyClear.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddClear.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractClear.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenClear.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenClear.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenClear.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayClear.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightClear.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalClear.Instance;
                    }

                case PixelAlphaCompositionMode.Xor:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyXor.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddXor.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractXor.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenXor.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenXor.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenXor.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayXor.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightXor.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalXor.Instance;
                    }

                case PixelAlphaCompositionMode.Src:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrc.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrc.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrc.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrc.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrc.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrc.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrc.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrc.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalSrc.Instance;
                    }

                case PixelAlphaCompositionMode.SrcAtop:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcAtop.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcAtop.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcAtop.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcAtop.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcAtop.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcAtop.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcAtop.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcAtop.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalSrcAtop.Instance;
                    }

                case PixelAlphaCompositionMode.SrcIn:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcIn.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcIn.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcIn.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcIn.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcIn.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcIn.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcIn.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcIn.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalSrcIn.Instance;
                    }

                case PixelAlphaCompositionMode.SrcOut:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcOut.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcOut.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcOut.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcOut.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcOut.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcOut.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcOut.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcOut.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalSrcOut.Instance;
                    }

                case PixelAlphaCompositionMode.Dest:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDest.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDest.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDest.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDest.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDest.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDest.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDest.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDest.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalDest.Instance;
                    }

                case PixelAlphaCompositionMode.DestAtop:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestAtop.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestAtop.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestAtop.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestAtop.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestAtop.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestAtop.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestAtop.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestAtop.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalDestAtop.Instance;
                    }

                case PixelAlphaCompositionMode.DestIn:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestIn.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestIn.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestIn.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestIn.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestIn.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestIn.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestIn.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestIn.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalDestIn.Instance;
                    }

                case PixelAlphaCompositionMode.DestOut:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestOut.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestOut.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestOut.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestOut.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestOut.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestOut.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestOut.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestOut.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalDestOut.Instance;
                    }

                case PixelAlphaCompositionMode.DestOver:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyDestOver.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddDestOver.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractDestOver.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenDestOver.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenDestOver.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenDestOver.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayDestOver.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightDestOver.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalDestOver.Instance;
                    }

                case PixelAlphaCompositionMode.SrcOver:
                default:
                    switch (colorMode)
                    {
                        case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcOver.Instance;
                        case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcOver.Instance;
                        case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcOver.Instance;
                        case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcOver.Instance;
                        case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcOver.Instance;
                        case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcOver.Instance;
                        case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcOver.Instance;
                        case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcOver.Instance;
                        case PixelColorBlendingMode.Normal:
                        default: return DefaultPixelBlenders<TPixel>.NormalSrcOver.Instance;
                    }
            }
        }
    }
}
