// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides access to pixel blenders
    /// </content>
    public partial class PixelOperations<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Find an instance of the pixel blender.
        /// </summary>
        /// <param name="mode">The blending mode to apply</param>
        /// <returns>A <see cref="PixelBlender{TPixel}"/>.</returns>
        internal virtual PixelBlender<TPixel> GetPixelBlender(PixelBlenderMode mode)
        {
            switch (mode)
            {
                case PixelBlenderMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplySrcOver.Instance;
                case PixelBlenderMode.Add: return DefaultPixelBlenders<TPixel>.AddSrcOver.Instance;
                case PixelBlenderMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractSrcOver.Instance;
                case PixelBlenderMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenSrcOver.Instance;
                case PixelBlenderMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenSrcOver.Instance;
                case PixelBlenderMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenSrcOver.Instance;
                case PixelBlenderMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlaySrcOver.Instance;
                case PixelBlenderMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightSrcOver.Instance;
                case PixelBlenderMode.Src: return DefaultPixelBlenders<TPixel>.NormalSrc.Instance;
                case PixelBlenderMode.Atop: return DefaultPixelBlenders<TPixel>.NormalSrcAtop.Instance;
                case PixelBlenderMode.Over: return DefaultPixelBlenders<TPixel>.NormalSrcOver.Instance;
                case PixelBlenderMode.In: return DefaultPixelBlenders<TPixel>.NormalSrcIn.Instance;
                case PixelBlenderMode.Out: return DefaultPixelBlenders<TPixel>.NormalSrcOut.Instance;
                case PixelBlenderMode.Dest: return DefaultPixelBlenders<TPixel>.NormalDest.Instance;
                case PixelBlenderMode.DestAtop: return DefaultPixelBlenders<TPixel>.NormalDestAtop.Instance;
                case PixelBlenderMode.DestOver: return DefaultPixelBlenders<TPixel>.NormalDestOver.Instance;
                case PixelBlenderMode.DestIn: return DefaultPixelBlenders<TPixel>.NormalDestIn.Instance;
                case PixelBlenderMode.DestOut: return DefaultPixelBlenders<TPixel>.NormalDestOut.Instance;
                case PixelBlenderMode.Clear: return DefaultPixelBlenders<TPixel>.NormalClear.Instance;
                case PixelBlenderMode.Xor: return DefaultPixelBlenders<TPixel>.NormalXor.Instance;

                case PixelBlenderMode.Normal:
                default:
                    return DefaultPixelBlenders<TPixel>.NormalSrcOver.Instance;
            }
        }
    }
}