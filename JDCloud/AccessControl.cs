﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JDCloud
{
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

	class SqlConf
	{
		public List<string> cond;
		public List<string> res;
		public List<string> join;
		public string orderby;
		public string gres;
		public Dictionary<string, SubobjDef> subobj;
		public bool distinct;
		public string union;
	}
	class Vcol
	{
		public string def, def0;
		public int vcolDefIdx = -1;
		public bool added;
	}

	public class AccessControl : JDApiBase
	{
		public static readonly List<string> stdAc = new List<string>() { "add", "get", "set", "del", "query" };
		protected List<string> allowedAc;
		protected string ac;
		protected string table;

		// 在add后自动设置; 在get/set/del操作调用onValidateId后设置。
		protected int id;

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
		// 注意：sqlConf.res/.cond[0]分别是传入的res/cond参数, sqlConf.orderby是传入的orderby参数, 为空均表示未传值。
		private SqlConf sqlConf; // {@cond, @res, @join, orderby, @subobj, @gres}

		// virtual columns
		private Dictionary<string, Vcol> vcolMap; // elem: vcol => {def, def0, added?, vcolDefIdx?=-1}

		public void init(string table, string ac)
		{
			this.table = table;
			this.ac = ac;
			this.onInit();
		}

		protected virtual void onInit()
		{
		}
		protected virtual void onValidate()
		{
		}
		protected virtual void onValidateId()
		{
		}
		protected virtual void onHandleRow(JsObject rowData)
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

		public void before()
		{
			if (this.allowedAc != null && stdAc.Contains(ac) && !this.allowedAc.Contains(ac))
				throw new MyException(E_FORBIDDEN, string.Format("Operation `{0}` is not allowed on object `{1}`", ac, table));

			if (ac == "get" || ac == "set" || ac == "del") {
				this.onValidateId();
				this.id = (int)mparam("id");
			}

			// TODO: check fields in metadata
			// foreach ($_POST as ($field, $val))

			if (ac == "add" || ac == "set") 
			{
				if (this.readonlyFields != null)
				{
					foreach (var field in this.readonlyFields)
					{
						if (_POST[field] != null && !(ac == "add" && this.requiredFields.Contains(field)))
						{
							logit("!!! warn: attempt to chang readonly field `field`");
							_POST.Remove(field);
						}
					}
				}
				if (ac == "set") {
					if (this.readonlyFields2 != null)
					{
						foreach (var field in this.readonlyFields2)
						{
							if (_POST[field] != null)
							{
								logit("!!! warn: attempt to change readonly field `field`");
								_POST.Remove(field);
							}
						}
					}
				}
				if (ac == "add") {
					if (this.requiredFields != null)
					{
						foreach (var field in this.requiredFields)
						{
							// 					if (! issetval(field, _POST))
							// 						throw new MyException(E_PARAM, "missing field `{field}`", "参数`{field}`未填写");
							mparam(field, "P"); // validate field and type; refer to field/type format for mparam.
						}
					}
				}
				else { // for set, the fields can not be set null
					var arr = new List<string>();
					if (this.requiredFields != null)
						arr.AddRange(this.requiredFields);
					if (this.requiredFields2 != null)
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
				string gres = param("gres", null, null, false) as string;
				string res = param("res", null, this.defaultRes, false) as string;
				sqlConf = new SqlConf() {
					res = new List<string>{res},
					gres = gres,
					cond = new List<string>{param("cond", null, null, false) as string},
					join = new List<string>(),
					orderby = param("orderby") as string,
					subobj = new Dictionary<string, SubobjDef>(),
					union = param("union") as string,
					distinct = (bool)param("distinct/b", false)
				};

				this.initVColMap();

				/* TODO
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
					if (this.sqlConf.subobj.Count == 0 && this.subobj != null) {
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

		private void handleRow(JsObject rowData)
		{
			if (this.hiddenFields != null)
			{
				foreach (var field in this.hiddenFields)
				{
					rowData.Remove(field);
				}
			}
			if (rowData.ContainsKey("pwd"))
				rowData["pwd"] = "****";
			// TODO: flag_handleResult(rowData);
			this.onHandleRow(rowData);
		}

		// for query. "field1"=>"t0.field1"
		private void fixUserQuery()
		{
			if (this.sqlConf.cond[0] != null) {
				if (this.sqlConf.cond[0].IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0) {
					throw new MyException(E_FORBIDDEN, "forbidden SELECT in param cond");
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
				if (! (m=Regex.Match(col, @"^(\w+)(?:\s+(?:AS\s+)?(\S+))?$", RegexOptions.IgnoreCase)).Success)
				{
					// 对于res, 还支持部分函数: "fn(col) as col1", 目前支持函数: count/sum
					if (supportFn && (m=Regex.Match(col, @"^(\w+)\([a-z0-9_.\'*]+\)\s+(?:AS\s+)?(\S+)$", RegexOptions.IgnoreCase)).Success) {
						fn = m.Groups[1].Value.ToUpper();
						if (fn != "COUNT" && fn != "SUM")
							throw new MyException(E_FORBIDDEN, string.Format("SQL function not allowed: `{0}`", fn));
					}
					else 
						throw new MyException(E_PARAM, string.Format("bad property `{0}`", col));
				}
				else {
					if (m.Groups[2].Length > 0) {
						col = m.Groups[1].Value;
						alias = m.Groups[2].Value;
					}
				}
				// alias可以用引号，用于支持中文
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
					if (this.subobj != null && this.subobj.ContainsKey(col)) {
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
				var col = col0.Trim();
				Match m;
				if (! (m=Regex.Match(col, @"^(\w+\.)?(\w+)(\s+(asc|desc))?$", RegexOptions.IgnoreCase)).Success)
					throw new MyException(E_PARAM, string.Format("bad property `{0}`", col));
				if (m.Groups[1].Value.Length > 0) // e.g. "t0.id desc"
				{
					colArr.Add(col);
					continue;
				}
				col = Regex.Replace(col, @"^(\w+)", m1 => {
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
		public void after(ref object ret)
		{
			// 确保只调用一次
			if (afterIsCalled)
				return;
			afterIsCalled = true;

			if (ac == "get") {
				var ret1 = ret as JsObject;
				this.handleRow(ret1);
			}
			else if (ac == "query") {
				var ls = ret as JsArray;
				ls.ForEach(rowData => {
					var row = rowData as JsObject;
					this.handleRow(row);
				});
			}
			/*
			else if (ac == "add") {
			}
			*/
			this.onAfter(ref ret);

			/* TODO
			foreach ($this.onAfterActions as $fn)
			{
				# NOTE: php does not allow call $this.onAfterActions();
				$fn();
			}
			*/
		}

		public virtual object api_add()
		{
			var keys = new StringBuilder();
			var values = new StringBuilder();

			foreach (string k in _POST)
			{
				if (k == "id")
					continue;
				var val = _POST[k];
				if (val.Length == 0)
					continue;
				if (!Regex.IsMatch(k, @"^\w+$"))
					throw new MyException(E_PARAM, string.Format("bad property `{0}`" + k));
				if (keys.Length > 0)
				{
					keys.Append(", ");
					values.Append(", ");
				}
				keys.Append(k);
				val = htmlEscape(val);
				values.Append(Q(val));
			}
			
			if (keys.Length == 0)
				throw new MyException(E_PARAM, "no field found to be added");

			string sql = string.Format("INSERT INTO {0} ({1}) VALUES ({2})", table, keys, values);
			this.id = execOne(sql, true);

			string res = param("res") as string;
			object ret = null;
			if (res != null)
			{
				ret = api_get();
			}
			else
				ret = this.id;
			return ret;
		}

		public virtual void api_set()
		{
			var kv = new StringBuilder();
			foreach (string k in _POST)
			{
				if (k == "id")
					continue;
				// ignore non-field param
				//if (substr($k,0,2) == "p_")
					//continue;
				// TODO: check meta
				if (!Regex.IsMatch(k, @"^\w+$"))
					throw new MyException(E_PARAM, string.Format("bad property `{0}`" + k));

				if (kv.Length > 0)
					kv.Append(", ");
				// 空串或null置空；empty设置空字符串
				var val = _POST[k];
				if (val == "" || val == "null")
					kv.Append(k + "=null");
				else if (val == "empty")
					kv.Append(k + "=''");
				else
					kv.Append(k + "=" + Q(htmlEscape(val)));
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

		public virtual void api_del()
		{
			string sql = string.Format("DELETE FROM {0} WHERE id={1}", table, id);
			int cnt = execOne(sql);
			if (cnt != 1)
				throw new MyException(E_PARAM, string.Format("not found id={0}", id));
		}

		protected StringBuilder genQuerySql()
		{
			string a, b;
			return genQuerySql(out a, out b);
		}
		protected StringBuilder genQuerySql(out string tblSql, out string condSql)
		{
			if (sqlConf.res[0] == null)
				sqlConf.res[0] = "t0.*";
			else if (sqlConf.res[0] == "")
				sqlConf.res.RemoveAt(0);

			string resSql = string.Join(",", sqlConf.res);
			if (resSql == "") {
				resSql = "t0.id";
			}
			if (sqlConf.distinct) {
				resSql = "DISTINCT " + resSql;
			}

			tblSql = table + " t0";
			if (sqlConf.join.Count > 0)
				tblSql += "\n" + string.Join("\n", sqlConf.join);

			var condBuilder = new StringBuilder();
			foreach (string cond in sqlConf.cond) {
				if (cond == null)
					continue;
				if (condBuilder.Length > 0)
					condBuilder.Append(" AND ");
				if (cond.IndexOf(" and ", StringComparison.OrdinalIgnoreCase) > 0 || cond.IndexOf(" or ", StringComparison.OrdinalIgnoreCase) > 0)
					condBuilder.AppendFormat("({0})", cond);
				else 
					condBuilder.Append(cond);
			}
			condSql = condBuilder.ToString();
			StringBuilder sql = new StringBuilder();
			sql.AppendFormat("SELECT {0} FROM {1}", resSql, tblSql);
			if (condBuilder.Length > 0)
			{
				// TODO: flag_handleCond(condSql);
				sql.AppendFormat("\nWHERE {0}", condBuilder);
			}
			return sql;
		}

		// return: JsObject
		public virtual object api_get()
		{
			this.addCond("t0.id=" + this.id, true);
			StringBuilder sql = genQuerySql();
			object ret = queryOne(sql.ToString(), true);
			if (ret.Equals(false))
				throw new MyException(E_PARAM, string.Format("not found `{0}.id`=`{1}`", table, id));
			//TODO
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
			int maxPageSz = Math.Min(this.maxPageSz, PAGE_SZ_LIMIT);
			if (pagesz < 0 || pagesz > maxPageSz)
				pagesz = maxPageSz;
			else if (pagesz == 0)
				pagesz = 20;

			int pagekey = Convert.ToInt32(pagekey_o);

			if (sqlConf.gres != null) {
				enablePartialQuery = false;
			}

			string orderSql = sqlConf.orderby;

			// setup cond for partialQuery
			if (orderSql == null)
				orderSql = defaultSort;

			if (enableTotalCnt == false && pagekey_o != null && pagekey == 0)
			{
				enableTotalCnt = true;
			}

			// 如果未指定orderby或只用了id(以后可放宽到唯一性字段), 则可以用partialQuery机制(性能更好更精准), _pagekey表示该字段的最后值；否则_pagekey表示下一页页码。
			string partialQueryCond;
			if (! enablePartialQuery) {
				if (Regex.IsMatch(orderSql, @"^(t0\.)?id\b")) {
					enablePartialQuery = true;
					if (pagekey_o!= null && pagekey != 0) {
						if (Regex.IsMatch(orderSql, @"\bid DESC", RegexOptions.IgnoreCase)) {
							partialQueryCond = "t0.id<" + pagekey;
						}
						else {
							partialQueryCond = "t0.id>" + pagekey;
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

			string tblSql, condSql;
			StringBuilder sql = genQuerySql(out tblSql, out condSql);
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
					string cntSql = "SELECT COUNT(*) FROM " + tblSql;
					if (condSql.Length > 0)
						cntSql += "\nWHERE " + condSql;
					totalCnt = queryScalar(cntSql);
				}

				if (enablePartialQuery) {
					sql.AppendFormat("\nLIMIT {0}", pagesz);
				}
				else {
					if (pagekey == 0)
						pagekey = 1;
					sql.AppendFormat("\nLIMIT {0},{1}", (pagekey-1)*pagesz, pagesz);
				}
			}

			var objArr = queryAll(sql.ToString(), true);

			// Note: colCnt may be changed in after().
			int fixedColCnt = objArr.Count()==0? 0: (objArr[0] as JsObject).Count();
			object reto = objArr;
			this.after(ref reto);

			object nextkey = null;
			if (pagesz == objArr.Count) { // 还有下一页数据, 添加nextkey
				// TODO: res参数中没有指定id时?
				if (enablePartialQuery) {
					nextkey = (objArr.Last() as JsObject)["id"];
				}
				else {
					nextkey = pagekey + 1;
				}
			}
			foreach (var mainObj in objArr) {
				/* TODO
				object id1;
				if ((mainObj as JsObject).TryGetValue("id", out id1))
				{
				}
				if (id1 != null)
					handleSubObj(sqlConf.subobj, id1, mainObj);
				*/
			}
			string fmt = param("_fmt") as string;
			JsObject ret = null;
			if (fmt == "list") {
				ret = new JsObject() { { "list", objArr } };
			}
			else {
				ret = objarr2table(objArr, fixedColCnt);
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
			else if ( (m = Regex.Match(res, @"^(.*?)\s+(?:as\s+)?""?(\w+)""?\s*", RegexOptions.IgnoreCase | RegexOptions.Singleline)).Success) {
				colName = m.Groups[2].Value;
				def = m.Groups[1].Value;
			}
			else
				throw new MyException(E_PARAM, string.Format("bad res definition: `{0}`", res));

			if (this.vcolMap.ContainsKey(colName)) {
				if (added && this.vcolMap[colName].added)
					throw new MyException(E_SERVER, string.Format("res for col `{0}` has added: `{1}`", colName, res));
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
			if (this.vcolMap == null)
				this.vcolMap = new Dictionary<string,Vcol>();
			if (this.vcolDefs == null)
				return;

			int idx = 0;
			foreach (var vcolDef in this.vcolDefs) {
				foreach (var e in vcolDef.res) {
					this.setColFromRes(e, false, idx);
				}
				++ idx;
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
					throw new MyException(E_SERVER, string.Format("unknown vcol `{0}`", col));
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
			if (this.vcolDefs == null)
				return;
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
}
