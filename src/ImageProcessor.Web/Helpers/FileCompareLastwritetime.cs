// -----------------------------------------------------------------------
// <copyright file="FileCompareLastwritetime.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    #region Using
    using System;
    using System.Collections.Generic;
    #endregion

    /// <summary>
    /// Encapsulates methods to support the comparison of <see cref="T:System.IO.FileInfo"/> objects for equality.
    /// </summary>
    public class FileCompareLastwritetime : IEqualityComparer<System.IO.FileInfo>
    {
        /// <summary>
        /// Converts the value of the current <see cref="T:System.DateTime"/> object to its equivalent
        /// nearest minute representation.
        /// </summary>
        /// <param name="value">An instance of <see cref="T:System.DateTime"/>.</param>
        /// <returns>
        /// A value of the current <see cref="T:System.DateTime"/> object to its equivalent
        /// nearest minute representation.
        /// </returns>
        public static DateTime ToMinute(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind).ToUniversalTime();
        }

        /// <summary>
        ///  Determines whether the specified instances of <see cref="T:System.IO.FileInfo"/> object are equal.
        /// </summary>
        /// <param name="f1">
        /// The first <see cref="T:System.IO.FileInfo"/> object to compare.
        /// </param>
        /// <param name="f2">
        /// The second <see cref="T:System.IO.FileInfo"/> object to compare.
        /// </param>
        /// <returns>true if the specified objects are equal; otherwise, false.</returns>
        public bool Equals(System.IO.FileInfo f1, System.IO.FileInfo f2)
        {
            return ToMinute(f1.LastWriteTimeUtc) == ToMinute(f2.LastWriteTimeUtc);
        }

        /// <summary>
        /// Returns a hash code for the specified <see cref="T:System.IO.FileInfo"/>.
        /// </summary>
        /// <param name="fi">The FileInfo to return the hashcode for.</param>
        /// <returns>A hash code for the specified <see cref="T:System.IO.FileInfo"/>.</returns>
        public int GetHashCode(System.IO.FileInfo fi)
        {
            return ToMinute(fi.LastWriteTimeUtc).GetHashCode();
        }
    }
}
