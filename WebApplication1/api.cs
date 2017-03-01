using System;
using System.Collections.Generic;

using JDCloud;
using System.Text.RegularExpressions;

namespace JDApi
{
	public class Global : JDApiBase
	{
		public object api_fn()
		{
			object ret = null;
			string f = mparam("f") as string;
			if (f == "param")
			{
				string name = mparam("name") as string;
				string coll = param("coll") as string;
				string defVal = param("defVal") as string;

				ret = param(name, defVal, coll);
			}
			else if (f == "mparam")
			{
				string name = mparam("name") as string;
				string coll = param("coll") as string;
				ret = mparam(name, coll);
			}

			else if (f == "queryAll")
			{
				string sql = mparam("sql", null, false) as string;
				bool assoc = (bool)param("assoc/b", false);
				ret = queryAll(sql, assoc);
			}
			else if (f == "queryOne")
			{
				string sql = mparam("sql", null, false) as string;
				bool assoc = (bool)param("assoc/b", false);
				ret = queryOne(sql, assoc);
			}
			else if (f == "execOne")
			{
				string sql = mparam("sql", null, false) as string;
				bool getNewId = (bool)param("getNewId/b", false);
				ret = execOne(sql, getNewId);
			}
			else
				throw new MyException(E_SERVER, "not implemented");
			return ret;
		}

		public object api_hello()
		{
			return new JsObject{
				{"id", 100},
				{"name", "hello"}
			};
		}

		public object api_login()
		{
			var uname = mparam("uname") as string;
			var pwd = mparam("pwd") as string;
			var id = 1001;
			_SESSION["uid"] = 1001;
			return new JsObject() {
				{"id", id}
			};
		}

		public object api_whoami()
		{
			checkAuth(AUTH_USER);
			var uid = (int)_SESSION["uid"];
			return new JsObject()
			{
				{"id", uid}
			};
		}
		public void api_logout()
		{
			// checkAuth(AUTH_LOGIN);
			if (_SESSION != null)
				_SESSION.Abandon();
		}
	}

	public class AC_ApiLog : AccessControl
	{
		public AC_ApiLog()
		{
			this.requiredFields = new List<string>(){"ac"};
			this.readonlyFields = new List<string>(){"tm"};
			this.readonlyFields2 = new List<string>(){"ac"};
			this.hiddenFields = new List<string>(){"ua"};
		}
		protected override void onValidate()
		{
			if (this.ac == "add")
			{
				_POST["tm"] = DateTime.Now.ToString();
			}
		}
	}

	public class AC_Ordr : AccessControl
	{
		public AC_Ordr()
		{
			allowedAc = new List<string>() { "get", "query", "add", "set" };
		}

		public object api_cancel()
		{
			var ret = new Dictionary<string, object>();
			ret["id"] = 100;
			ret["name"] = "hello";
			return ret;
		}
	}
}