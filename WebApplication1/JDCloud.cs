using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Data.Common;
using System.Data;
using System.Collections.Specialized;
using System.Text;

/*
using JsObject = System.Collections.Generic.Dictionary<string, object>;
using JsArray = System.Collections.Generic.List<object>;
using ValueTable = System.Collections.Generic.List<JsArray>;
using ObjectTable = System.Collections.Generic.List<JsObject>;
*/

namespace JDCloud
{
	public class JsObject : Dictionary<string, object>
	{
		public JsObject() { }
	}

	public class JsArray : List<object>
	{
		public JsArray() { }
		public JsArray(IEnumerable<object> collection) : base(collection) { }
	}

	/*
	class ValueTable : List<JsArray>
	{
	}

	class ObjectTable : List<JsObject>
	{
	}
	*/

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

		public static Dictionary<int, string> ERRINFO = new Dictionary<int, string>(){
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

		public HttpContext ctx_;
		public HttpContext ctx
		{
			get
			{
				return ctx_;
			}
			set
			{
				this.ctx_ = value;
				this._GET = ctx.Request.QueryString;
				this._POST = ctx.Request.Form;
				this._REQUEST = ctx.Request.Params;
			}
		}
		public NameValueCollection _GET, _POST, _REQUEST;
		
		private SSTk.DbConn cnn_;
		public SSTk.DbConn cnn
		{
			get {
				dbconn();
				return cnn_;
			}
		}

		public object param(string name, char from = 'a', string defVal = null)
		{
			string val = null;
			if (from == 'a' || from == 'g')
				val = _GET[name];
			if ((val == null && from == 'a') || from == 'p')
				val = _POST[name];
			if (val == null && defVal != null)
				val = defVal;
			return val;
		}

		public object mparam(string name, char from = 'a')
		{
			object val = param(name, from);
			if (val == null)
				throw new MyException(E_PARAM, "require param `" + name + "`");
			return val;
		}
		//public static
		public void dbconn()
		{
			// TODO: from conf.user.php
			if (cnn_ == null)
			{
				cnn_ = new SSTk.DbConn();
				cnn_.Open(SSTk.DbConnType.Odbc, "mytest", "", "");
			}
		}

		public static string Q(string s)
		{
			return "'" + s.Replace("'", "\\'") + "'";
		}

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
				object[] arr = null;
				rd.GetValues(arr);
				ret = new JsArray(arr);
			}
			return ret;
		}

		public JsArray queryAll(string sql, bool assoc)
		{
			DbDataReader rd = cnn.ExecQuery(sql);
			var ret = new JsArray();
			if (rd.HasRows)
			{
				while (true)
				{
					ret.Add(readerToCol(rd, assoc));
					if (rd.Read() == false)
						break;
				}
			}
			rd.Close();
			return ret;
		}

		// can cast to JsObject or JsArray
		public object queryOne(string sql, bool assoc = false)
		{
			DbDataReader rd = cnn.ExecQuery(sql);
			object ret = null;
			if (rd.HasRows)
			{
				ret = readerToCol(rd, assoc);
			}
			rd.Close();
			return ret;
		}
		public object queryScalar(string sql)
		{
			return cnn.ExecScalar(sql);
		}

		public int execOne(string sql, bool getNewId = false)
		{
			int ret = cnn.ExecNonQuery(sql);
			if (getNewId)
			{
				ret = getLastInsertId();
			}
			return ret;
		}

		public int getLastInsertId()
		{
			object ret = cnn.ExecScalar("SELECT LAST_INSERT_ID()");
			return Convert.ToInt32(ret);
		}

		public string htmlEscape(string s)
		{
			return HttpUtility.HtmlEncode(s);
		}

		public void addLog(string s, int level = 0)
		{
		}
		public void logit(string s, string which = "trace")
		{
		}
	}

	public class VcolDef
	{
		public List<string> res;
		public string join;
		public string cond;
		public bool isDefault;
		public string require;
		public bool added;
	}
	public class SubobjDef
	{
		public string sql;
		public bool wantOne;
		public bool isDefault;
	}

	struct SqlConf
	{
		public List<string> cond;
		public List<string> res;
		public List<string> join;
		public string orderby;
		public List<string> gres;
		public Dictionary<string, SubobjDef> subobj;
		public string distinct;
		public string union;
	}
	public class Vcol
	{
		public string def, def0;
		public int vcolDefIdx; // TODO = -1;
		public bool added;
	}

	public class AccessControl : JDApiBase
	{
		public static List<string> stdAc = new List<string>() { "add", "get", "set", "del", "query" };
		protected List<string> allowedAc;
		protected string ac;
		protected string table;

		// 在add后自动设置; 在get/set/del操作调用onValidateId后设置。
		protected object id;

		// for add/set
		protected List<string> readonlyFields;
		// for set
		protected List<string> readonlyFields2;
		// for add/set
		protected List<string> requiredFields;
		// for set
		protected List<string> requiredFields2;
		// for get/query
		protected List<string> hiddenFields;
		// for query
		protected string defaultRes; // 缺省为 "t0.*" 加  default=true的虚拟字段
		protected string defaultSort = "t0.id";
		// for query
		protected int maxPageSz = 100;

		// for get/query
		// virtual columns definition
		protected List<VcolDef> vcolDefs; // elem: {res, join, default?=false}
		protected Dictionary<string, SubobjDef> subobj; // elem: { name => {sql, wantOne, isDefault}}

		// TODO: 回调函数集。在after中执行（在onAfter回调之后）。
		// protected onAfterActions = [];

		// for get/query
		// 注意：sqlConf["res"/"cond"][0]分别是传入的res/cond参数, sqlConf["orderby"]是传入的orderby参数, 为空(注意用isset/is_null判断)均表示未传值。
		private SqlConf sqlConf; // {@cond, @res, @join, orderby, @subobj, @gres}

		// virtual columns
		private Dictionary<string, Vcol> vcolMap; // elem: vcol => {def, def0, added?, vcolDefIdx?=-1}

		public void init(string table, string ac)
		{
			this.table = table;
			this.ac = ac;
		}

		protected virtual void onValidate()
		{
		}
		protected virtual void onValidateId()
		{
		}
		protected virtual void onHandleRow(ref JsObject rowData)
		{
		}
		protected virtual void onAfter(ref object ret)
		{
		}
		protected virtual void onQuery()
		{
		}
		protected virtual int onGenId()
		{
			return 0;
		}

		public virtual void before()
		{
			if (this.allowedAc != null && stdAc.IndexOf(ac) >= 0 && this.allowedAc.IndexOf(ac) < 0)
				throw new MyException(E_FORBIDDEN, string.Format("Operation `{0}` is not allowed on object `{1}`", ac, table));

			if (ac == "get" || ac == "set" || ac == "del") {
				this.onValidateId();
				this.id = (int)mparam("id");
			}

			// TODO: check fields in metadata
			// foreach ($_POST as ($field, $val))

			if (ac == "add" || ac == "set") {
				foreach (var field in this.readonlyFields) {
					if (_POST[field] != null) {
						logit("!!! warn: attempt to chang readonly field `field`");
						_POST.Remove(field);
					}
				}
				if (ac == "set") {
					foreach (var field in this.readonlyFields2) {
						if (_POST[field] != null) {
							logit("!!! warn: attempt to change readonly field `field`");
							ctx.Request.Form.Remove(field);
						}
					}
				}
				if (ac == "add") {
					foreach (var field in this.requiredFields) {
	// 					if (! issetval(field, _POST))
	// 						throw new MyException(E_PARAM, "missing field `{field}`", "参数`{field}`未填写");
						mparam(field, 'p'); // validate field and type; refer to field/type format for mparam.
					}
				}
				else { // for set, the fields can not be set null
					var arr = new List<string>(this.requiredFields);
					arr.AddRange(this.requiredFields2);
					foreach (var field in arr) {
						/* 
						if (is_array(field)) // TODO
							continue;
						*/
						var v = _POST[field];
						if (v != null && (v == "null" || v == "" || v =="empty" )) {
							throw new MyException(E_PARAM, string.Format("{0}.set: cannot set field `field` to null.", field));
						}
					}
				}
				this.onValidate();
			}
			else if (ac == "get" || ac == "query") {
				string gres = param("gres") as string;
				string res = param("res", 'a', this.defaultRes) as string;
				sqlConf = new SqlConf() {
					res = new List<string>{res},
					gres = new List<string> {gres},
					cond = new List<string>{param("cond") as string},
					join = new List<string>(),
					orderby = param("orderby") as string,
					subobj = new Dictionary<string, SubobjDef>(),
					union = param("union") as string,
					distinct = param("distinct") as string
				};

				this.initVColMap();

				/*
				// support internal param res2/join/cond2
				if ((res2 = param("res2")) != null) {
					if (! is_array(res2))
						throw new MyException(E_SERVER, "res2 should be an array: `res2`");
					foreach (res2 as e)
						this.addRes(e);
				}
				if ((join=param("join")) != null) {
					this.addJoin(join);
				}
				if ((cond2 = param("cond2")) != null) {
					if (! is_array(cond2))
						throw new MyException(E_SERVER, "cond2 should be an array: `cond2`");
					foreach (cond2 as e)
						this.addCond(e);
				}
				if ((subobj = param("subobj")) != null) {
					if (! is_array(subobj))
						throw new MyException(E_SERVER, "subobj should be an array");
					this.sqlConf["subobj"] = subobj;
				}
				*/
				this.fixUserQuery();

				this.onQuery();

				// 确保res/gres参数符合安全限定
				if (gres != null) {
					this.filterRes(gres);
				}
				if (res != null) {
					this.filterRes(res, true);
				}
				else {
					this.addDefaultVCols();
					if (this.sqlConf.subobj.Count == 0) {
						foreach (var kv in this.subobj) {
							var col = kv.Key;
							var def = kv.Value;
							if (def.isDefault)
								this.sqlConf.subobj[col] = def;
						}
					}
				}
				if (ac == "query")
				{
					this.supportEasyuiSort();
					if (this.sqlConf.orderby != null && this.sqlConf.union == null)
						this.sqlConf.orderby = this.filterOrderby(this.sqlConf.orderby);
				}
			}
		}

		private void handleRow(ref JsObject rowData)
		{
			foreach (var field in this.hiddenFields) {
				rowData.Remove(field);
			}
			if (rowData.ContainsKey("pwd"))
				rowData["pwd"] = "****";
			// TODO: flag_handleResult(rowData);
			this.onHandleRow(ref rowData);
		}

		// for query. "field1"=>"t0.field1"
		private void fixUserQuery()
		{
			if (this.sqlConf.cond[0] != null) {
				if (this.sqlConf.cond[0].IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0) {
					throw new MyException(E_SERVER, "forbidden SELECT in param cond");
				}
				// "aa = 100 and t1.bb>30 and cc IS null" . "t0.aa = 100 and t1.bb>30 and t0.cc IS null" 
				this.sqlConf.cond[0] = Regex.Replace(this.sqlConf.cond[0], @"/[\w|.]+(?=(\s*[=><]|(\s+(IS|LIKE))))", m => {
					// 't0.0' for col, or 'voldef' for vcol
					var col = m.Value;
					if (col.Contains('.'))
						return col;
					if (this.vcolMap.ContainsKey(col)) {
						this.addVCol(col, false, "-");
						return this.vcolMap[col].def;
					}
					return "t0." + col;
				}, RegexOptions.IgnoreCase);
			}
		}
		private void supportEasyuiSort()
		{
			// support easyui: sort/order
			if (_REQUEST["sort"] != null)
			{
				string orderby = _REQUEST["sort"];
				if (_REQUEST["order"] != null)
					orderby += " " + _REQUEST["order"];
				this.sqlConf.orderby = orderby;
			}
		}
		// return: new field list
		private void filterRes(string res, bool supportFn=false)
		{
			string firstCol = "";
			foreach (var col0 in res.Split(',')) {
				string col = col0.Trim();
				string alias = null;
				string fn = null;
				if (col == "*" || col == "t0.*") {
					firstCol = "t0.*";
					continue;
				}
				Match m;
				// 适用于res/gres, 支持格式："col" / "col col1" / "col as col1"
				if (! (m=Regex.Match(col, @"^\s*(\w+)(?:\s+(?:AS\s+)?(\S+))?\s*", RegexOptions.IgnoreCase)).Success)
				{
					// 对于res, 还支持部分函数: "fn(col) as col1", 目前支持函数: count/sum
					if (supportFn && (m=Regex.Match(col, @"(\w+)\([a-z0-9_.\'*]+\)\s+(?:AS\s+)?(\S+)", RegexOptions.IgnoreCase)).Success) {
						fn = m.Groups[1].Value.ToUpper();
						if (fn != "COUNT" && fn != "SUM")
							throw new MyException(E_FORBIDDEN, string.Format("void not allowed: `{0}`", fn));
					}
					else 
						throw new MyException(E_PARAM, string.Format("bad property `{0}`", col));
				}
				else {
					if (m.Groups[2] != null) {
						col = m.Groups[1].Value;
						alias = m.Groups[2].Value;
					}
				}
				if (alias != null && alias[0] != '"') {
					alias = '"' + alias + '"';
				}
				if (fn != null) {
					this.addRes(col);
					continue;
				}

	// 			if (! ctype_alnum(col))
	// 				throw new MyException(E_PARAM, "bad property `col`");
				if (this.addVCol(col, true, alias) == false) {
					if (this.subobj.ContainsKey(col)) {
						this.sqlConf.subobj[alias != null ? alias: col] = this.subobj[col];
					}
					else {
						col = "t0." + col;
						if (alias != null) {
							col += " AS " + alias;
						}
						this.addRes(col, false);
					}
				}
			}
			this.sqlConf.res[0] = firstCol;
		}

		private string filterOrderby(string orderby)
		{
			var colArr = new List<string>();
			foreach (var col0 in orderby.Split(',')) {
				var col = col0;
				Match m;
				if (! (m=Regex.Match(col, @"^\s*(\w+\.)?(\w+)(\s+(asc|desc))?", RegexOptions.IgnoreCase)).Success)
					throw new MyException(E_PARAM, string.Format("bad property `{0}`", col));
				if (m.Groups[1].Value != null) // e.g. "t0.id desc"
				{
					colArr.Add(col);
					continue;
				}
				col = Regex.Replace(col, @"^\s*(\w+)", m1 => {
					string col1 = m1.Groups[1].Value;
					if (this.addVCol(col1, true, "-") != false)
						return col1;
					return "t0." + col1;
				});
				colArr.Add(col);
			}
			return string.Join(",", colArr);
		}


		private bool afterIsCalled = false;
		public virtual void after(ref object ret)
		{
			// 确保只调用一次
			if (afterIsCalled)
				return;
			afterIsCalled = true;

			if (ac == "get") {
				var ret1 = ret as JsObject;
				this.handleRow(ref ret1);
			}
			else if (ac == "query") {
				var ls = ret as List<object>;
				ls.ForEach(delegate( object rowData) {
					var row = rowData as JsObject;
					this.handleRow(ref row);
				});
			}
			else if (ac == "add") {
				this.id = ret;
			}
			this.onAfter(ref ret);

			/*
			foreach ($this.onAfterActions as $fn)
			{
				# NOTE: php does not allow call $this.onAfterActions();
				$fn();
			}
			*/
		}

		public static string create(string table)
		{
			return "AC_" + table;
		}

		public object api_add()
		{
			var keys = new StringBuilder();
			var values = new StringBuilder();

			var form = ctx.Request.Form;
			foreach (string k in form)
			{
				if (k == "id")
					continue;
				if (form[k] == "")
					continue;
				if (!Regex.IsMatch(k, @"^\w+$"))
					throw new MyException(E_PARAM, "bad key " + k);
				if (keys.Length > 0)
				{
					keys.Append(", ");
					values.Append(", ");
				}
				keys.Append(k);
				string val = htmlEscape(form[k]);
				values.Append(Q(val));
			}
			
			if (keys.Length == 0)
				throw new MyException(E_PARAM, "no field found to be added");

			string sql = string.Format("INSERT INTO {0} ({1}) VALUES ({2})", table, keys, values);

			int id = execOne(sql, true);

			string res = param("res") as string;
			object ret = null;
			if (res != null)
			{
				this.id = id;
				ret = api_get();
			}
			else
				ret = id;
			return ret;
			/*
			//TODO: cache the table format
			SSTk.SSDataTable tbl = cnn.ExecQueryForWrite("SELECT * FROM " + table + " WHERE 1<>1");
			DataRow row = tbl.AddRow();
			NameValueCollection form = ctx.Request.Form;
			foreach (string k in ctx.Request.Form.Keys)
			{
				try
				{
					row[k] = form[k];
				}
				catch (ArgumentException)
				{
					throw new MyException(E_PARAM, "bad field `" + k + "`");
				}
			}
			tbl.Update();
			return getLastInsertId();
			*/
		}

		public void api_set()
		{
			var kv = new StringBuilder();
			var form = ctx.Request.Form;
			foreach (string k in form)
			{
				if (k == "id")
					continue;
				// ignore non-field param
				//if (substr($k,0,2) == "p_")
					//continue;
				// TODO: check meta
				if (!Regex.IsMatch((string)k, @"^\w+$"))
					throw new MyException(E_PARAM, "bad key " + k);

				if (kv.Length > 0)
					kv.Append(", ");
				string v = form[k];
				// 空串或null置空；empty设置空字符串
				if (v == "" || v == "null")
					kv.Append(k + "=null");
				else if (v == "empty")
					kv.Append(k + "=''");
				else
					kv.Append(k + "=" + Q(htmlEscape(v)));
			}
			if (kv.Length == 0) 
			{
				addLog("no field found to be set");
			}
			else {
				string sql = String.Format("UPDATE {0} SET {1} WHERE id={2}", table, kv, id);
				int cnt = execOne(sql);
			}
		}

		public void api_del()
		{
			string sql = string.Format("DELETE FROM {0} WHERE id={1}", table, id);
			int cnt = execOne(sql);
			if (cnt != 1)
				throw new MyException(E_PARAM, string.Format("not found id={0}", id));
		}

		public object api_get()
		{
			/*
			if (! $sqlConf["res"][0] != null)
				$sqlConf["res"][0] = "t0.*";
			else if ($sqlConf["res"][0] == "")
				array_shift($sqlConf["res"]);
			$resSql = join(",", $sqlConf["res"]);
			if ($resSql == "") {
				$resSql = "t0.id";
			}
			*/
			string resSql = "*";
			string sql = string.Format("SELECT {0} FROM {1} WHERE id={2}", resSql, table, id);
			object ret = queryOne(sql, true);
			if (ret == null) 
				throw new MyException(E_PARAM, string.Format("not found `{0}.id`=`{1}`", table, id));
			//handleSubObj($sqlConf["subobj"], $id, $ret);

			return ret;
		}

		public object api_query()
		{
			object pagesz_o = param("_pagesz/i");
			object pagekey_o = param("_pagekey/i");
			bool enableTotalCnt = false;
			bool enablePartialQuery = false;


			// support jquery-easyui
			if (pagesz_o == null && pagekey_o == null) {
				pagesz_o = param("rows/i");
				pagekey_o = param("page/i");
				if (pagekey_o != null)
				{
					enableTotalCnt = true;
					enablePartialQuery = false;
				}
			}
			int pagesz = Convert.ToInt32(pagesz_o);
			if (pagesz == 0)
				pagesz = 20;
			int maxPageSz = Math.Min(this.maxPageSz, PAGE_SZ_LIMIT);
			if (pagesz < 0 || pagesz > maxPageSz)
				pagesz = maxPageSz;

			if (sqlConf.gres != null) {
				enablePartialQuery = false;
			}

			string orderSql = sqlConf.orderby;

			// setup cond for partialQuery
			if (orderSql == null)
				orderSql = defaultSort;

			if (enableTotalCnt == false && pagekey_o != null && (int)pagekey_o == 0)
			{
				enableTotalCnt = true;
			}

			// 如果未指定orderby或只用了id(以后可放宽到唯一性字段), 则可以用partialQuery机制(性能更好更精准), _pagekey表示该字段的最后值；否则_pagekey表示下一页页码。
			string partialQueryCond;
			if (! enablePartialQuery) {
				if (Regex.IsMatch(orderSql, @"^(t0\.)?id\b")) {
					enablePartialQuery = true;
					if (pagekey_o!= null && (int)pagekey_o != 0) {
						if (Regex.IsMatch(orderSql, @"\bid DESC", RegexOptions.IgnoreCase)) {
							partialQueryCond = "t0.id<" + pagekey_o;
						}
						else {
							partialQueryCond = "t0.id>" + pagekey_o;
						}
						// setup res for partialQuery
						if (partialQueryCond != null) {
// 							if (sqlConf["res"][0] != null && !Regex.IsMatch('/\bid\b/',sqlConf["res"][0])) {
// 								array_unshift(sqlConf["res"], "t0.id");
// 							}
							sqlConf.cond.Insert(0, partialQueryCond);
						}
					}
				}
			}
			if (pagekey_o == null)
				pagekey_o = 1;

			if (sqlConf.res[0] == null)
				sqlConf.res[0] = "t0.*";
			else if (sqlConf.res[0] == "")
				sqlConf.res.RemoveAt(0);

			string resSql = string.Join(",", sqlConf.res);
			if (resSql == "") {
				resSql = "t0.id";
			}
			if (sqlConf.distinct != null) {
				resSql = "DISTINCT " + resSql;
			}

			string tblSql = table + " t0";
			if (sqlConf.join.Count > 0)
				tblSql += "\n" + string.Join("\n", sqlConf.join);

			string condSql = "";
			foreach (string cond in sqlConf.cond) {
				if (cond == null)
					continue;
				if (condSql.Length > 0)
					condSql += " AND ";
				if (cond.IndexOf(" and ", StringComparison.OrdinalIgnoreCase) > 0 || cond.IndexOf(" or ", StringComparison.OrdinalIgnoreCase) > 0)
					condSql += "({cond})";
				else 
					condSql += cond;
			}
			StringBuilder sql = new StringBuilder();
			sql.AppendFormat("SELECT {0} FROM {1}", resSql, tblSql);
			if (condSql.Length > 0)
			{
				// TODO: flag_handleCond(condSql);
				sql.Append("\nWHERE condSql");
			}
			if (sqlConf.union != null) {
				sql.Append("\nUNION\n").Append(sqlConf.union);
			}
			if (sqlConf.gres != null) {
				sql.AppendFormat("\nGROUP BY {0}", sqlConf.gres);
			}

			object totalCnt = null;
			if (orderSql != null) {
				sql.AppendFormat("\nORDER BY {0}", orderSql);

				if (enableTotalCnt) {
					string cntSql = "SELECT COUNT(*) FROM tblSql";
					if (condSql != null)
						cntSql += "\nWHERE condSql";
					totalCnt = queryOne(cntSql);
				}

				if (enablePartialQuery) {
					sql.AppendFormat("\nLIMIT {0}", pagesz);
				}
				else {
					sql.AppendFormat("\nLIMIT {0},{1}", ((int)pagekey_o-1)*pagesz, pagesz);
				}
			}

		/*
			if (forGet) {
				ret = queryOne(sql, PDO::FETCH_ASSOC);
				if (ret == false) 
					throw new MyException(E_PARAM, "not found `tbl.id`=`id`");
				handleSubObj(sqlConf["subobj"], id, ret);
			}
		*/
			var retArr = queryAll(sql.ToString(), true);
			object reto = retArr;

			// Note: colCnt may be changed in after().
			int fixedColCnt = retArr.Count()==0? 0: (retArr[0] as JsArray).Count();
			this.after(ref reto);

			object nextkey = null;
			if (pagesz == retArr.Count) { // 还有下一页数据, 添加nextkey
				if (enablePartialQuery) {
nextkey = (retArr.Last() as JsObject)["id"];
				}
				else {
					nextkey = (int)pagekey_o + 1;
				}
			}
			//TODO: ret = objarr2table(ret, fixedColCnt);
			foreach (var mainObj in retArr) {
				var id1 = (mainObj as JsObject)["id"];
				/* TODO
				if (id1 != null)
					handleSubObj(sqlConf.subobj, id1, mainObj);
				*/
			}
			string fmt = param("_fmt") as string;
			JsObject ret = null;
			if ((string)fmt == "list") {
				ret = new JsObject() { { "list", ret } };
			}
			else {
				//TODO
				//ret = objarr2table(ret, fixedColCnt);
			}
			if (nextkey != null) {
				ret["nextkey"] = nextkey;
			}
			if (totalCnt != null) {
				ret["total"] = totalCnt;
			}
			/* TODO
			if (fmt != null)
				handleExportFormat($fmt, $ret, $tbl);
			*/

			return ret;
		}

		public void addRes(string res, bool analyzeCol=true)
		{
			this.sqlConf.res.Add(res);
			if (analyzeCol)
				this.setColFromRes(res, true);
		}

	/**
	@fn AccessControl::addCond(cond, prepend=false)

	@param prepend 为true时将条件排到前面。

	调用多次addCond时，多个条件会依次用"AND"连接起来。

	添加查询条件。
	示例：假如设计有接口：

		Ordr.query(q?) . tbl(..., payTm?)
		参数：
		q:: 查询条件，值为"paid"时，查询10天内已付款的订单。且结果会多返回payTm/付款时间字段。

	实现时，在onQuery中检查参数"q"并定制查询条件：

		protected void onQuery()
		{
			// 限制只能看用户自己的订单
			uid = _SESSION["uid"];
			this.addCond("t0.userId=uid");

			q = param("q");
			if (isset(q) && q == "paid") {
				validDate = date("Y-m-d", strtotime("-9 day"));
				this.addRes("olpay.tm payTm");
				this.addJoin("INNER JOIN OrderLog olpay ON olpay.orderId=t0.id");
				this.addCond("olpay.action='PA' AND olpay.tm>'validDate'");
			}
		}

	@see AccessControl::addRes
	@see AccessControl::addJoin
	 */
		public void addCond(string cond, bool prepend=false)
		{
			if (prepend)
				this.sqlConf.cond.Insert(0, cond);
			else
				this.sqlConf.cond.Add(cond);
		}

		/**
	@fn AccessControl::addJoin(joinCond)

	添加Join条件.

	@see AccessControl::addCond 其中有示例
		 */
		public void addJoin(string join)
		{
			this.sqlConf.join.Add(join);
		}

		private void setColFromRes(string res, bool added, int vcolDefIdx=-1)
		{
			Match m = null;
			string colName, def;
			if ( (m=Regex.Match(res, @"^(\w+)\.(\w+)")).Success) {
				colName = m.Groups[2].Value;
				def = res;
			}
			else if ( (m = Regex.Match(res, @"^(.*?)\s+(?:as\s+)?(\w+)\s*", RegexOptions.IgnoreCase | RegexOptions.Singleline)).Success) {
				colName = m.Groups[2].Value;
				def = m.Groups[1].Value;
			}
			else
				throw new MyException(E_SERVER, "bad res definition: `res`");

			if (this.vcolMap.ContainsKey(colName)) {
				if (added && this.vcolMap[colName].added)
					throw new MyException(E_SERVER, "res for col `colName` has added: `res`");
				this.vcolMap[ colName ].added = true;
			}
			else {
				this.vcolMap[ colName ] = new Vcol() {
					def=def, def0=res, added=added, vcolDefIdx=vcolDefIdx
				};
			}
		}

		private void initVColMap()
		{
			if (this.vcolMap == null) {
				this.vcolMap = new Dictionary<string,Vcol>();
				int idx = 0;
				foreach (var vcolDef in this.vcolDefs) {
					foreach (var e in vcolDef.res) {
						this.setColFromRes(e, false, idx);
					}
					++ idx;
				}
			}
		}

	/**
	@fn AccessControl::addVCol(col, ignoreError=false, alias=null)

	@param col 必须是一个英文词, 不允许"col as col1"形式; 该列必须在 vcolDefs 中已定义.
	@param alias 列的别名。可以中文. 特殊字符"-"表示不加到最终res中(只添加join/cond等定义), 由addVColDef内部调用时使用.
	@return Boolean T/F

	用于AccessControl子类添加已在vcolDefs中定义的vcol. 一般应先考虑调用addRes(col)函数.

	@see AccessControl::addRes
	 */
		protected bool addVCol(string col, bool ignoreError = false, string alias = null)
		{
			if (! this.vcolMap.ContainsKey(col)) {
				if (!ignoreError)
					throw new MyException(E_SERVER, "unknown vcol `col`");
				return false;
			}
			if (this.vcolMap[col].added)
				return true;
			this.addVColDef(this.vcolMap[col].vcolDefIdx, true);
			if (alias != null) {
				if (alias != "-")
					this.addRes(this.vcolMap[col].def + " AS " + alias, false);
			}
			else {
				this.addRes(this.vcolMap[col].def0, false);
			}
			return true;
		}

		private void addDefaultVCols()
		{
			int idx = 0;
			foreach (var vcolDef in this.vcolDefs) {
				if (vcolDef.isDefault) {
					this.addVColDef(idx);
				}
				++ idx;
			}
		}

		private void addVColDef(int idx, bool dontAddRes = false)
		{
			if (idx < 0 || this.vcolDefs[idx].added)
				return;

			var vcolDef = this.vcolDefs[idx];
			vcolDef.added = true;
			if (! dontAddRes) {
				foreach (var e in vcolDef.res) {
					this.addRes(e);
				}
			}
			if (vcolDef.require != null)
			{
				var requireCol = vcolDef.require;
				this.addVCol(requireCol, false, "-");
			}
			if (vcolDef.join != null)
				this.addJoin(vcolDef.join);
			if (vcolDef.cond != null)
				this.addCond(vcolDef.cond);
		}

	}

	/// <summary>
	/// Summary description for Handler1
	/// </summary>
	public class JDHandler : JDApiBase, IHttpHandler
	{
		public void ProcessRequest(HttpContext context)
		{
			var ret = new List<object>() {0, null};
			try
			{
				string path = context.Request.Path;
				Match m = Regex.Match(path, @"api/((\w+)(?:\.(\w+))?)$");
				//Match m = Regex.Match(path, @"api/(\w+)");
				if (! m.Success)
					throw new MyException(E_PARAM, "bad ac");
				string ac = m.Groups[1].Value;
				string ac1 = null;
				string table = null;
				string clsName = null;
				string methodName = null;
				if (m.Groups[3].Length > 0)
				{
					table = m.Groups[2].Value;
					ac1 = m.Groups[3].Value;
					clsName = AccessControl.create(table);
					methodName = "api_" + ac1;
				}
				else
				{
					clsName = "Global";
					methodName = "api_" + m.Groups[2].Value;
				}

				JDApiBase obj = null;
				Assembly asm = Assembly.GetExecutingAssembly();
				obj = asm.CreateInstance("JDApi." + clsName) as JDApiBase;
				if (obj == null)
					throw new MyException(E_PARAM, "bad ac=`" + ac + "` (no class)");
				Type t = obj.GetType();
				MethodInfo mi = t.GetMethod(methodName);
				if (mi == null)
					throw new MyException(E_PARAM, "bad ac=`" + ac + "` (no method)");
				obj.ctx = context;
				if (clsName == "Global")
				{
					ret[1] = mi.Invoke(obj, null);
				}
				else if (t.IsSubclassOf(typeof(AccessControl)))
				{
					AccessControl accessCtl = obj as AccessControl;
					accessCtl.init(table, ac1);
					accessCtl.before();
					object rv = mi.Invoke(obj, null);
					//ret[1] = t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, obj, null);
					accessCtl.after(ref rv);
					ret[1] = rv;
				}
				else
				{
					throw new MyException(E_SERVER, "misconfigured ac=`" + ac + "`");
				}
				if (ret[1] == null)
					ret[1] = "OK";
			}
			catch(MyException ex)
			{
				ret[0] = ex.Code;
				ret[1] = ex.Message;
				ret.Add(ex.DebugInfo);
			}
			catch (Exception ex)
			{
				if (ex.InnerException is MyException)
				{
					MyException ex1 = ex.InnerException as MyException;
					ret[0] = ex1.Code;
					ret[1] = ex1.Message;
					ret.Add(ex1.DebugInfo);
				}
				else
				{
					ret[0] = E_SERVER;
					ret[1] = GetErrInfo(E_SERVER);
					ret.Add(ex.Message);
				}
			}

			var ser = new JavaScriptSerializer();
			//ser.Serialize(ser);
			context.Response.ContentType = "text/plain";
			context.Response.Write(ser.Serialize(ret));
		}

		public bool IsReusable
		{
			get
			{
				return false;
			}
		}
	}
}
