using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Reflection;

namespace WebApplication1
{
	public class Global
	{
		public string api_hello()
		{
			return "hello";
		}
	}

	/// <summary>
	/// Summary description for Handler1
	/// </summary>
	public class Handler1 : JDCloud.Handler
	{
		protected override string Namespace
		{
			get { return "WebApplication1"; }
		}
	}
}