using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Reflection;

namespace Infrastructure.Sql.Components
{
    public static class ModelBuilderExtensions
    {
        // 针对“DbContext 类型”缓存：每种上下文只编译一次
        private static readonly ConcurrentDictionary<Type, Lazy<Action<ModelBuilder>>> _compiledByContext = new();

        /// <summary>
        /// 从“当前 modelBuilder 已识别的实体”中，自动执行实现了 IHasEntityConfiguration&lt;T&gt; 的实体配置。
        /// 结果按 DbContext 类型缓存为 Lazy&lt;Action&lt;ModelBuilder&gt;&gt;，后续同类型上下文直接复用。
        /// </summary>
        public static void ApplyEntityConfigsFromModel(this ModelBuilder modelBuilder, DbContext dbContext)
        {
            var ctxType = dbContext.GetType();

            var lazy = _compiledByContext.GetOrAdd(
                ctxType,
                _ => new Lazy<Action<ModelBuilder>>(
                    // 首次：基于“当前这个 modelBuilder”的实体集进行编译
                    () => CompileForContext(modelBuilder),
                    LazyThreadSafetyMode.ExecutionAndPublication
                )
            );

            // 执行已编译好的动作
            lazy.Value(modelBuilder);
        }

        /// <summary>
        /// 把当前 modelBuilder.Model 中命中的实体类型预编译成一个合成动作：mb => { ApplyOne&lt;T1&gt;(mb); ApplyOne&lt;T2&gt;(mb); ... }
        /// </summary>
        private static Action<ModelBuilder> CompileForContext(ModelBuilder modelBuilder)
        {
            var ifaceDef = typeof(IHasEntityConfiguration<>);
            // 取“已识别的所有实体类型”（包含无 DbSet 但通过导航或显式 Entity<T> 发现的）
            var candidates = modelBuilder.Model.GetEntityTypes()
                .Select(et => et.ClrType)
                .Where(t => t is not null && t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .Distinct()
                .ToArray();
            // 过滤：必须实现 IHasEntityConfiguration<T> 且 T == 本类型
            var hitTypes = candidates.Where(t =>
                t!.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == ifaceDef &&
                    i.GenericTypeArguments[0] == t
                )
            ).ToArray();
            // 为每个命中类型生成强类型委托：mb => ApplyOne<T>(mb)
            var steps = new List<Action<ModelBuilder>>(hitTypes.Length);
            foreach (var t in hitTypes)
            {
                var mi = typeof(ModelBuilderExtensions)
                    .GetMethod(nameof(ApplyOne), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(t!);

                var del = (Action<ModelBuilder>)Delegate.CreateDelegate(typeof(Action<ModelBuilder>), mi);
                steps.Add(del);
            }
            // 合成单一动作：依次执行所有 T 的 ApplyOne<T>(mb)
            return mb =>
            {
                for (int i = 0; i < steps.Count; i++)
                    steps[i](mb);
            };
        }

        // 真正调用点：拿到 EntityTypeBuilder<T> 并执行 T.Configure(b)
        private static void ApplyOne<T>(ModelBuilder mb) where T : class, IHasEntityConfiguration<T>
        {
            var b = mb.Entity<T>();
            T.Configure(b);
        }
    }

}
