using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Reflection;

namespace JDCloud
{
	/// <summary>
	/// Summary description for Handler1
	/// </summary>
	public class Handler : IHttpHandler
	{
		virtual protected string Namespace
		{
			get { return "JDCloud"; }
		}

		public void ProcessRequest(HttpContext context)
		{
			string ac = context.Request.Params["ac"];
			var ret = new List<object>();
			ret.Add(0);
			ret.Add(null);
			string clsName = "Global";
			string methodName = "api_" + ac;

			object obj = null;
			try
			{
				Assembly asm = Assembly.GetExecutingAssembly();
				obj = asm.CreateInstance(Namespace + "." + clsName);
				Type t = obj.GetType();
				ret[1] = t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, obj, null);
				if (ret[1] == null)
					ret[1] = "OK";
			}
			catch (Exception ex)
			{
				ret[0] = 1;
				ret[1] = "bad class or ac";
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
