using System;
using System.Globalization;

namespace TH.UI
{
    public readonly struct TooltipTokenValue
    {
        private readonly object value;

        public TooltipTokenValue(object value)
        {
            this.value = value;
        }

        public string ToDisplayString(string format = null, IFormatProvider formatProvider = null)
        {
            if (value == null)
                return string.Empty;

            formatProvider ??= CultureInfo.InvariantCulture;

            if (value is bool boolValue && string.IsNullOrWhiteSpace(format))
                return boolValue ? "true" : "false";

            if (value is IFormattable formattable)
                return formattable.ToString(format, formatProvider);

            return Convert.ToString(value, formatProvider) ?? string.Empty;
        }

        public static implicit operator TooltipTokenValue(string value) => new(value);
        public static implicit operator TooltipTokenValue(int value) => new(value);
        public static implicit operator TooltipTokenValue(float value) => new(value);
        public static implicit operator TooltipTokenValue(double value) => new(value);
        public static implicit operator TooltipTokenValue(bool value) => new(value);
    }
}
