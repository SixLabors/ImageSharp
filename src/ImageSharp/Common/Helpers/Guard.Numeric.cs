// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors
{
    /// <summary>
    /// Provides methods to protect against invalid parameters.
    /// </summary>
    internal static partial class Guard
    {
        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(byte value, byte max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(byte value, byte max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(byte value, byte min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(byte value, byte min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(byte value, byte min, byte max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(sbyte value, sbyte max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(sbyte value, sbyte max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(sbyte value, sbyte min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(sbyte value, sbyte min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(sbyte value, sbyte min, sbyte max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(short value, short max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(short value, short max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(short value, short min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(short value, short min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(short value, short min, short max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(ushort value, ushort max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(ushort value, ushort max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(ushort value, ushort min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(ushort value, ushort min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(ushort value, ushort min, ushort max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(char value, char max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(char value, char max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(char value, char min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(char value, char min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(char value, char min, char max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(int value, int max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(int value, int max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(int value, int min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(int value, int min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(int value, int min, int max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(uint value, uint max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(uint value, uint max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(uint value, uint min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(uint value, uint min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(uint value, uint min, uint max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(float value, float max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(float value, float max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(float value, float min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(float value, float min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(float value, float min, float max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(long value, long max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(long value, long max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(long value, long min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(long value, long min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(long value, long min, long max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(ulong value, ulong max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(ulong value, ulong max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(ulong value, ulong min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(ulong value, ulong min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(ulong value, ulong min, ulong max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(double value, double max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(double value, double max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(double value, double min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(double value, double min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(double value, double min, double max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }

        /// <summary>
        /// Ensures that the specified value is less than a maximum value.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThan(decimal value, decimal max, string parameterName)
        {
            if (value >= max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThan(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is less than or equal to a maximum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeLessThanOrEqualTo(decimal value, decimal max, string parameterName)
        {
            if (value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeLessThanOrEqualTo(value, max, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThan(decimal value, decimal min, string parameterName)
        {
            if (value <= min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThan(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value
        /// and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeGreaterThanOrEqualTo(decimal value, decimal min, string parameterName)
        {
            if (value < min)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeGreaterThanOrEqualTo(value, min, parameterName);
            }
        }

        /// <summary>
        /// Verifies that the specified value is greater than or equal to a minimum value and less than
        /// or equal to a maximum value and throws an exception if it is not.
        /// </summary>
        /// <param name="value">The target value, which should be validated.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <param name="parameterName">The name of the parameter that is to be checked.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is less than the minimum value of greater than the maximum value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MustBeBetweenOrEqualTo(decimal value, decimal min, decimal max, string parameterName)
        {
            if (value < min || value > max)
            {
                ThrowHelper.ThrowArgumentOutOfRangeExceptionForMustBeBetweenOrEqualTo(value, min, max, parameterName);
            }
        }
    }
}
