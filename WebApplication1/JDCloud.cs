using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace JDCloud
{
	public class DirectReturn : Exception
	{
	}

	public class MyException : Exception
	{
		public object DebugInfo { get; protected set; }
		public int Code { get; protected set;}
		public MyException(int code, object debugInfo = null, string message = null)
			:base(message)
		{
			Code = code;
			DebugInfo = debugInfo;
		}
	}

	public class AccessControl
	{
		protected List<string> allowedAc = new List<string>() { "add", "get", "set", "del", "query" };

		public virtual void before()
		{
		}

		public virtual void after()
		{
		}

		public static string create(string table)
		{
			return "AC_" + table;
		}

		public object api_add()
		{
			return 1;
		}

		public object api_set()
		{
			return null;
		}

		public object api_del()
		{
			return null;
		}

		public object api_get()
		{
			return new Dictionary<string, object>();
		}

		public object api_query()
		{
			return new List<object>();
		}
	}

	/// <summary>
	/// Summary description for Handler1
	/// </summary>
	public class JDHandler : IHttpHandler
	{
		public const int E_OK = 0;
		public const int E_PARAM = 1;
		public const int E_NOAUTH = 2;
		public const int E_SERVER = 3;

		public void ProcessRequest(HttpContext context)
		{
			var ret = new List<object>() {0, null};
			try
			{
				string path = context.Request.Path;
				Match m = Regex.Match(path, @"api/((\w+)(?:\.(\w+))?)$");
				//Match m = Regex.Match(path, @"api/(\w+)");
				if (! m.Success)
					throw new MyException(E_PARAM, "bad ac");
				string ac = m.Groups[1].Value;
				string clsName = null;
				string methodName = null;
				if (m.Groups[3].Length > 0)
				{
					clsName = AccessControl.create(m.Groups[2].Value);
					methodName = "api_" + m.Groups[3].Value;
				}
				else
				{
					clsName = "Global";
					methodName = "api_" + m.Groups[2].Value;
				}

				object obj = null;
				Assembly asm = Assembly.GetExecutingAssembly();
				obj = asm.CreateInstance("JDApi." + clsName);
				Type t = obj.GetType();
				if (clsName == "Global")
				{
					ret[1] = t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, obj, null);
				}
				else if (t.IsSubclassOf(typeof(AccessControl)))
				{
					AccessControl accessCtl = (AccessControl)obj;
					accessCtl.before();
					ret[1] = t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, obj, null);
					accessCtl.after();
				}
				else
				{
					throw new MyException(E_PARAM, "bad ac");
				}

				if (ret[1] == null)
					ret[1] = "OK";
			}
			catch(MyException ex)
			{
				ret[0] = ex.Code;
				ret[1] = ex.Message;
				ret.Add(ex.DebugInfo);
			}
			catch (Exception ex)
			{
				ret[0] = E_SERVER;
				ret[1] = null; // TODO
				ret.Add(ex.Message);
			}

			var ser = new JavaScriptSerializer();
			ser.Serialize(ser);
			context.Response.ContentType = "text/plain";
			context.Response.Write(ser.Serialize(ret));
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
