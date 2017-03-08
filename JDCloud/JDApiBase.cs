using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Web.SessionState;
using System.Data.Common;
using System.Web;

/*
在JDApiBase中实现工具函数。
 */
namespace JDCloud
{
	public class JDApiBase
	{
		public const int E_ABORT = -100;
		public const int E_AUTHFAIL = -1;
		public const int E_OK = 0;
		public const int E_PARAM = 1;
		public const int E_NOAUTH = 2;
		public const int E_DB = 3;
		public const int E_SERVER = 4;
		public const int E_FORBIDDEN = 5;

		public const int PAGE_SZ_LIMIT = 10000;

		// 登录类型定义：
		public const int AUTH_USER = 0x1;
		public const int AUTH_EMP = 0x2;
		// 支持8种登录类型 0x1-0x80; 其它权限应从0x100开始定义。
		public const int AUTH_LOGIN = 0xff; 

		public JDEnvBase env;
		public NameValueCollection _GET 
		{
			get { return env._GET;}
		}
		public NameValueCollection _POST
		{
			get { return env._POST;}
		}
		public NameValueCollection _SERVER
		{
			get { return env.ctx.Request.ServerVariables;}
		}
		public HttpSessionState _SESSION
		{
			get { return env.ctx.Session;}
		}

		public static readonly Dictionary<int, string> ERRINFO = new Dictionary<int, string>(){
			{ E_AUTHFAIL, "认证失败" },
			{ E_PARAM, "参数不正确" },
			{ E_NOAUTH, "未登录" },
			{ E_DB, "数据库错误" },
			{ E_SERVER, "服务器错误"},
			{ E_FORBIDDEN, "禁止操作"},
		};

		public static string GetErrInfo(int code)
		{
			if (ERRINFO.ContainsKey(code))
				return ERRINFO[code];
			return "未知错误";
		}

		public void header(string key, string value)
		{
			env.ctx.Response.AddHeader(key, value);
		}
		public void echo(params object[] objs)
		{
			foreach (var o in objs)
			{
				if (o is byte[])
					env.ctx.Response.BinaryWrite(o as byte[]);
				else
					env.ctx.Response.Write(o);
			}
		}

		private string parseType_(ref string name)
		{
			string type = null;
			int n;
			if ((n=name.IndexOf('/')) >= 0)
			{
				type = name.Substring(n+1);
				name = name.Substring(0, n);
			}
			else {
				if (name == "id" || name.EndsWith("Id")) {
					type = "i";
				}
				else {
					type = "s";
				}
			}
			return type;
		}

		public bool tryParseBool(string s, out bool val)
		{
			val = false;
			if (s == null)
			{
				return true;
			}
			s = s.ToLower();
			if (s=="0" || s=="false" || s=="off" || s == "no")
				val = false;
			else if (s=="1" || s=="true" || s=="on" || s =="yes")
				val = true;
			else
				return false;
			return true;
		}

		class ElemType
		{
			public string type; // type
			public bool optional;
		}

