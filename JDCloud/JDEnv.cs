﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Collections.Specialized;
using System.Reflection;
using System.Configuration;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;

/*
对外接口
 */
namespace JDCloud
{
	public class JsObject : Dictionary<string, object>
	{
	}

	public class JsArray : List<object>
	{
	}

	public class DirectReturn : Exception
	{
		public Object retVal;
		public DirectReturn() {}

		public DirectReturn(Object retVal)
		{
			this.retVal = retVal;
		}
	}

	public class MyException : Exception
	{
		public object DebugInfo { get; protected set; }
		public int Code { get; protected set;}
		public MyException(int code, object debugInfo = null, string message = null)
			: base(message != null? message : JDApiBase.GetErrInfo(code))
		{
			Code = code;
			DebugInfo = debugInfo;
		}
	}

	public static class Tool
	{
		public static void RemoveIf_jd<K, V>(this IDictionary<K, V> m, Predicate<K> cond)
		{
			var rmKeys = new HashSet<K>();
			foreach (var k in m.Keys)
			{
				if (cond(k))
					rmKeys.Add(k);
			}
			foreach (var k in rmKeys)
			{
				m.Remove(k);
			}
		}
	}

	public abstract class JDEnvBase: JDApiBase
	{
		public const string ImpClassName = "JDApi.JDEnv";

		private static Assembly asm_;

		private DbConn cnn_;
		public DbConn cnn
		{
			get {
				dbconn();
				return cnn_;
			}
		}

		public bool isTestMode { get; protected set; }
		public int debugLevel = 0;
		public JsArray debugInfo = new JsArray();
		public string appName, appType;
		public string baseDir;

		public HttpContext ctx;
		public new NameValueCollection _GET, _POST;
		public object JsonContent;

		public static JDEnvBase createInstance()
		{
			JDEnvBase env;
			if (asm_ == null)
			{
				foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
				{
					env = asm.CreateInstance(ImpClassName) as JDEnvBase;
					if (env != null)
					{
						asm_ = asm;
						return env;
					}
				}
				jdRet(JDApiBase.E_SERVER, "No class " + ImpClassName);
			}
			return asm_.CreateInstance(ImpClassName) as JDEnvBase;
		}

		public static Assembly getAsmembly()
		{
			return asm_;
		}

		public JDEnvBase()
        {
			this.env = this; // as JDApiBase

			this.isTestMode = int.Parse(getenv("P_TEST_MODE", "0")) != 0;
			this.debugLevel = int.Parse(getenv("P_DEBUG", "0"));
			this.baseDir = getenv("baseDir", System.AppDomain.CurrentDomain.BaseDirectory);
        }

		public void init(HttpContext ctx)
		{
			this.ctx = ctx;
			this._GET = new NameValueCollection(ctx.Request.QueryString);
			this._POST = new NameValueCollection(ctx.Request.Form);

			this.appName = param("_app", "user", "G") as string;
			this.appType = Regex.Replace(this.appName, @"(\d+|-\w+)$", "");

			if (ctx.Request.ContentType != null && ctx.Request.ContentType.IndexOf("/json") > 0)
			{
				var rd = new StreamReader(ctx.Request.InputStream);
				var jsonStr = rd.ReadToEnd();
				this.JsonContent = jsonDecode(jsonStr);
				if (JsonContent is IDictionary<string, object>) 
				{
					var dict = (IDictionary<string, object>)JsonContent;
					foreach (var kv in dict)
					{
						if (!(kv.Value is IList || kv.Value is IDictionary))
							this._POST[kv.Key] = kv.Value.ToString();
					}
				}
			}

			if (this.isTestMode)
			{
				header("X-Daca-Test-Mode", "1");
			}
			// TODO: X-Daca-Mock-Mode, X-Daca-Server-Rev
		}

