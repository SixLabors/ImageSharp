namespace ImageProcessor.Web.Extensions
{
    using System;
    using System.Linq.Expressions;

    internal static class TypePropertyHelpers
    {
        public static string GetPropertyName<T>(Expression<Func<T>> expression)
        {
            MemberExpression member = expression.Body as MemberExpression;
            if (member != null)
            {
                return member.Member.Name;
            }

            throw new ArgumentException("expression");
        }
    }
}
