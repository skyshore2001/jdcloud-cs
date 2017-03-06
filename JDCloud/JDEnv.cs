using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Collections.Specialized;
using System.Reflection;
using System.Configuration;

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

	public abstract class JDEnv
	{
		public const string ImpClassName = "JDCloud.JDEnvImp";

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
		public NameValueCollection _GET, _POST, _REQUEST;

		public static JDEnv createInstance()
		{
			JDEnv env;
			if (asm_ == null)
			{
				foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
				{
					env = asm.CreateInstance(ImpClassName) as JDEnv;
					if (env != null)
					{
						asm_ = asm;
						return env;
					}
				}
				throw new MyException(JDApiBase.E_SERVER, "No class " + ImpClassName);
			}
			return asm_.CreateInstance(ImpClassName) as JDEnv;
		}

		public static Assembly getAsmembly()
		{
			return asm_;
		}

		public void init(HttpContext ctx)
		{
			this.ctx = ctx;
			this._GET = new NameValueCollection(ctx.Request.QueryString);
			this._POST = new NameValueCollection(ctx.Request.Form);
			this._REQUEST = new NameValueCollection(_POST);
			foreach (string k in _GET)
			{
				_REQUEST[k] = _GET[k];
			}

			this.isTestMode = true;
			this.debugLevel = 9;
		}

		//public static
		public void dbconn()
		{
			// TODO: from conf.user.php
			if (cnn_ == null)
			{
				cnn_ = new SSTk.DbConn();

				//string connStr = ConfigurationManager.AppSettings["dbConnectionString"];
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

		protected string onCreateAC(string table)
		{
			return null;
		}
	}

}
