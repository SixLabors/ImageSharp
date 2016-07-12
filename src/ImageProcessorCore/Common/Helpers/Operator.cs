namespace ImageProcessorCore.Helpers
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// The Operator class provides easy access to the standard operators
    /// (addition, etc) for generic types, using type inference to simplify
    /// usage.
    /// </summary>
    public static class Operator
    {

        /// <summary>
        /// Indicates if the supplied value is non-null,
        /// for reference-types or Nullable&lt;T&gt;
        /// </summary>
        /// <returns>True for non-null values, else false</returns>
        public static bool HasValue<T>(T value)
        {

            return Operator<T>.NullOp.HasValue(value);

        }

        /// <summary>
        /// Increments the accumulator only
        /// if the value is non-null. If the accumulator
        /// is null, then the accumulator is given the new
        /// value; otherwise the accumulator and value
        /// are added.
        /// </summary>
        /// <param name="accumulator">The current total to be incremented (can be null)</param>
        /// <param name="value">The value to be tested and added to the accumulator</param>
        /// <returns>True if the value is non-null, else false - i.e.
        /// "has the accumulator been updated?"</returns>
        public static bool AddIfNotNull<T>(ref T accumulator, T value)
        {
            return Operator<T>.NullOp.AddIfNotNull(ref accumulator, value);
        }

        /// <summary>
        /// Evaluates unary negation (-) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T Negate<T>(T value)
        {
            return Operator<T>.Negate(value);
        }
        /// <summary>
        /// Evaluates bitwise not (~) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T Not<T>(T value)
        {
            return Operator<T>.Not(value);
        }
        /// <summary>
        /// Evaluates bitwise or (|) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T Or<T>(T value1, T value2)
        {
            return Operator<T>.Or(value1, value2);
        }
        /// <summary>
        /// Evaluates bitwise and (&amp;) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T And<T>(T value1, T value2)
        {
            return Operator<T>.And(value1, value2);
        }
        /// <summary>
        /// Evaluates bitwise xor (^) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T Xor<T>(T value1, T value2)
        {
            return Operator<T>.Xor(value1, value2);
        }
        /// <summary>
        /// Performs a conversion between the given types; this will throw
        /// an InvalidOperationException if the type T does not provide a suitable cast, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this cast.
        /// </summary>
        public static TTo Convert<TFrom, TTo>(TFrom value)
        {
            return Operator<TFrom, TTo>.Convert(value);
        }
        /// <summary>
        /// Evaluates binary addition (+) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>        
        public static T Add<T>(T value1, T value2)
        {
            return Operator<T>.Add(value1, value2);
        }
        /// <summary>
        /// Evaluates binary addition (+) for the given type(s); this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static TArg1 AddAlternative<TArg1, TArg2>(TArg1 value1, TArg2 value2)
        {
            return Operator<TArg2, TArg1>.Add(value1, value2);
        }
        /// <summary>
        /// Evaluates binary subtraction (-) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T Subtract<T>(T value1, T value2)
        {
            return Operator<T>.Subtract(value1, value2);
        }
        /// <summary>
        /// Evaluates binary subtraction(-) for the given type(s); this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static TArg1 SubtractAlternative<TArg1, TArg2>(TArg1 value1, TArg2 value2)
        {
            return Operator<TArg2, TArg1>.Subtract(value1, value2);
        }
        /// <summary>
        /// Evaluates binary multiplication (*) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T Multiply<T>(T value1, T value2)
        {
            return Operator<T>.Multiply(value1, value2);
        }
        /// <summary>
        /// Evaluates binary multiplication (*) for the given type(s); this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static TArg1 MultiplyAlternative<TArg1, TArg2>(TArg1 value1, TArg2 value2)
        {
            return Operator<TArg2, TArg1>.Multiply(value1, value2);
        }
        /// <summary>
        /// Evaluates binary division (/) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static T Divide<T>(T value1, T value2)
        {
            return Operator<T>.Divide(value1, value2);
        }
        /// <summary>
        /// Evaluates binary division (/) for the given type(s); this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static TArg1 DivideAlternative<TArg1, TArg2>(TArg1 value1, TArg2 value2)
        {
            return Operator<TArg2, TArg1>.Divide(value1, value2);
        }
        /// <summary>
        /// Evaluates binary equality (==) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static bool Equal<T>(T value1, T value2)
        {
            return Operator<T>.Equal(value1, value2);
        }
        /// <summary>
        /// Evaluates binary inequality (!=) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static bool NotEqual<T>(T value1, T value2)
        {
            return Operator<T>.NotEqual(value1, value2);
        }
        /// <summary>
        /// Evaluates binary greater-than (&gt;) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static bool GreaterThan<T>(T value1, T value2)
        {
            return Operator<T>.GreaterThan(value1, value2);
        }
        /// <summary>
        /// Evaluates binary less-than (&lt;) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static bool LessThan<T>(T value1, T value2)
        {
            return Operator<T>.LessThan(value1, value2);
        }
        /// <summary>
        /// Evaluates binary greater-than-on-eqauls (&gt;=) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static bool GreaterThanOrEqual<T>(T value1, T value2)
        {
            return Operator<T>.GreaterThanOrEqual(value1, value2);
        }
        /// <summary>
        /// Evaluates binary less-than-or-equal (&lt;=) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static bool LessThanOrEqual<T>(T value1, T value2)
        {
            return Operator<T>.LessThanOrEqual(value1, value2);
        }
        /// <summary>
        /// Evaluates integer division (/) for the given type; this will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary><remarks>
        /// This operation is particularly useful for computing averages and
        /// similar aggregates.
        /// </remarks>
        public static T DivideInt32<T>(T value, int divisor)
        {
            return Operator<int, T>.Divide(value, divisor);
        }
    }
    /// <summary>
    /// Provides standard operators (such as addition) that operate over operands of
    /// different types. For operators, the return type is assumed to match the first
    /// operand.
    /// </summary>
    /// <seealso cref="Operator&lt;T&gt;"/>
    /// <seealso cref="Operator"/>
    public static class Operator<TValue, TResult>
    {
        private static readonly Func<TValue, TResult> convert;
        /// <summary>
        /// Returns a delegate to convert a value between two types; this delegate will throw
        /// an InvalidOperationException if the type T does not provide a suitable cast, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this cast.
        /// </summary>
        public static Func<TValue, TResult> Convert => convert;

        static Operator()
        {
            convert = ExpressionUtil.CreateExpression<TValue, TResult>(body => Expression.Convert(body, typeof(TResult)));
            add = ExpressionUtil.CreateExpression<TResult, TValue, TResult>(Expression.Add, true);
            subtract = ExpressionUtil.CreateExpression<TResult, TValue, TResult>(Expression.Subtract, true);
            multiply = ExpressionUtil.CreateExpression<TResult, TValue, TResult>(Expression.Multiply, true);
            divide = ExpressionUtil.CreateExpression<TResult, TValue, TResult>(Expression.Divide, true);
        }

        private static readonly Func<TResult, TValue, TResult> add, subtract, multiply, divide;
        /// <summary>
        /// Returns a delegate to evaluate binary addition (+) for the given types; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<TResult, TValue, TResult> Add => add;

        /// <summary>
        /// Returns a delegate to evaluate binary subtraction (-) for the given types; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<TResult, TValue, TResult> Subtract => subtract;

        /// <summary>
        /// Returns a delegate to evaluate binary multiplication (*) for the given types; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<TResult, TValue, TResult> Multiply => multiply;

        /// <summary>
        /// Returns a delegate to evaluate binary division (/) for the given types; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<TResult, TValue, TResult> Divide => divide;
    }

    /// <summary>
    /// Provides standard operators (such as addition) over a single type
    /// </summary>
    /// <seealso cref="Operator"/>
    /// <seealso cref="Operator&lt;TValue,TResult&gt;"/>
    public static class Operator<T>
    {
        static readonly INullOp<T> nullOp;
        internal static INullOp<T> NullOp => nullOp;

        static readonly T zero;
        /// <summary>
        /// Returns the zero value for value-types (even full Nullable&lt;TInner&gt;) - or null for reference types
        /// </summary>
        public static T Zero => zero;

        static readonly Func<T, T> negate, not;
        static readonly Func<T, T, T> or, and, xor;
        /// <summary>
        /// Returns a delegate to evaluate unary negation (-) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T> Negate => negate;

        /// <summary>
        /// Returns a delegate to evaluate bitwise not (~) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T> Not => not;

        /// <summary>
        /// Returns a delegate to evaluate bitwise or (|) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, T> Or => or;

        /// <summary>
        /// Returns a delegate to evaluate bitwise and (&amp;) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, T> And => and;

        /// <summary>
        /// Returns a delegate to evaluate bitwise xor (^) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, T> Xor => xor;

        static readonly Func<T, T, T> add, subtract, multiply, divide;

        /// <summary>
        /// Returns a delegate to evaluate binary addition (+) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, T> Add => add;

        /// <summary>
        /// Returns a delegate to evaluate binary subtraction (-) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, T> Subtract => subtract;

        /// <summary>
        /// Returns a delegate to evaluate binary multiplication (*) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, T> Multiply => multiply;

        /// <summary>
        /// Returns a delegate to evaluate binary division (/) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, T> Divide => divide;

        static readonly Func<T, T, bool> equal, notEqual, greaterThan, lessThan, greaterThanOrEqual, lessThanOrEqual;
        
        /// <summary>
        /// Returns a delegate to evaluate binary equality (==) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, bool> Equal => equal;

        /// <summary>
        /// Returns a delegate to evaluate binary inequality (!=) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, bool> NotEqual => notEqual;

        /// <summary>
        /// Returns a delegate to evaluate binary greater-then (&gt;) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, bool> GreaterThan => greaterThan;

        /// <summary>
        /// Returns a delegate to evaluate binary less-than (&lt;) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, bool> LessThan => lessThan;

        /// <summary>
        /// Returns a delegate to evaluate binary greater-than-or-equal (&gt;=) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, bool> GreaterThanOrEqual => greaterThanOrEqual;

        /// <summary>
        /// Returns a delegate to evaluate binary less-than-or-equal (&lt;=) for the given type; this delegate will throw
        /// an InvalidOperationException if the type T does not provide this operator, or for
        /// Nullable&lt;TInner&gt; if TInner does not provide this operator.
        /// </summary>
        public static Func<T, T, bool> LessThanOrEqual => lessThanOrEqual;

        static Operator()
        {
            add = ExpressionUtil.CreateExpression<T, T, T>(Expression.Add);
            subtract = ExpressionUtil.CreateExpression<T, T, T>(Expression.Subtract);
            divide = ExpressionUtil.CreateExpression<T, T, T>(Expression.Divide);
            multiply = ExpressionUtil.CreateExpression<T, T, T>(Expression.Multiply);

            greaterThan = ExpressionUtil.CreateExpression<T, T, bool>(Expression.GreaterThan);
            greaterThanOrEqual = ExpressionUtil.CreateExpression<T, T, bool>(Expression.GreaterThanOrEqual);
            lessThan = ExpressionUtil.CreateExpression<T, T, bool>(Expression.LessThan);
            lessThanOrEqual = ExpressionUtil.CreateExpression<T, T, bool>(Expression.LessThanOrEqual);
            equal = ExpressionUtil.CreateExpression<T, T, bool>(Expression.Equal);
            notEqual = ExpressionUtil.CreateExpression<T, T, bool>(Expression.NotEqual);

            negate = ExpressionUtil.CreateExpression<T, T>(Expression.Negate);
            and = ExpressionUtil.CreateExpression<T, T, T>(Expression.And);
            or = ExpressionUtil.CreateExpression<T, T, T>(Expression.Or);
            not = ExpressionUtil.CreateExpression<T, T>(Expression.Not);
            xor = ExpressionUtil.CreateExpression<T, T, T>(Expression.ExclusiveOr);

            Type typeT = typeof(T);
            if (typeT.GetTypeInfo().IsValueType && typeT.GetTypeInfo().IsGenericType && (typeT.GetGenericTypeDefinition() == typeof(Nullable<>)))
            {
                // get the *inner* zero (not a null Nullable<TValue>, but default(TValue))
                Type nullType = typeT.GetTypeInfo().GenericTypeArguments[0];
                zero = (T)Activator.CreateInstance(nullType);
                nullOp = (INullOp<T>)Activator.CreateInstance(
                        typeof(StructNullOp<>).MakeGenericType(nullType));
            }
            else
            {
                zero = default(T);
                if (typeT.GetTypeInfo().IsValueType)
                {
                    nullOp = (INullOp<T>)Activator.CreateInstance(
                        typeof(StructNullOp<>).MakeGenericType(typeT));
                }
                else
                {
                    nullOp = (INullOp<T>)Activator.CreateInstance(
                        typeof(ClassNullOp<>).MakeGenericType(typeT));
                }
            }
        }
    }
}
