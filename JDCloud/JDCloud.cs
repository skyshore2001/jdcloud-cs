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
				ret[1] = env.callSvc(ac);
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
				Exception ex1 = ex.InnerException;
				if (ex1 == null)
					ex1 = ex;
				if (ex1 is MyException)
				{
					MyException ex2 = ex1 as MyException;
					ret[0] = ex2.Code;
					ret[1] = ex2.Message;
					ret.Add(ex2.DebugInfo);
				}
				else if (ex1 is DirectReturn)
				{
					ok = true;
					dret = true;
				}
				else
				{
					ret[0] = ex1 is DbException ? E_DB : E_SERVER;
					ret[1] = GetErrInfo((int)ret[0]);
					if (env != null && env.isTestMode)
						ret.Add(ex1.Message);
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

			var ser = new JavaScriptSerializer();
			var retStr = ser.Serialize(ret);
			context.Response.ContentType = "text/plain";
			context.Response.Write(retStr);
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
