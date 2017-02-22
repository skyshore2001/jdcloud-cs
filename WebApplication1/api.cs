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

	public class AC_Ordr : AccessControl
	{
		protected new List<string> allowedAc = new List<string>() { "get", "query" };

		public object api_cancel()
		{
			var ret = new Dictionary<string, object>();
			ret["id"] = 100;
			ret["name"] = "hello";
			return ret;
		}
	}
}