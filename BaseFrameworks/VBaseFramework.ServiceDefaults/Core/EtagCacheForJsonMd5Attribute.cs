using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VBaseFramework.ServiceDefaults.Core
{
    /// <summary>
    /// 基于“响应对象 -> JSON -> MD5”的 ETag 生成与校验过滤器。
    /// 用法：在 Action 上标记 [EtagFromJsonMd5]
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class EtagCacheForJsonMd5Attribute : Attribute, IAsyncResultFilter
    {
        public bool Weak { get; init; } = false; // 需要弱验证可设为 true，返回形如：W/"xxxx"
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            // 仅处理 200/OK 场景且结果为 ObjectResult（JSON）
            if (context.Result is ObjectResult obj && obj.Value is not null)
            {
                // 拿到与 MVC 一致的 JsonOptions，避免序列化差异导致 ETag 不稳定
                var jsonOpts = context.HttpContext.RequestServices
                    .GetService(typeof(IOptions<JsonOptions>)) as IOptions<JsonOptions>;

                var serializerOptions = jsonOpts?.Value?.JsonSerializerOptions ?? new JsonSerializerOptions
                {
                    // 根据你的项目需要决定大小写/忽略空值等；默认即可
                    WriteIndented = false
                };

                // 1) 将响应对象序列化为 JSON
                var json = JsonSerializer.Serialize(obj.Value, serializerOptions);

                // 2) 计算 MD5（十六进制大写），并按 RFC 规则加引号
                string etag;
                using (var md5 = MD5.Create())
                {
                    var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(json));
                    var hex = Convert.ToHexString(hash); // AABBCC...
                    etag = $"\"{hex}\"";
                }

                if (Weak)
                {
                    etag = $"W/{etag}";
                }

                var request = context.HttpContext.Request;
                var response = context.HttpContext.Response;

                // 3) 比对 If-None-Match
                if (request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inmHeaders))
                {
                    // If-None-Match 可能是多个值，用逗号分隔；需要逐个 Trim 比较
                    var matched = inmHeaders
                        .SelectMany(v => v.Split(','))
                        .Select(v => v.Trim())
                        .Any(v => string.Equals(v, etag, StringComparison.Ordinal));

                    if (matched)
                    {
                        // 匹配：资源未修改 → 返回 304；写回当前 ETag 以便客户端更新其头部
                        response.Headers[HeaderNames.ETag] = etag;

                        // 建议添加缓存控制（可按需调整）
                        if (!response.Headers.ContainsKey(HeaderNames.CacheControl))
                        {
                            response.Headers[HeaderNames.CacheControl] = "public, max-age=0, must-revalidate";
                        }

                        context.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
                        await next();
                        return; // 不再执行后续写入
                    }
                }

                // 4) 不匹配：资源已更新或首次请求 → 下发 ETag，继续正常输出 200 + Body
                response.Headers[HeaderNames.ETag] = etag;
                if (!response.Headers.ContainsKey(HeaderNames.CacheControl))
                {
                    response.Headers[HeaderNames.CacheControl] = "public, max-age=0, must-revalidate";
                }
            }

            await next();
        }
    }
}
