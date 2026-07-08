//using Infrastructure.Redis.SupRoomInfo;
//using Infrastructure.Sql.VBase;
//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Filters;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Caching.Distributed;
//using Microsoft.IdentityModel.Tokens;
//using NLog;
//using System.Reflection;
//using VBaseFramework.Api.Core;
//using VBaseFramework.Core.Auth;
//using VBaseFramework.Core.Extensions;

//namespace VBaseFramework.ServiceDefaults.Core.Auth
//{
//    public class HttpAuthFilter : IAsyncActionFilter
//    {
//        readonly IDistributedCache _Cache;
//        readonly IUserContext _UserContext;
//        readonly VBaseRedisContext _VBaseRedisContext;
//        readonly VBaseContext _VBaseContext;
//        public HttpAuthFilter(IDistributedCache cache, IUserContext userContext, VBaseRedisContext vBaseRedisContext, VBaseContext vBaseContext)
//        {
//            _Cache = cache;
//            _UserContext = userContext;
//            _VBaseRedisContext = vBaseRedisContext;
//            _VBaseContext = vBaseContext;
//        }

//        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
//        {
//            var description =
//                (Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor;

//            var Controllername = description.ControllerName.ToLower();
//            var Actionname = description.ActionName.ToLower();


//            bool flowControl = await LoadUser(context, next);

//            if (string.IsNullOrEmpty(_UserContext.UserId))
//            {
//                //匿名标识
//                var authorize = description.MethodInfo.GetCustomAttribute(typeof(AllowAnonymousAttribute));
//                if (authorize != null)
//                {
//                    await next();
//                    return;
//                }

//                context.HttpContext.Response.StatusCode = 401;
//                context.Result = new JsonResult(new ErrorResponse
//                {
//                    Code = 401,
//                    Message = "认证失败，请提供认证信息"
//                });
//                return;
//            }
//            else
//            {
//                try
//                {
//                    _UserContext.Name = await _VBaseContext.Users.Where(e => e.Id == _UserContext.UserId.To<int>()).Select(e => e.NickName).FirstOrDefaultAsync();
//                }
//                catch (Exception)
//                {

//                }

//                ScopeContext.PushProperty("UserId", _UserContext.UserId);
//            }
//            await next();
//        }

//        private async Task<bool> LoadUser(ActionExecutingContext context, ActionExecutionDelegate next)
//        {
//            var apitoken = "";
//            try
//            {
//                apitoken = context.HttpContext.Request.Headers["apitoken"];
//            }
//            catch (Exception e)
//            {

//            }
//            if (!string.IsNullOrEmpty(apitoken))
//            {
//                var userid = await _VBaseRedisContext.GetUserIdForAppId(apitoken);
//                if (userid > 0)
//                {
//                    _UserContext.IsApi = true;
//                    _UserContext.UserId = userid.ToString();
//                    return true;
//                }
//            }



//            var token = "";
//            try
//            {
//                token = context.HttpContext.Request.Headers["token"];
//            }
//            catch (Exception e)
//            {

//            }

//            string userId = null;
//            try
//            {
//                userId = await _Cache.GetStringAsync(token);
//                _UserContext.UserId = userId;
//                return true;
//            }
//            catch (Exception e)
//            {

//            }
//            return false;

//        }
//    }
//}
