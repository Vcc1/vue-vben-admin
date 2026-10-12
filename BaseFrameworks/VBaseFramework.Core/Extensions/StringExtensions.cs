using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.Extensions
{

    public static class StringExtensions
    {
        // 缓存可空类型的底层类型，提高性能
        private static readonly Dictionary<Type, Type> NullableTypeCache = new();

        /// <summary>
        /// 将字符串转换为指定类型 T，严格模式：null 或无法转换都会抛出异常。
        /// </summary>
        public static T To<T>(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException(nameof(value), "值不能为空");

            return (T)ConvertValue(value, typeof(T), throwOnFailure: true);
        }

        /// <summary>
        /// 将字符串转换为指定类型 T，提供默认值：null 返回默认值，非法值抛异常。
        /// </summary>
        public static T To<T>(this string value, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return (T)ConvertValue(value, typeof(T), throwOnFailure: true);
        }

        /// <summary>
        /// 将字符串尝试转换为指定类型 T，所有异常均返回默认值（null 或非法值都容错）。
        /// </summary>
        public static T TryTo<T>(this string value, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            try
            {
                return (T)ConvertValue(value, typeof(T), throwOnFailure: false);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 内部通用转换逻辑：支持枚举、可空类型转换。
        /// </summary>
        private static object ConvertValue(string value, Type targetType, bool throwOnFailure)
        {
            try
            {
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, value, ignoreCase: true);
                var realType = GetUnderlyingType(targetType);


                return Convert.ChangeType(value, realType);
            }
            catch (Exception ex)
            {
                if (throwOnFailure)
                    throw new InvalidCastException($"无法将 '{value}' 转为类型 {targetType.Name}。", ex);
                return null;
            }
        }

        /// <summary>
        /// 获取非空类型（去除 Nullable 包装）。
        /// </summary>
        private static Type GetUnderlyingType(Type type)
        {
            if (!IsNullable(type)) return type;

            if (!NullableTypeCache.TryGetValue(type, out var realType))
            {
                realType = Nullable.GetUnderlyingType(type);
                NullableTypeCache[type] = realType;
            }

            return realType;
        }

        /// <summary>
        /// 判断是否为 Nullable 类型。
        /// </summary>
        private static bool IsNullable(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

}
