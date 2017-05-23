// <copyright file="TypeConstants.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Commands
{
    using System;

    /// <summary>
    /// Contains reusable type variables
    /// </summary>
    internal static class TypeConstants
    {
        /// <summary>
        /// The <see cref="sbyte"/> type
        /// </summary>
        public static readonly Type Sbyte = typeof(sbyte);

        /// <summary>
        /// The <see cref="byte"/> type
        /// </summary>
        public static readonly Type Byte = typeof(byte);

        /// <summary>
        /// The <see cref="short"/> type
        /// </summary>
        public static readonly Type Short = typeof(short);

        /// <summary>
        /// The <see cref="ushort"/> type
        /// </summary>
        public static readonly Type UShort = typeof(ushort);

        /// <summary>
        /// The <see cref="int"/> type
        /// </summary>
        public static readonly Type Int = typeof(int);

        /// <summary>
        /// The <see cref="uint"/> type
        /// </summary>
        public static readonly Type UInt = typeof(uint);

        /// <summary>
        /// The <see cref="long"/> type
        /// </summary>
        public static readonly Type Long = typeof(long);

        /// <summary>
        /// The <see cref="ulong"/> type
        /// </summary>
        public static readonly Type ULong = typeof(ulong);

        /// <summary>
        /// The collection of integral number types
        /// </summary>
        public static readonly Type[] IntegralTypes =
        {
            Sbyte,
            Byte,
            Short,
            UShort,
            Int,
            UInt,
            Long,
            ULong
        };

        /// <summary>
        /// The <see cref="ImageSharp.Rgba32"/> type
        /// </summary>
        public static readonly Type Rgba32 = typeof(Rgba32);
    }
}
