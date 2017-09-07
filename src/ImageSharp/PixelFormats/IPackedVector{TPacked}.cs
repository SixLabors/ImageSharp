﻿// <copyright file="IPackedVector{TPacked}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;

    /// <summary>
    /// This interface exists for ensuring signature compatibility to MonoGame and XNA packed color types.
    /// See <a href="https://msdn.microsoft.com/en-us/library/bb197661.aspx" />
    /// </summary>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public interface IPackedVector<TPacked> : IPixel
        where TPacked : struct, IEquatable<TPacked>
    {
        /// <summary>
        /// Gets or sets the packed representation of the value.
        /// </summary>
        TPacked PackedValue { get; set; }
    }
}