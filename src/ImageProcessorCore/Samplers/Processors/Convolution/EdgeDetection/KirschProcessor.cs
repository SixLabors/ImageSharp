// <copyright file="KirschProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class KirschProcessor<TColor, TPacked> : EdgeDetectorCompassFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        public KirschProcessor(bool grayscale)
            : base(new[] { North, Northwest, West, Southwest, South, Southeast, East, Northeast }, grayscale)
        {
        }

        /// <summary>
        ///   Gets the North direction Kirsch kernel mask.
        /// </summary>
        /// 
        public static float[,] North => new float[,]
        {
            { -3, -3, 5 },
            { -3,  0, 5 },
            { -3, -3, 5 }
        };

        /// <summary>
        ///   Gets the Northwest direction Kirsch kernel mask.
        /// </summary>
        /// 
        public static float[,] Northwest => new float[,]
        {
            { -3,  5,  5 },
            { -3,  0,  5 },
            { -3, -3, -3 }
        };

        /// <summary>
        ///   Gets the West direction Kirsch kernel mask.
        /// </summary>
        /// 
        public static float[,] West => new float[,]
        {
            {  5,  5,  5 },
            { -3,  0, -3 },
            { -3, -3, -3 }
        };

        /// <summary>
        ///   Gets the Southwest direction Kirsch kernel mask.
        /// </summary>
        /// 
        public static float[,] Southwest => new float[,]
        {
            {  5,  5, -3 },
            {  5,  0, -3 },
            { -3, -3, -3 }
        };

        /// <summary>
        ///   Gets the South direction Kirsch kernel mask.
        /// </summary>
        /// 
        public static float[,] South => new float[,]
        {
            {  5, -3, -3 },
            {  5,  0, -3 },
            {  5, -3, -3 }
        };

        /// <summary>
        ///   Gets the Southeast direction Kirsch kernel mask.
        /// </summary>
        public static float[,] Southeast => new float[,]
        {
            { -3, -3, -3 },
            { 5, 0, -3 },
            { 5,  5, -3 }
        };

        /// <summary>
        ///   Gets the East direction Kirsch kernel mask.
        /// </summary>
        /// 
        public static float[,] East => new float[,]
        {
            { -3, -3, -3 },
            { -3,  0, -3 },
            { 5,  5,  5 }
        };

        /// <summary>
        ///   Gets the Northeast direction Kirsch kernel mask.
        /// </summary>
        /// 
        public static float[,] Northeast => new float[,]
        {
            { -3, -3, -3 },
            { -3,  0,  5 },
            { -3,  5,  5 }
        };
    }
}
