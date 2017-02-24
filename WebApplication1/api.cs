using System;
using System.Collections.Generic;

using JDCloud;

namespace JDApi
{
	public class Global
	{
		public string api_hello()
		{
			return "hello";
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