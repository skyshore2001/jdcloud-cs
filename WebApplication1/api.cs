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
			else if (f == "queryScalar")
			{
				string sql = mparam("sql", null, false) as string;
				ret = queryScalar(sql);
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
	}

	public class AC_ApiLog : AccessControl
	{
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