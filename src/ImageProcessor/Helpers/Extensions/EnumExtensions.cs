// -----------------------------------------------------------------------
// <copyright file="EnumExtensions.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Helpers.Extensions
{
    #region Using
    using System;
    using System.ComponentModel;
    using System.Diagnostics.Contracts;
    #endregion

    /// <summary>
    /// Encapsulates a series of time saving extension methods to <see cref="T:System.Enum">Enum</see>s.
    /// </summary>
    public static class EnumExtensions
    {
        #region Methods
        /// <summary>
        /// Extends the <see cref="T:System.Enum">Enum</see> type to return the description attribute for the given type.
        /// Useful for when the type to match in the data source contains spaces. 
        /// </summary>
        /// <param name="expression">The given <see cref="T:System.Enum">Enum</see> that this method extends.</param>
        /// <returns>A string containing the Enum's description attribute.</returns>
        public static string ToDescription(this Enum expression)
        {
            Contract.Requires(expression != null);

            DescriptionAttribute[] descriptionAttribute =
                (DescriptionAttribute[])
                expression.GetType().GetField(expression.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false);

            return descriptionAttribute.Length > 0 ? descriptionAttribute[0].Description : expression.ToString();
        }
        #endregion
    }
}
