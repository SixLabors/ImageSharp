using System.Globalization;
using System.Linq;

namespace SixLabors.ImageSharp.Common.Helpers
{
    internal static class FloatToStringUtil
    {
        internal static string FloatToString(params float[] values)
            => string.Join(", ", values.Select(v => v.ToString("#0.##", CultureInfo.InvariantCulture)).ToArray());
    }
}
