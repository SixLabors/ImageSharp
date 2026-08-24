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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeClear;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnClear;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightClear;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceClear;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionClear;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueClear;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationClear;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorClear;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityClear;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeXor;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnXor;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightXor;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceXor;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionXor;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueXor;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationXor;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorXor;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityXor;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeSrc;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnSrc;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightSrc;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceSrc;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionSrc;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueSrc;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationSrc;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorSrc;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminositySrc;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeSrcAtop;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnSrcAtop;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightSrcAtop;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceSrcAtop;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionSrcAtop;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueSrcAtop;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationSrcAtop;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorSrcAtop;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminositySrcAtop;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeSrcIn;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnSrcIn;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightSrcIn;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceSrcIn;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionSrcIn;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueSrcIn;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationSrcIn;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorSrcIn;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminositySrcIn;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeSrcOut;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnSrcOut;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightSrcOut;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceSrcOut;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionSrcOut;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueSrcOut;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationSrcOut;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorSrcOut;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminositySrcOut;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeDest;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnDest;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightDest;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceDest;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionDest;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueDest;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationDest;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorDest;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityDest;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeDestAtop;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnDestAtop;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightDestAtop;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceDestAtop;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionDestAtop;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueDestAtop;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationDestAtop;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorDestAtop;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityDestAtop;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeDestIn;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnDestIn;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightDestIn;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceDestIn;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionDestIn;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueDestIn;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationDestIn;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorDestIn;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityDestIn;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeDestOut;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnDestOut;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightDestOut;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceDestOut;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionDestOut;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueDestOut;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationDestOut;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorDestOut;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityDestOut;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeDestOver;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnDestOver;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightDestOver;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceDestOver;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionDestOver;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueDestOver;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationDestOver;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorDestOver;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityDestOver;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalDestOver;
                }

            case PixelAlphaCompositionMode.Plus:
                switch (colorMode)
                {
                    case PixelColorBlendingMode.Multiply: return DefaultPixelBlenders<TPixel>.MultiplyPlus;
                    case PixelColorBlendingMode.Add: return DefaultPixelBlenders<TPixel>.AddPlus;
                    case PixelColorBlendingMode.Subtract: return DefaultPixelBlenders<TPixel>.SubtractPlus;
                    case PixelColorBlendingMode.Screen: return DefaultPixelBlenders<TPixel>.ScreenPlus;
                    case PixelColorBlendingMode.Darken: return DefaultPixelBlenders<TPixel>.DarkenPlus;
                    case PixelColorBlendingMode.Lighten: return DefaultPixelBlenders<TPixel>.LightenPlus;
                    case PixelColorBlendingMode.Overlay: return DefaultPixelBlenders<TPixel>.OverlayPlus;
                    case PixelColorBlendingMode.HardLight: return DefaultPixelBlenders<TPixel>.HardLightPlus;
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgePlus;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnPlus;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightPlus;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferencePlus;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionPlus;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HuePlus;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationPlus;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorPlus;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminosityPlus;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalPlus;
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
                    case PixelColorBlendingMode.ColorDodge: return DefaultPixelBlenders<TPixel>.ColorDodgeSrcOver;
                    case PixelColorBlendingMode.ColorBurn: return DefaultPixelBlenders<TPixel>.ColorBurnSrcOver;
                    case PixelColorBlendingMode.SoftLight: return DefaultPixelBlenders<TPixel>.SoftLightSrcOver;
                    case PixelColorBlendingMode.Difference: return DefaultPixelBlenders<TPixel>.DifferenceSrcOver;
                    case PixelColorBlendingMode.Exclusion: return DefaultPixelBlenders<TPixel>.ExclusionSrcOver;
                    case PixelColorBlendingMode.Hue: return DefaultPixelBlenders<TPixel>.HueSrcOver;
                    case PixelColorBlendingMode.Saturation: return DefaultPixelBlenders<TPixel>.SaturationSrcOver;
                    case PixelColorBlendingMode.Color: return DefaultPixelBlenders<TPixel>.ColorSrcOver;
                    case PixelColorBlendingMode.Luminosity: return DefaultPixelBlenders<TPixel>.LuminositySrcOver;
                    case PixelColorBlendingMode.Normal:
                    default: return DefaultPixelBlenders<TPixel>.NormalSrcOver;
                }
        }
    }
}
