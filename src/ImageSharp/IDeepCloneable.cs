// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// A generic interface for a deeply cloneable type.
    /// </summary>
    /// <typeparam name="T">The type of object to clone.</typeparam>
    public interface IDeepCloneable<out T>
        where T : class
    {
        /// <summary>
        /// Creates a new <typeparamref name="T"/> that is a deep copy of the current instance.
        /// </summary>
        /// <returns>The <typeparamref name="T"/>.</returns>
        T DeepClone();
    }

    /// <summary>
    /// An interface for objects that can be cloned. This creates a deep copy of the object.
    /// </summary>
    public interface IDeepCloneable
    {
        /// <summary>
        /// Creates a new object that is a deep copy of the current instance.
        /// </summary>
        /// <returns>The <see cref="IDeepCloneable"/>.</returns>
        IDeepCloneable DeepClone();
    }
}