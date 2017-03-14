using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Data.Common;
using System.Data;
using System.Collections.Specialized;
using System.Text;
using System.Web.SessionState;
using System.Configuration;

namespace JDCloud
{
	/// <summary>
	/// Summary description for Handler1
	/// </summary>
	public class JDHandler : JDApiBase, IHttpHandler, IRequiresSessionState
	{
		public void ProcessRequest(HttpContext context)
		{
			var ret = new List<object>() {0, null};
			bool ok = false;
			bool dret = false;
			JDEnvBase env = null;
			context.Response.ContentType = "text/plain";
			try
			{
				env = JDEnvBase.createInstance();
				env.init(context);
				this.env = env;

				string path = context.Request.Path;
				Match m = Regex.Match(path, @"api/+([\w|.]+)$");
				//Match m = Regex.Match(path, @"api/(\w+)");
				if (!m.Success)
					throw new MyException(E_PARAM, "bad ac");

				// 测试模式允许跨域
				string origin;
				if (env.isTestMode && (origin = _SERVER["HTTP_ORIGIN"]) != null)
				{
					context.Response.AddHeader("Access-Control-Allow-Origin", origin);
					context.Response.AddHeader("Access-Control-Allow-Credentials", "true");
				}

				string ac = m.Groups[1].Value;
				try
				{
					ret[1] = env.callSvc(ac);
				}
				catch (TargetInvocationException ex)
				{
					if (ex.InnerException != null)
						throw ex.InnerException;
					throw ex;
				}
				ok = true;
			}
			catch (DirectReturn)
			{
				ok = true;
				dret = true;
			}
			catch (MyException ex)
			{
				ret[0] = ex.Code;
				ret[1] = ex.Message;
				ret.Add(ex.DebugInfo);
			}
			catch (Exception ex)
			{
				ret[0] = ex is DbException ? E_DB : E_SERVER;
				ret[1] = GetErrInfo((int)ret[0]);
				if (env != null && env.isTestMode)
				{
					ret.Add(ex.Message);
					ret.Add(ex.StackTrace);
				}
			}
			if (env != null)
			{
				env.close(ok);
				if (env.debugInfo.Count > 0)
					ret.Add(env.debugInfo);
			}

			if (dret)
				return;

			var s = jsonEncode(ret, env.isTestMode);
			context.Response.Write(s);
		}

		public bool IsReusable
		{
			get
			{
				return false;
			}
		}
	}
}
