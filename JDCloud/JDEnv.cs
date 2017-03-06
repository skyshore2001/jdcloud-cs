using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Collections.Specialized;
using System.Reflection;
using System.Configuration;
using System.Text.RegularExpressions;

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

	public abstract class JDEnvBase
	{
		public const string ImpClassName = "JDApi.JDEnv";

		private static Assembly asm_;

		private SSTk.DbConn cnn_;
		public SSTk.DbConn cnn
		{
			get {
				dbconn();
				return cnn_;
			}
		}

		public bool isTestMode { get; protected set; }
		public int debugLevel = 0;
		public JsArray debugInfo = new JsArray();

		public HttpContext ctx;
		public NameValueCollection _GET, _POST;

		public JDApiBase api;

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
				throw new MyException(JDApiBase.E_SERVER, "No class " + ImpClassName);
			}
			return asm_.CreateInstance(ImpClassName) as JDEnvBase;
		}

		public static Assembly getAsmembly()
		{
			return asm_;
		}

		public void init(HttpContext ctx)
		{
			this.ctx = ctx;
			this.api = new JDApiBase();
			this.api.env = this;

			this._GET = new NameValueCollection(ctx.Request.QueryString);
			this._POST = new NameValueCollection(ctx.Request.Form);

			this.isTestMode = int.Parse(ConfigurationManager.AppSettings["P_TESTMODE"]) != 0;
			this.debugLevel = int.Parse(ConfigurationManager.AppSettings["P_DEBUG"]);
		}

		public void dbconn()
		{
			if (cnn_ == null)
			{
				cnn_ = new SSTk.DbConn();

				string connStr = ConfigurationManager.ConnectionStrings["default"].ConnectionString;
				cnn_.Open(SSTk.DbConnType.Odbc, connStr, "", "");
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
			public NameValueCollection _GET, _POST;
			public bool isCleanCall = false;
			public string ac;
			public bool asAdmin = false;
		}

		// TODO: asAdmin
		public object callSvc(string ac, CallSvcOpt opt = null)
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

			JDApiBase obj = null;
			Assembly asm = JDEnvBase.getAsmembly();
			obj = asm.CreateInstance("JDApi." + clsName) as JDApiBase;
			if (obj == null)
				throw new MyException(JDApiBase.E_PARAM, "bad ac=`" + ac + "` (no class)");
			Type t = obj.GetType();
			MethodInfo mi = t.GetMethod(methodName);
			if (mi == null)
				throw new MyException(JDApiBase.E_PARAM, "bad ac=`" + ac + "` (no method)");
			obj.env = this;

			NameValueCollection[] bak = null;
			if (opt != null)
			{
				if (opt.isCleanCall || opt._GET != null|| opt._POST != null)
				{
					bak = new NameValueCollection[] { this._GET, this._POST };
					if (opt.isCleanCall)
					{
						this._GET = new NameValueCollection();
						this._POST = new NameValueCollection();
					}
					if (opt._GET != null)
					{
						foreach (string k in opt._GET)
						{
							this._GET[k] = opt._GET[k];
						}
					}
					if (opt._POST != null)
					{
						foreach (string k in opt._POST)
						{
							this._POST[k] = opt._POST[k];
						}
					}
				}
			}

			object ret = null;
			if (clsName == "Global")
			{
				ret = mi.Invoke(obj, null);
			}
			else if (t.IsSubclassOf(typeof(AccessControl)))
			{
				AccessControl accessCtl = obj as AccessControl;
				accessCtl.init(table, ac1);
				accessCtl.before();
				object rv = mi.Invoke(obj, null);
				//ret[1] = t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, obj, null);
				accessCtl.after(ref rv);
				ret = rv;
			}
			else
			{
				throw new MyException(JDApiBase.E_SERVER, "misconfigured ac=`" + ac + "`");
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
