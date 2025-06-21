// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering;

/// <summary>
/// An error diffusion dithering implementation.
/// </summary>
public readonly partial struct ErrorDither
{
    /// <summary>
    /// Applies error diffusion based dithering using the Atkinson image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither Atkinson = CreateAtkinson();

    /// <summary>
    /// Applies error diffusion based dithering using the Burks image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither Burkes = CreateBurks();

    /// <summary>
    /// Applies error diffusion based dithering using the Floyd–Steinberg image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither FloydSteinberg = CreateFloydSteinberg();

    /// <summary>
    /// Applies error diffusion based dithering using the Jarvis, Judice, Ninke image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither JarvisJudiceNinke = CreateJarvisJudiceNinke();

    /// <summary>
    /// Applies error diffusion based dithering using the Sierra2 image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither Sierra2 = CreateSierra2();

    /// <summary>
    /// Applies error diffusion based dithering using the Sierra3 image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither Sierra3 = CreateSierra3();

    /// <summary>
    /// Applies error diffusion based dithering using the Sierra Lite image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither SierraLite = CreateSierraLite();

    /// <summary>
    /// Applies error diffusion based dithering using the Stevenson-Arce image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither StevensonArce = CreateStevensonArce();

    /// <summary>
    /// Applies error diffusion based dithering using the Stucki image dithering algorithm.
    /// </summary>
    public static readonly ErrorDither Stucki = CreateStucki();

    private static ErrorDither CreateAtkinson()
    {
        const float divisor = 8F;
        const int offset = 1;

        float[,] matrix =
        {
            { 0, 0, 1 / divisor, 1 / divisor },
            { 1 / divisor, 1 / divisor, 1 / divisor, 0 },
            { 0, 1 / divisor, 0, 0 }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateBurks()
    {
        const float divisor = 32F;
        const int offset = 2;

        float[,] matrix =
        {
            { 0, 0, 0, 8 / divisor, 4 / divisor },
            { 2 / divisor, 4 / divisor, 8 / divisor, 4 / divisor, 2 / divisor }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateFloydSteinberg()
    {
        const float divisor = 16F;
        const int offset = 1;

        float[,] matrix =
        {
            { 0, 0, 7 / divisor },
            { 3 / divisor, 5 / divisor, 1 / divisor }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateJarvisJudiceNinke()
    {
        const float divisor = 48F;
        const int offset = 2;

        float[,] matrix =
        {
            { 0, 0, 0, 7 / divisor, 5 / divisor },
            { 3 / divisor, 5 / divisor, 7 / divisor, 5 / divisor, 3 / divisor },
            { 1 / divisor, 3 / divisor, 5 / divisor, 3 / divisor, 1 / divisor }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateSierra2()
    {
        const float divisor = 16F;
        const int offset = 2;

        float[,] matrix =
        {
           { 0, 0, 0, 4 / divisor, 3 / divisor },
           { 1 / divisor, 2 / divisor, 3 / divisor, 2 / divisor, 1 / divisor }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateSierra3()
    {
        const float divisor = 32F;
        const int offset = 2;

        float[,] matrix =
        {
           { 0, 0, 0, 5 / divisor, 3 / divisor },
           { 2 / divisor, 4 / divisor, 5 / divisor, 4 / divisor, 2 / divisor },
           { 0, 2 / divisor, 3 / divisor, 2 / divisor, 0 }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateSierraLite()
    {
        const float divisor = 4F;
        const int offset = 1;

        float[,] matrix =
        {
           { 0, 0, 2 / divisor },
           { 1 / divisor, 1 / divisor, 0 }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateStevensonArce()
    {
        const float divisor = 200F;
        const int offset = 3;

        float[,] matrix =
        {
           { 0,  0,  0,  0,  0, 32 / divisor,  0 },
           { 12 / divisor, 0, 26 / divisor,  0, 30 / divisor,  0, 16 / divisor },
           { 0, 12 / divisor,  0, 26 / divisor,  0, 12 / divisor,  0 },
           { 5 / divisor,  0, 12 / divisor,  0, 12 / divisor,  0,  5 / divisor }
        };

        return new(matrix, offset);
    }

    private static ErrorDither CreateStucki()
    {
        const float divisor = 42F;
        const int offset = 2;

        float[,] matrix =
        {
           { 0, 0, 0, 8 / divisor, 4 / divisor },
           { 2 / divisor, 4 / divisor, 8 / divisor, 4 / divisor, 2 / divisor },
           { 1 / divisor, 2 / divisor, 4 / divisor, 2 / divisor, 1 / divisor }
        };

        return new(matrix, offset);
    }
}