		public void dbconn()
		{
			if (cnn_ == null)
			{
				var dbType = ConfigurationManager.AppSettings["P_DBTYPE"];
				var connSetting = ConfigurationManager.ConnectionStrings["default"];
				if (connSetting == null)
					jdRet(JDApiBase.E_SERVER, "No db connectionString defined in web.config");

				cnn_ = new DbConn();
				cnn_.onExecSql += new DbConn.OnExecSql(delegate(string sql)
				{
					addLog(sql, 9);
				});
				cnn_.Open(connSetting.ConnectionString, connSetting.ProviderName, dbType);
				cnn_.BeginTransaction();
			}
		}


		public void close(bool ok)
		{
			if (cnn_ != null)
			{
				if (ok)
					cnn_.Commit();
				else
					cnn_.Rollback();
				cnn_.Dispose();
			}
		}

		public class CallSvcOpt
		{
			public bool backupEnv = false;
			public bool isCleanCall = false;
			public bool asAdmin = false;
		}

		// TODO: asAdmin
		public object callSvc(string ac, JsObject param = null, JsObject postParam = null, CallSvcOpt opt = null)
		{
			Match m = Regex.Match(ac, @"(\w+)(?:\.(\w+))?$");
			string ac1 = null;
			string table = null;
			string clsName = null;
			string methodName = null;
			if (m.Groups[2].Length > 0)
			{
				table = m.Groups[1].Value;
				ac1 = m.Groups[2].Value;
				clsName = onCreateAC(table);
				methodName = "api_" + ac1;
			}
			else
			{
				clsName = "Global";
				methodName = "api_" + m.Groups[1].Value;
			}

			JDApiBase api = null;
			Assembly asm = JDEnvBase.getAsmembly();
			api = asm.CreateInstance("JDApi." + clsName) as JDApiBase;
			if (api == null)
			{
				if (table == null)
					jdRet(JDApiBase.E_PARAM, "bad ac=`" + ac + "` (no Global)");

				int code = !hasPerm(JDApiBase.AUTH_LOGIN)? JDApiBase.E_NOAUTH : JDApiBase.E_FORBIDDEN;
				jdRet(code, string.Format("Operation is not allowed for current user on object `{0}`", table));
			}
			Type t = api.GetType();
			MethodInfo mi = t.GetMethod(methodName);
			if (mi == null)
				jdRet(JDApiBase.E_PARAM, "bad ac=`" + ac + "` (no method)");
			api.env = this;

			NameValueCollection[] bak = null;
			if (opt != null)
			{
				if (opt.backupEnv)
					bak = new NameValueCollection[] { this._GET, this._POST };
				if (opt.isCleanCall)
				{
					this._GET = new NameValueCollection();
					this._POST = new NameValueCollection();
				}
			}
			if (param != null)
			{
				foreach (var kv in param)
				{
					this._GET[kv.Key] = kv.Value.ToString();
				}
			}
			if (postParam != null)
			{
				foreach (var kv in postParam)
				{
					this._POST[kv.Key] = kv.Value.ToString();
				}
			}

			object ret = null;
			if (clsName == "Global")
			{
				ret = mi.Invoke(api, null);
			}
			else if (t.IsSubclassOf(typeof(AccessControl)))
			{
				AccessControl accessCtl = api as AccessControl;
				accessCtl.init(table, ac1);
				accessCtl.before();
				object rv = mi.Invoke(api, null);
				//ret[1] = t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, api, null);
				accessCtl.after(ref rv);
				ret = rv;
			}
			else
			{
				jdRet(JDApiBase.E_SERVER, "misconfigured ac=`" + ac + "`");
			}
			if (ret == null)
				ret = "OK";
			if (bak != null)
			{
				this._GET = bak[0] as NameValueCollection;
				this._POST = bak[1] as NameValueCollection;
			}
			return ret;
		}

		public  virtual string onCreateAC(string table)
		{
			return "AC_" + table;
		}

		public virtual int onGetPerms()
		{
			return 0;
		}
	}

}
