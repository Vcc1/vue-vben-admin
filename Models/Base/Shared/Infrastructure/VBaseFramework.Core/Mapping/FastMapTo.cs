using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace VBaseFramework.Core.Mapping
{
    public static class FastMapTo
    {
        private static readonly ConcurrentDictionary<(Type S, Type T, bool IgnoreCase), Action<object, object>> _compiled = new();

        public static TTarget MapTo<TTarget>(this object source, bool ignoreCase = true)
            where TTarget : new()
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            var target = new TTarget();
            MapToExisting(source, target, ignoreCase);
            return target;
        }

        public static void MapToExisting(this object source, object target, bool ignoreCase = true)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));

            var key = (source.GetType(), target.GetType(), ignoreCase);
            var act = _compiled.GetOrAdd(key, k => BuildMapDelegate(k.S, k.T, k.IgnoreCase));
            act(source, target);
        }

        private static Action<object, object> BuildMapDelegate(Type sType, Type tType, bool ignoreCase)
        {
            var sParam = Expression.Parameter(typeof(object), "src");
            var tParam = Expression.Parameter(typeof(object), "dst");
            var src = Expression.Variable(sType, "s");
            var dst = Expression.Variable(tType, "t");

            var assignSrc = Expression.Assign(src, Expression.Convert(sParam, sType));
            var assignDst = Expression.Assign(dst, Expression.Convert(tParam, tType));

            var sProps = sType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
            var tProps = tType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0);

            var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var tDict = tProps.ToDictionary(p => p.Name, p => p, comparer);

            var body = new List<Expression> { assignSrc, assignDst };

            foreach (var sp in sProps)
            {
                if (!tDict.TryGetValue(sp.Name, out var tp)) continue;

                // 类型兼容：T 赋自 S
                if (tp.PropertyType.IsAssignableFrom(sp.PropertyType) || IsNullableOf(tp.PropertyType, sp.PropertyType))
                {
                    body.Add(Expression.Assign(
                        Expression.Property(dst, tp),
                        Expression.Convert(Expression.Property(src, sp), tp.PropertyType)
                    ));
                }
            }

            var block = Expression.Block(new[] { src, dst }, body);
            var lambda = Expression.Lambda<Action<object, object>>(block, sParam, tParam);
            return lambda.Compile();
        }

        private static bool IsNullableOf(Type nullableType, Type inner)
            => nullableType.IsGenericType
               && nullableType.GetGenericTypeDefinition() == typeof(Nullable<>)
               && nullableType.GetGenericArguments()[0].IsAssignableFrom(inner);
    }
}
