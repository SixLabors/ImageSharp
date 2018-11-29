// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Implements math operations using tolerant comparison.
    /// </summary>
    internal struct TolerantMath
    {
        private readonly double epsilon;

        private readonly double negEpsilon;

        public TolerantMath(double epsilon)
        {
            DebugGuard.MustBeGreaterThan(epsilon, 0, nameof(epsilon));

            this.epsilon = epsilon;
            this.negEpsilon = -epsilon;
        }

        public static TolerantMath Default { get; } = new TolerantMath(1e-8);

        /// <summary>
        /// <paramref name="a"/> == 0
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsZero(double a) => a > this.negEpsilon && a < this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &gt; 0
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsPositive(double a) => a > this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &lt; 0
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsNegative(double a) => a < this.negEpsilon;

        /// <summary>
        /// <paramref name="a"/> == <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool AreEqual(double a, double b) => this.IsZero(a - b);

        /// <summary>
        /// <paramref name="a"/> &gt; <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsGreater(double a, double b) => a > b + this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &lt; <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsLess(double a, double b) => a < b - this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &gt;= <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsGreaterOrEqual(double a, double b) => a >= b - this.epsilon;

        /// <summary>
        /// <paramref name="a"/> &lt;= <paramref name="b"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool IsLessOrEqual(double a, double b) => b >= a - this.epsilon;
    }
}