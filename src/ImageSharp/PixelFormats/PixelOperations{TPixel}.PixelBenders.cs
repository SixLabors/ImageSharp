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
                case PixelBlenderMode.Multiply: return DefaultPixelBlenders<TPixel>.Multiply.Instance;
                case PixelBlenderMode.Add: return DefaultPixelBlenders<TPixel>.Add.Instance;
                case PixelBlenderMode.Substract: return DefaultPixelBlenders<TPixel>.Substract.Instance;
                case PixelBlenderMode.Screen: return DefaultPixelBlenders<TPixel>.Screen.Instance;
                case PixelBlenderMode.Darken: return DefaultPixelBlenders<TPixel>.Darken.Instance;
                case PixelBlenderMode.Lighten: return DefaultPixelBlenders<TPixel>.Lighten.Instance;
                case PixelBlenderMode.Overlay: return DefaultPixelBlenders<TPixel>.Overlay.Instance;
                case PixelBlenderMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLight.Instance;
                case PixelBlenderMode.Src: return DefaultPixelBlenders<TPixel>.Src.Instance;
                case PixelBlenderMode.Atop: return DefaultPixelBlenders<TPixel>.Atop.Instance;
                case PixelBlenderMode.Over: return DefaultPixelBlenders<TPixel>.Over.Instance;
                case PixelBlenderMode.In: return DefaultPixelBlenders<TPixel>.In.Instance;
                case PixelBlenderMode.Out: return DefaultPixelBlenders<TPixel>.Out.Instance;
                case PixelBlenderMode.Dest: return DefaultPixelBlenders<TPixel>.Dest.Instance;
                case PixelBlenderMode.DestAtop: return DefaultPixelBlenders<TPixel>.DestAtop.Instance;
                case PixelBlenderMode.DestOver: return DefaultPixelBlenders<TPixel>.DestOver.Instance;
                case PixelBlenderMode.DestIn: return DefaultPixelBlenders<TPixel>.DestIn.Instance;
                case PixelBlenderMode.DestOut: return DefaultPixelBlenders<TPixel>.DestOut.Instance;
                case PixelBlenderMode.Clear: return DefaultPixelBlenders<TPixel>.Clear.Instance;
                case PixelBlenderMode.Xor: return DefaultPixelBlenders<TPixel>.Xor.Instance;

                case PixelBlenderMode.Normal:
                default:
                    return DefaultPixelBlenders<TPixel>.Normal.Instance;
            }
        }
    }
}