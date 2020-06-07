// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// An error diffusion dithering implementation.
    /// </summary>
    public readonly partial struct ErrorDither
    {
        /// <summary>
        /// Applies error diffusion based dithering using the Atkinson image dithering algorithm.
        /// </summary>
        public static ErrorDither Atkinson = CreateAtkinson();

        /// <summary>
        /// Applies error diffusion based dithering using the Burks image dithering algorithm.
        /// </summary>
        public static ErrorDither Burkes = CreateBurks();

        /// <summary>
        /// Applies error diffusion based dithering using the Floyd–Steinberg image dithering algorithm.
        /// </summary>
        public static ErrorDither FloydSteinberg = CreateFloydSteinberg();

        /// <summary>
        /// Applies error diffusion based dithering using the Jarvis, Judice, Ninke image dithering algorithm.
        /// </summary>
        public static ErrorDither JarvisJudiceNinke = CreateJarvisJudiceNinke();

        /// <summary>
        /// Applies error diffusion based dithering using the Sierra2 image dithering algorithm.
        /// </summary>
        public static ErrorDither Sierra2 = CreateSierra2();

        /// <summary>
        /// Applies error diffusion based dithering using the Sierra3 image dithering algorithm.
        /// </summary>
        public static ErrorDither Sierra3 = CreateSierra3();

        /// <summary>
        /// Applies error diffusion based dithering using the Sierra Lite image dithering algorithm.
        /// </summary>
        public static ErrorDither SierraLite = CreateSierraLite();

        /// <summary>
        /// Applies error diffusion based dithering using the Stevenson-Arce image dithering algorithm.
        /// </summary>
        public static ErrorDither StevensonArce = CreateStevensonArce();

        /// <summary>
        /// Applies error diffusion based dithering using the Stucki image dithering algorithm.
        /// </summary>
        public static ErrorDither Stucki = CreateStucki();

        private static ErrorDither CreateAtkinson()
        {
            const float Divisor = 8F;
            const int Offset = 1;

            var matrix = new float[,]
            {
                { 0, 0, 1 / Divisor, 1 / Divisor },
                { 1 / Divisor, 1 / Divisor, 1 / Divisor, 0 },
                { 0, 1 / Divisor, 0, 0 }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateBurks()
        {
            const float Divisor = 32F;
            const int Offset = 2;

            var matrix = new float[,]
            {
                { 0, 0, 0, 8 / Divisor, 4 / Divisor },
                { 2 / Divisor, 4 / Divisor, 8 / Divisor, 4 / Divisor, 2 / Divisor }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateFloydSteinberg()
        {
            const float Divisor = 16F;
            const int Offset = 1;

            var matrix = new float[,]
            {
                { 0, 0, 7 / Divisor },
                { 3 / Divisor, 5 / Divisor, 1 / Divisor }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateJarvisJudiceNinke()
        {
            const float Divisor = 48F;
            const int Offset = 2;

            var matrix = new float[,]
            {
                { 0, 0, 0, 7 / Divisor, 5 / Divisor },
                { 3 / Divisor, 5 / Divisor, 7 / Divisor, 5 / Divisor, 3 / Divisor },
                { 1 / Divisor, 3 / Divisor, 5 / Divisor, 3 / Divisor, 1 / Divisor }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateSierra2()
        {
            const float Divisor = 16F;
            const int Offset = 2;

            var matrix = new float[,]
            {
               { 0, 0, 0, 4 / Divisor, 3 / Divisor },
               { 1 / Divisor, 2 / Divisor, 3 / Divisor, 2 / Divisor, 1 / Divisor }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateSierra3()
        {
            const float Divisor = 32F;
            const int Offset = 2;

            var matrix = new float[,]
            {
               { 0, 0, 0, 5 / Divisor, 3 / Divisor },
               { 2 / Divisor, 4 / Divisor, 5 / Divisor, 4 / Divisor, 2 / Divisor },
               { 0, 2 / Divisor, 3 / Divisor, 2 / Divisor, 0 }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateSierraLite()
        {
            const float Divisor = 4F;
            const int Offset = 1;

            var matrix = new float[,]
            {
               { 0, 0, 2 / Divisor },
               { 1 / Divisor, 1 / Divisor, 0 }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateStevensonArce()
        {
            const float Divisor = 200F;
            const int Offset = 3;

            var matrix = new float[,]
            {
               { 0,  0,  0,  0,  0, 32 / Divisor,  0 },
               { 12 / Divisor, 0, 26 / Divisor,  0, 30 / Divisor,  0, 16 / Divisor },
               { 0, 12 / Divisor,  0, 26 / Divisor,  0, 12 / Divisor,  0 },
               { 5 / Divisor,  0, 12 / Divisor,  0, 12 / Divisor,  0,  5 / Divisor }
            };

            return new ErrorDither(matrix, Offset);
        }

        private static ErrorDither CreateStucki()
        {
            const float Divisor = 42F;
            const int Offset = 2;

            var matrix = new float[,]
            {
               { 0, 0, 0, 8 / Divisor, 4 / Divisor },
               { 2 / Divisor, 4 / Divisor, 8 / Divisor, 4 / Divisor, 2 / Divisor },
               { 1 / Divisor, 2 / Divisor, 4 / Divisor, 2 / Divisor, 1 / Divisor }
            };

            return new ErrorDither(matrix, Offset);
        }
    }
}
