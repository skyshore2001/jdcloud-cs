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
			JDEnv env = null;
			try
			{
				env = JDEnv.createInstance();
				env.init(context);
				this.env = env;

				string path = context.Request.Path;
				Match m = Regex.Match(path, @"api/+((\w+)(?:\.(\w+))?)$");
				//Match m = Regex.Match(path, @"api/(\w+)");
				if (!m.Success)
					throw new MyException(E_PARAM, "bad ac");
				// TODO: 测试模式允许跨域
				string origin;
				if ((origin = _SERVER["HTTP_ORIGIN"]) != null)
				{
					context.Response.AddHeader("Access-Control-Allow-Origin", origin);
					context.Response.AddHeader("Access-Control-Allow-Credentials", "true");
				}

				string ac = m.Groups[1].Value;
				string ac1 = null;
				string table = null;
				string clsName = null;
				string methodName = null;
				if (m.Groups[3].Length > 0)
				{
					table = m.Groups[2].Value;
					ac1 = m.Groups[3].Value;
					clsName = AccessControl.create(table);
					methodName = "api_" + ac1;
				}
				else
				{
					clsName = "Global";
					methodName = "api_" + m.Groups[2].Value;
				}

				JDApiBase obj = null;
				Assembly asm = JDEnv.getAsmembly();
				obj = asm.CreateInstance("JDApi." + clsName) as JDApiBase;
				if (obj == null)
					throw new MyException(E_PARAM, "bad ac=`" + ac + "` (no class)");
				Type t = obj.GetType();
				MethodInfo mi = t.GetMethod(methodName);
				if (mi == null)
					throw new MyException(E_PARAM, "bad ac=`" + ac + "` (no method)");
				obj.env = env;
				if (clsName == "Global")
				{
					ret[1] = mi.Invoke(obj, null);
				}
				else if (t.IsSubclassOf(typeof(AccessControl)))
				{
					AccessControl accessCtl = obj as AccessControl;
					accessCtl.init(table, ac1);
					accessCtl.before();
					object rv = mi.Invoke(obj, null);
					//ret[1] = t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, obj, null);
					accessCtl.after(ref rv);
					ret[1] = rv;
				}
				else
				{
					throw new MyException(E_SERVER, "misconfigured ac=`" + ac + "`");
				}
				if (ret[1] == null)
					ret[1] = "OK";
				ok = true;
			}
			catch (DirectReturn)
			{
				ok = true;
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
