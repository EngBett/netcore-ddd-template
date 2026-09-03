using System.ComponentModel;
using System.Reflection;

namespace Template.Common.Extensions
{
    public static class EnumUtilExtension
    {
        public static T ToEnum<T>(this string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        public static string GetDescription(this Enum value)
        {
            var field = value.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Single(x => value.Equals(x.GetValue(null)));

            var description = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute))
                as DescriptionAttribute;

            return description?.Description ?? value.ToString();
        }
    }
}