		public JsArray param_varr(string str, string type, string name)
		{
			JsArray ret = new JsArray();
			var elemTypes = new List<ElemType>();
			foreach (var t in type.Split(':'))
			{
				int tlen = t.Length;
				if (tlen == 0)
					throw new MyException(E_SERVER, string.Format("bad type spec: `{0}`", type));
				bool optional = false;
				string t1= t;
				if (t[tlen-1] == '?')
				{
					t1 = t.Substring(0, tlen-1);
					optional = true;
				}
				elemTypes.Add(new ElemType() {type=t1, optional=optional});
			}
			int colCnt = elemTypes.Count;

			foreach (var row0 in str.Split(','))
			{
				var row = row0.Split(new char[] {':'}, colCnt);
				Array.Resize(ref row, colCnt);
				/*
				while (row.Length < colCnt) {
					row[] = null;
				}
				*/

				var row1 = new JsArray();
				for (int i=0; i<row.Length; ++i)
				{
					var e = row[i];
					var t = elemTypes[i];
					if (e == null || e.Length == 0)
					{
						if (t.optional)
						{
							row1.Add(null);
							continue;
						}
						throw new MyException(E_PARAM, string.Format("Bad Request - param `{0}`: list({1}). require col: `{2}`[{3}]", name, type, row0, i));
					}
					string v = htmlEscape(e);
					if (t.type == "i") 
					{
						int ival;
						if (! int.TryParse(v, out ival))
							throw new MyException(E_PARAM, string.Format("Bad Request - param `{0}`: list({1}). require integer col: `{2}`[{3}]=`{4}`.", name, type, row0, ival, v));
						row1.Add(ival);
					}
					else if (t.type == "n") 
					{
						decimal n;
						if (! decimal.TryParse(v, out n))
							throw new MyException(E_PARAM, string.Format("Bad Request - param `{0}`: list({1}). require numberic col: `{2}`[{3}]=`{4}`.", name, type, row0, i, v));
						row1.Add(n);
					}
					else if (t.type == "b")
					{
						bool b;
						if (!tryParseBool(v, out b))
							throw new MyException(E_PARAM, string.Format("Bad Request - param `{0}`: list({1}). require boolean col: `{2}`[{3}]=`{4}`.", name, type, row0, i, v));
						row1.Add(b);
					}
					else if (t.type == "s")
					{
						row1.Add(v);
					}
					else if (t.type == "dt" || t.type == "tm") {
						DateTime dt;
						if (! DateTime.TryParse(v, out dt))
							throw new MyException(E_PARAM, string.Format("Bad Request - param `{0}`: list({1}). require datetime col: `{2}`[{3}]=`{4}`.", name, t.type, row0, i, v));
						row1.Add(dt);
					}
					else {
						throw new MyException(E_SERVER, string.Format("unknown elem type `{0}` for param `{1}`: list({2})", t.type, name, v));
					}
				}
				ret.Add(row1);
			}
			if (ret.Count == 0)
				throw new MyException(E_PARAM, "Bad Request - list param `{0}` is empty.", name);
			return ret;
		}

		public object param(string name, object defVal = null, string coll = null, bool doHtmlEscape = true)
		{
			string type = parseType_(ref name);
			string val = null;
			object ret = null;
			if (coll == null || coll == "G")
				val = _GET[name];
			if ((val == null && coll == null) || coll == "P")
				val = _POST[name];
			if (val == null && defVal != null)
				return defVal;

			if (val != null) 
			{
				if (type == "s")
				{
					// avoid XSS attack
					if (doHtmlEscape)
						ret = htmlEscape(val);
					else
						ret = val;
				}
				else if (type == "i")
				{
					int i;
					if (!int.TryParse(val, out i))
						throw new MyException(E_PARAM, string.Format("Bad Request - integer param `{0}`=`{1}`.", name, val));
					ret = i;
				}
				else if (type == "n")
				{
					double n;
					if (!double.TryParse(val, out n))
						throw new MyException(E_PARAM, string.Format("Bad Request - numeric param `{0}`=`{1}`.", name, val));
					ret = n;
				}
				else if (type == "b")
				{
					bool b;
					if (!tryParseBool(val, out b))
						throw new MyException(E_PARAM, string.Format("Bad Request - bool param `{0}`=`{1}`.", name, val));
					ret = b;
				}
				else if (type == "i+")
				{
					var arr = new List<int>();
					foreach (var e in val.Split(','))
					{
						int i;
						if (!int.TryParse(e, out i))
							throw new MyException(E_PARAM, string.Format("Bad Request - int array param `{0}` contains `{1}`.", name, e));
						arr.Add(i);
					}
					if (arr.Count == 0)
						throw new MyException(E_PARAM, string.Format("Bad Request - int array param `{0}` is empty.", name));
					ret = arr;
				}
				else if (type == "dt" || type == "tm")
				{
					DateTime dt;
					if (!DateTime.TryParse(val, out dt))
						throw new MyException(E_PARAM, string.Format("Bad Request - invalid datetime param `{0}`=`{1}`.", name, val));
					ret = dt;
				}
				/*
				else if (type == "js" || type == "tbl") {
					ret1 = json_decode(ret, true);
					if (ret1 == null)
						throw new MyException(E_PARAM, "Bad Request - invalid json param `name`=`ret`.");
					if (type == "tbl") {
						ret1 = table2objarr(ret1);
						if (ret1 == false)
							throw new MyException(E_PARAM, "Bad Request - invalid table param `name`=`ret`.");
					}
					ret = ret1;
				}
				*/
				else if (type.Contains(':'))
				{
					ret = param_varr(val, type, name);
				}
				else
				{
					throw new MyException(E_SERVER, string.Format("unknown type `{0}` for param `{1}`", type, name));
				}
			}
			return ret;
		}

