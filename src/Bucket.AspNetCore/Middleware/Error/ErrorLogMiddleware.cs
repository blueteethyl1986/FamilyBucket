﻿using Bucket.Buried;
using Bucket.ErrorCode;
using Bucket.Exceptions;
using Bucket.Values;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
namespace Bucket.AspNetCore.Middleware.Error
{
    public class ErrorLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IErrorCodeStore _errorCodeStore;
        private readonly IBuriedContext _buriedContext;
        public ErrorLogMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IErrorCodeStore errorCodeStore, IBuriedContext buriedContext)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<ErrorLogMiddleware>();
            _errorCodeStore = errorCodeStore;
            _buriedContext = buriedContext;
        }

        public async Task Invoke(HttpContext context)
        {
            ErrorResult errorInfo = null;
            try
            {
                await _next(context);
            }
            catch (BucketException ex)
            {
                var newMsg = _errorCodeStore.StringGet(ex.ErrorCode);
                if (string.IsNullOrWhiteSpace(newMsg))
                    newMsg = ex.ErrorMessage;
                errorInfo = new ErrorResult(ex.ErrorCode, newMsg);
            }
            catch (Exception ex)
            {
                errorInfo = new ErrorResult("-1", "系统开小差了,请稍后再试");
                _logger.LogError(ex, $"全局异常捕获,状态码：{ context.Response.StatusCode}");
            }
            finally
            {
                if (errorInfo != null)
                {
                    var Message = JsonConvert.SerializeObject(errorInfo);
                    var inputParam = string.Empty;
                    var HttpUrl = new StringBuilder().Append(context.Request.Scheme)
                                      .Append("://")
                                      .Append(context.Request.Host)
                                      .Append(context.Request.PathBase)
                                      .Append(context.Request.Path)
                                      .Append(context.Request.QueryString)
                                      .ToString();
                    if (context.Request.Method.ToLower() == HttpMethod.Post.ToString().ToLower())
                    {
                        using (var ms = new MemoryStream())
                        {
                            try { context.Request.Body.Position = 0; } catch (Exception ex) { }
                            context.Request.Body.CopyTo(ms);
                            ms.Position = 0;
                            var myByteArray = ms.ToArray();
                            inputParam = Encoding.UTF8.GetString(myByteArray);
                        }
                    }
                    else if (context.Request.Method.ToLower() == HttpMethod.Get.ToString().ToLower())
                    {
                        inputParam = context.Request.QueryString.Value;
                    }
                    await _buriedContext.PublishAsync(new BuriedInformationInput
                    {
                        FuncName = context.Request.Path,
                        ApiType = 1,
                        ApiUri = HttpUrl,
                        BussinessSuccess = 0,
                        CalledResult = errorInfo.ErrorCode == "-1" ? -1 : 0,
                        InputParams = inputParam,
                        OutputParams = Message,
                        ErrorCode = errorInfo.ErrorCode
                    });
                    await HandleExceptionAsync(context, Message);
                }
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, string message)
        {
            context.Response.ContentType = "application/json;charset=utf-8";
            return context.Response.WriteAsync(message);
        }
    }
}
