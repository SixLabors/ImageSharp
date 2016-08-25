// <copyright file="IAlmostEquatable.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;

    /// <summary>
    /// Defines a generalized method that a value type or class implements to create 
    /// a type-specific method for determining approximate equality of instances.
    /// </summary>
    /// <typeparam name="TColor">The type of objects to compare.</typeparam>
    /// <typeparam name="TPrecision">The object specifying the type to specify precision with.</typeparam>
    public interface IAlmostEquatable<in TColor, in TPrecision> 
        where TPrecision : struct, IComparable<TPrecision>
    {
        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type
        /// when compared to the specified precision level.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <param name="precision">The object specifying the level of precision.</param>
        /// <returns>
        /// true if the current object is equal to the other parameter; otherwise, false.
        /// </returns>
        bool AlmostEquals(TColor other, TPrecision precision);
    }
}