		public object mparam(string name, string coll = null, bool htmlEscape = true)
		{
			object val = param(name, null, coll, htmlEscape);
			if (val == null)
				throw new MyException(E_PARAM, "require param `" + name + "`");
			return val;
		}

		public static string Q(string s)
		{
			return "'" + s.Replace("'", "\\'") + "'";
		}

		// return: JsObject or JsArray
		public object readerToCol(DbDataReader rd, bool assoc)
		{
			object ret = null;
			if (assoc)
			{
				var jsobj = new JsObject();
				for (int i = 0; i < rd.FieldCount; ++i)
				{
					jsobj[rd.GetName(i)] = rd.GetValue(i);
				}
				ret = jsobj;
			}
			else
			{
				var jsarr = new JsArray();
				for (int i = 0; i < rd.FieldCount; ++i)
				{
					jsarr.Add(rd.GetValue(i));
				}
				ret = jsarr;
			}
			return ret;
		}

		// 每一项是JsArray或JsObject(assoc=true)
		public JsArray queryAll(string sql, bool assoc = false)
		{
			addLog(sql, 9);
			DbDataReader rd = env.cnn.ExecQuery(sql);
			var ret = new JsArray();
			if (rd.HasRows)
			{
				while (rd.Read())
				{
					ret.Add(readerToCol(rd, assoc));
				}
			}
			rd.Close();
			return ret;
		}

		/**
		// can cast to JsObject or JsArray. 如果assoc=false且只有一列，直接返回数据。(相当于queryScalar)
		var rv = queryOne("...");
		if (rv.Equals(false)) { } // no data
		*/
		public object queryOne(string sql, bool assoc = false)
		{
			addLog(sql, 9);
			DbDataReader rd = env.cnn.ExecQuery(sql);
			object ret = null;
			if (rd.HasRows)
			{
				ret = readerToCol(rd, assoc);
				if (!assoc && (ret as JsArray).Count == 1)
					ret = (ret as JsArray)[0];
			}
			else
			{
				ret = false;
			}
			rd.Close();
			return ret;
		}
		public object queryScalar(string sql)
		{
			addLog(sql, 9);
			return env.cnn.ExecScalar(sql);
		}

		public int execOne(string sql, bool getNewId = false)
		{
			addLog(sql, 9);
			int ret = env.cnn.ExecNonQuery(sql);
			if (getNewId)
			{
				ret = getLastInsertId();
			}
			return ret;
		}

		// TODO: now just mysql; mssql uses "SELECT SCOPE_IDENTITY()" or "SELECT @@IDENTITY"
		public int getLastInsertId()
		{
			object ret = env.cnn.ExecScalar("SELECT LAST_INSERT_ID()");
			return Convert.ToInt32(ret);
		}

		public string htmlEscape(string s)
		{
			return HttpUtility.HtmlEncode(s);
		}

		public void addLog(string s, int level = 0)
		{
			if (env.isTestMode && env.debugLevel >= level)
			{
				env.debugInfo.Add(s);
			}
		}
		public void logit(string s, string which = "trace")
		{
			//TODO
		}

		public void checkAuth(int perms)
		{
			if (hasPerm(perms))
				return;
			if (hasPerm(AUTH_LOGIN))
				throw new MyException(E_FORBIDDEN, "permission denied.");
			throw new MyException(E_NOAUTH, "need login");
		}

		int perms_;
		public bool hasPerm(int perms)
		{
			perms_ = env.onGetPerms();
			if ((perms_ & perms) != 0)
				return true;
			return false;
		}

		public JsObject objarr2table(JsArray rs, int fixedColCnt=-1)
		{
			var h = new JsArray();
			var d = new JsArray();
			var ret = new JsObject() {
				{"h", h}, {"d", d}
			};
			if (rs.Count == 0)
				return ret;

			JsObject row0 = rs[0] as JsObject;
			h.AddRange(row0.Keys);
			if (fixedColCnt >= 0) {
				/*
				TODO
				foreach (rs as row) {
					h1 = array_keys(row);
					for (i=fixedColCnt; i<count(h1); ++i) {
						if (array_search(h1[i], h) === false) {
							h[] = h1[i];
						}
					}
				}
				*/
			}
			foreach (JsObject row in rs) {
				var arr = new JsArray();
				d.Add(arr);
				foreach (string k in h) {
					arr.Add(row[k]);
				}
			}
			return ret;
		}

	}

}
