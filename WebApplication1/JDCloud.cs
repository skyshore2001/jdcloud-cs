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

namespace JDCloud
{
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

		public HttpContext ctx;
		
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
				val = ctx.Request.QueryString[name];
			if ((val == null && from == 'a') || from == 'p')
				val = ctx.Request.Form[name];
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
				var dict = new Dictionary<string, object>();
				for (int i = 0; i < rd.FieldCount; ++i)
				{
					dict.Add(rd.GetName(i), rd.GetValue(i));
				}
				ret = dict;
			}
			else
			{
				object[] arr = null;
				rd.GetValues(arr);
				ret = arr;
			}
			return ret;
		}

		public object queryAll(string sql, bool assoc)
		{
			DbDataReader rd = cnn.ExecQuery(sql);
			var ret = new List<object>();
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
	}

	public class AccessControl : JDApiBase
	{
		public static List<string> stdAc = new List<string>() { "add", "get", "set", "del", "query" };
		protected virtual List<string> allowedAc { get; set; }
		protected string ac;
		protected string table;

		protected object id;

		public void init(string table, string ac)
		{
			this.table = table;
			this.ac = ac;
		}
		public virtual void before()
		{
			if (this.allowedAc != null && stdAc.IndexOf(ac) >= 0 && this.allowedAc.IndexOf(ac) < 0)
				throw new MyException(E_FORBIDDEN, "forbidden ac=`" + ac + "`");

			if (ac == "get" || ac == "set" || ac == "del") {
				this.onValidateId();
				this.id = mparam("id");
			}
			if (ac == "add" || ac == "set") {
				/*
				foreach ($this->readonlyFields as $field) {
					if (array_key_exists($field, $_POST)) {
						logit("!!! warn: attempt to change readonly field `$field`");
						unset($_POST[$field]);
					}
				}
				if ($ac == "set") {
					foreach ($this->readonlyFields2 as $field) {
						if (array_key_exists($field, $_POST)) {
							logit("!!! warn: attempt to change readonly field `$field`");
							unset($_POST[$field]);
						}
					}
				}
				if ($ac == "add") {
					foreach ($this->requiredFields as $field) {
	// 					if (! issetval($field, $_POST))
	// 						throw new MyException(E_PARAM, "missing field `{$field}`", "参数`{$field}`未填写");
						mparam($field, $_POST); // validate field and type; refer to field/type format for mparam.
					}
				}
				else { # for set, the fields can not be set null
					$fs = array_merge($this->requiredFields, $this->requiredFields2);
					foreach ($fs as $field) {
						if (is_array($field)) // TODO
							continue;
						if (array_key_exists($field, $_POST) && ( ($v=$_POST[$field]) == "null" || $v == "" || $v=="empty" )) {
							throw new MyException(E_PARAM, "{$this->table}.set: cannot set field `$field` to null.", "字段`$field`不允许置空");
						}
					}
				}
				*/
				this.onValidate();
			}
			
		}

		private void handleRow(ref object rowData)
		{
			/*
			foreach ($this->hiddenFields as $field) {
				unset($rowData[$field]);
			}
			if (isset($rowData["pwd"]))
				$rowData["pwd"] = "****";
			flag_handleResult($rowData);
			*/
			this.onHandleRow(ref rowData);
		}
		
		public virtual void after(ref object ret)
		{
			if (ac == "get") {
				this.handleRow(ref ret);
			}
			else if (ac == "query") {
				var ls = ret as List<object>;
				ls.ForEach(delegate( object rowData) { this.handleRow(ref rowData); });
			}
			else if (ac == "add") {
				this.id = ret;
			}
			this.onAfter(ref ret);

			/*
			foreach ($this->onAfterActions as $fn)
			{
				# NOTE: php does not allow call $this->onAfterActions();
				$fn();
			}
			*/
		}

		public static string create(string table)
		{
			return "AC_" + table;
		}

		protected virtual void onValidateId()
		{
		}
		protected virtual void onValidate()
		{
		}
		protected virtual void onHandleRow(ref object rowData)
		{
		}
		protected virtual void onAfter(ref object ret)
		{
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

			object res = param("res");
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
			object id = mparam("id", 'g');
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
			object id = mparam("id");
			string sql = string.Format("DELETE FROM {0} WHERE id={1}", table, id);
			int cnt = execOne(sql);
			if (cnt != 1)
				throw new MyException(E_PARAM, string.Format("not found id={0}", id));
		}

		public object api_get()
		{
			/*
			if (! isset($sqlConf["res"][0]))
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
		/*
			//object wantArray = param("wantArray/b");
			//$sqlConf = $accessCtl->sqlConf;
			
			bool enablePaging = !forGet;
			if (forGet)
			{
				id = mparam("id");
				array_unshift($sqlConf["cond"], "t0.id=$id");
			}
			else {
				$pagesz = param("_pagesz/i");
				$pagekey = param("_pagekey/i");
				// support jquery-easyui
				if (!isset($pagesz) && !isset($pagekey)) {
					$pagesz = param("rows/i");
					$pagekey = param("page/i");
					if (isset($pagekey))
					{
						$enableTotalCnt = true;
						$enablePartialQuery = false;
					}
				}
				if ($pagesz == 0)
					$pagesz = 20;

				$maxPageSz = min($accessCtl->getMaxPageSz(), PAGE_SZ_LIMIT);
				if ($pagesz < 0 || $pagesz > $maxPageSz)
					$pagesz = $maxPageSz;

				if (isset($sqlConf["gres"])) {
					$enablePartialQuery = false;
				}
			}

			$orderSql = $sqlConf["orderby"];

			// setup cond for partialQuery
			if ($enablePaging) {
				if ($orderSql == null)
					$orderSql = $accessCtl->getDefaultSort();

				if (!isset($enableTotalCnt))
				{
					$enableTotalCnt = false;
					if ($pagekey == 0)
						$enableTotalCnt = true;
				}

				// 如果未指定orderby或只用了id(以后可放宽到唯一性字段), 则可以用partialQuery机制(性能更好更精准), _pagekey表示该字段的最后值；否则_pagekey表示下一页页码。
				if (!isset($enablePartialQuery)) {
					$enablePartialQuery = false;
					if (preg_match('/^(t0\.)?id\b/', $orderSql)) {
						$enablePartialQuery = true;
						if ($pagekey) {
							if (preg_match('/\bid DESC/i', $orderSql)) {
								$partialQueryCond = "t0.id<$pagekey";
							}
							else {
								$partialQueryCond = "t0.id>$pagekey";
							}
							// setup res for partialQuery
							if ($partialQueryCond) {
	// 							if (isset($sqlConf["res"][0]) && !preg_match('/\bid\b/',$sqlConf["res"][0])) {
	// 								array_unshift($sqlConf["res"], "t0.id");
	// 							}
								array_unshift($sqlConf["cond"], $partialQueryCond);
							}
						}
					}
				}
				if (! $pagekey)
					$pagekey = 1;
			}

			if (! isset($sqlConf["res"][0]))
				$sqlConf["res"][0] = "t0.*";
			else if ($sqlConf["res"][0] == "")
				array_shift($sqlConf["res"]);
			$resSql = join(",", $sqlConf["res"]);
			if ($resSql == "") {
				$resSql = "t0.id";
			}
			if (@$sqlConf["distinct"]) {
				$resSql = "DISTINCT {$resSql}";
			}

			$tblSql = "$tbl t0";
			if (count($sqlConf["join"]) > 0)
				$tblSql .= "\n" . join("\n", $sqlConf["join"]);
			$condSql = "";
			foreach ($sqlConf["cond"] as $cond) {
				if ($cond == null)
					continue;
				if (strlen($condSql) > 0)
					$condSql .= " AND ";
				if (stripos($cond, " and ") !== false || stripos($cond, " or ") !== false)
					$condSql .= "({$cond})";
				else 
					$condSql .= $cond;
			}
			$sql = "SELECT $resSql FROM $tblSql";
			if ($condSql)
			{
				flag_handleCond($condSql);
				$sql .= "\nWHERE $condSql";
			}
			if (isset($sqlConf["union"])) {
				$sql .= "\nUNION\n" . $sqlConf["union"];
			}
			if ($sqlConf["gres"]) {
				$sql .= "\nGROUP BY {$sqlConf['gres']}";
			}

			if ($orderSql)
				$sql .= "\nORDER BY " . $orderSql;

			if ($enablePaging) {
				if ($enableTotalCnt) {
					$cntSql = "SELECT COUNT(*) FROM $tblSql";
					if ($condSql)
						$cntSql .= "\nWHERE $condSql";
					$totalCnt = queryOne($cntSql);
				}

				if ($enablePartialQuery) {
					$sql .= "\nLIMIT " . $pagesz;
				}
				else {
					$sql .= "\nLIMIT " . ($pagekey-1)*$pagesz . "," . $pagesz;
				}
			}
			else {
				if ($pagesz) {
					$sql .= "\nLIMIT " . $pagesz;
				}
			}

			if ($forGet) {
				$ret = queryOne($sql, PDO::FETCH_ASSOC);
				if ($ret == false) 
					throw new MyException(E_PARAM, "not found `$tbl.id`=`$id`");
				handleSubObj($sqlConf["subobj"], $id, $ret);
			}
			else {
				$ret = queryAll($sql, PDO::FETCH_ASSOC);
				if ($ret == false)
					$ret = [];

				if ($wantArray) {
					foreach ($ret as &$mainObj) {
						$id1 = $mainObj["id"];
						handleSubObj($sqlConf["subobj"], $id1, $mainObj);
					}
				}
				else {
					// Note: colCnt may be changed in after().
					$fixedColCnt = count($ret)==0? 0: count($ret[0]);
					$accessCtl->after($ret);
					$ignoreAfter = true;

					if ($enablePaging && $pagesz == count($ret)) { // 还有下一页数据, 添加nextkey
						if ($enablePartialQuery) {
							$nextkey = $ret[count($ret)-1]["id"];
						}
						else {
							$nextkey = $pagekey + 1;
						}
					}
					$ret = objarr2table($ret, $fixedColCnt);
					if (isset($nextkey)) {
						$ret["nextkey"] = $nextkey;
					}
					if (isset($totalCnt)) {
						$ret["total"] = $totalCnt;
					}

					handleFormat($ret, $tbl);
				}
			}
			*/

			return new Dictionary<string, object>();
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
			ser.Serialize(ser);
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
