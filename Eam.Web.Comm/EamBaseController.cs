using System;
using System.Text;
using System.Web.Mvc; 
namespace Eam.Web.Comm
{
    public class EamBaseController : Controller
    {
        /// <summary>
        /// 操作人，传IP....到后端记录
        /// </summary>
        public virtual Operater Operater
        {
            get { return null; }
        }
        
        protected ContentResult JsonP(string callback, object data)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            return this.Content(string.Format("{0}({1})", callback, json));
        }        

        /// <summary>
        ///  警告并且历史返回
        /// </summary>
        /// <param name="notice"></param>
        /// <returns></returns>
        public ContentResult Back(string notice)
        {
            var content = new StringBuilder("<script>");
            if (!string.IsNullOrEmpty(notice))
                content.AppendFormat("alert('{0}');", notice);
            content.Append("history.go(-1)</script>");
            return this.Content(content.ToString());
        }

        public ContentResult PageReturn(string msg, string url = null)
        {
            var content = new StringBuilder("<script type='text/javascript'>");
            if (!string.IsNullOrEmpty(msg))
                content.AppendFormat("alert('{0}');", msg);
            if (string.IsNullOrWhiteSpace(url))
                url = Request.Url.ToString();
            content.Append("window.location.href='" + url + "'</script>");
            return this.Content(content.ToString());
        }

        /// <summary>
        /// 转向到一个提示页面，然后自动返回指定的页面
        /// </summary>
        /// <param name="notice"></param>
        /// <param name="redirect"></param>
        /// <returns></returns>
        public ContentResult Stop(string notice, string redirect, bool isAlert = false)
        {
            var content = "<meta http-equiv='refresh' content='1;url=" + redirect +
                          "' /><body style='margin-top:0px;color:red;font-size:24px;'>" + notice + "</body>";
            if (isAlert)
                content = string.Format("<script>alert('{0}'); window.location.href='{1}'</script>", notice, redirect);

            return this.Content(content);
        }

        /// <summary>
        /// 在方法执行前更新操作人
        /// </summary>
        /// <param name="filterContext"></param>
        public virtual void UpdateOperater(ActionExecutingContext filterContext)
        {
            if (this.Operater == null)
                return;
            //WCFContext.Current.Operater = this.Operater;
        }

        public virtual void ClearOperater()
        {
            //TODO
        }

        /// <summary>
        /// AOP拦截，在Action执行后
        /// </summary>
        /// <param name="filterContext">filter context</param>
        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            base.OnActionExecuted(filterContext);
            /*if (!filterContext.RequestContext.HttpContext.Request.IsAjaxRequest() && !filterContext.IsChildAction)
                RenderViewData();*/

            this.ClearOperater();
        }

        
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.UpdateOperater(filterContext);
            base.OnActionExecuting(filterContext);
        } 

        /// <summary>
        /// 当前Http上下文信息，用于写Log或其他作用
        /// </summary>
        public WebExceptionContext WebExceptionContext
        {
            get
            {
                var exceptionContext = new WebExceptionContext
                {
                    Ip = Fetch.UserIp,
                    CurrentUrl = Fetch.CurrentUrl,
                    RefUrl = (Request == null || Request.UrlReferrer == null)
                            ? string.Empty
                            : Request.UrlReferrer.AbsoluteUri,
                    IsAjaxRequest = (Request != null) && Request.IsAjaxRequest(),
                    FormData = (Request == null) ? null : Request.Form,
                    QueryData = (Request == null) ? null : Request.QueryString,
                    RouteData = (Request == null || Request.RequestContext == null || Request.RequestContext.RouteData == null)
                            ? null
                            : Request.RequestContext.RouteData.Values
                };

                return exceptionContext;
            }
        }

        /// <summary>
        /// 发生异常写Log
        /// </summary>
        /// <param name="filterContext"></param>
        protected override void OnException(ExceptionContext filterContext)
        {
            base.OnException(filterContext);
            var e = filterContext.Exception;
            //LogException(e, this.WebExceptionContext);
        }

    }
}