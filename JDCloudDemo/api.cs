using System;
using System.Collections.Generic;

using JDCloud;
using System.Text.RegularExpressions;

namespace JDApi
{
	public class JDEnv: JDEnvBase
	{
		public override int onGetPerms()
		{
			int perms = 0;
			if (ctx.Session["uid"] != null)
				perms |= JDApiBase.AUTH_USER;

			return perms;
		}

		public override string onCreateAC(string table)
		{
			if (api.hasPerm(JDApiBase.AUTH_USER))
				return "AC1_" + table;
			return "AC_" + table;
		}
	}

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

			var sql = string.Format("SELECT id FROM User WHERE uname={0}", Q(uname));
			var id = queryOne(sql);
			if (id.Equals(false))
				throw new MyException(E_AUTHFAIL, "bad uname or pwd");
			_SESSION["uid"] = id;
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
		protected override void onInit()
		{
			this.requiredFields = new List<string>(){"ac"};
			this.readonlyFields = new List<string>(){"ac", "tm"};
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

	public class AC1_UserApiLog : AC_ApiLog
	{
		private int uid;
		protected override void  onInit()
		{
			this.allowedAc = new List<string>() { "get", "query", "add", "del" };
			this.uid = (int)_SESSION["uid"];

			this.table = "ApiLog";
			this.defaultSort = "id DESC";

			VcolDef vcol = null;
			if (env.cnn.DbType == "mssql")
			{
				vcol = new VcolDef() {
					res = new List<string>() { 
@"(SELECT TOP 3 cast(id as varchar) + ':' + ac + ','
FROM ApiLog log 
WHERE userId=" + this.uid + @" ORDER BY id DESC FOR XML PATH('')
) last3LogAc"
					}
				};
			}
			else if (env.cnn.DbType == "mysql")
			{
				vcol = new VcolDef()
				{
					res = new List<string>() {
@"(SELECT group_concat(concat(id, ':', ac))
FROM (
SELECT id, ac
FROM ApiLog 
WHERE userId=" + this.uid + @" ORDER BY id DESC LIMIT 3) t
) last3LogAc"
					}
				};
			}
			this.vcolDefs = new List<VcolDef>() 
			{
				new VcolDef() {
					res = new List<string>() {"u.name userName"},
					join = "INNER JOIN User u ON u.id=t0.userId",
					isDefault = true
				},
				vcol
			};

			this.subobj = new Dictionary<string, SubobjDef>()
			{
				{ "user", new SubobjDef() {
					sql = "SELECT id,name FROM User u WHERE id=" + this.uid,
					wantOne = true
				}},
				{ "last3Log", new SubobjDef() {
					sql = env.cnn.fixPaging("SELECT id,ac FROM ApiLog log WHERE userId=" + this.uid + " ORDER BY id DESC LIMIT 3"),
				}}
			};
		}

		protected override void onValidate()
		{
			base.onValidate();
			if (this.ac == "add")
			{
				_POST["userId"] = this.uid.ToString();
			}
		}

		protected override void onValidateId()
		{
			if (this.ac == "del")
			{
				var id = mparam("id");
				var rv = queryOne("SELECT id FROM ApiLog WHERE id=" + id + " AND userId=" + this.uid);
				if (!rv.Equals(false))
					throw new MyException(E_FORBIDDEN, "not your log");
			}
		}

		protected override void onQuery()
		{
			base.onQuery();
			this.addCond("userId=" + this.uid);
		}

		public object api_listByAc()
		{
			var ac = mparam("ac", "G") as string;
			var param = new JsObject() {
				{"fmt", "list"},
				{"cond", "ac=" + Q(ac)}
			};

			return env.callSvc("UserApiLog.query", param);
		}
	}
}
