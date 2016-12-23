// <copyright file="EnumHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    public class EnumHelper
    {
        public static T[] GetSortedValues<T>()
        {
            T[] vals = (T[])Enum.GetValues(typeof(T));
            Array.Sort(vals);
            return vals;
        }
    }
}
