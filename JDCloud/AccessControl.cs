using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;

namespace JDCloud
{
	public class VcolDef
	{
		public List<string> res;
		public string join;
		public string cond;
		public bool isDefault;
		// 依赖另一列
		public string require;
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
		public string gres, gcond;
		public Dictionary<string, SubobjDef> subobj;
		public bool distinct;
		public string union;
	}
	class Vcol
	{
		// def0包含alias, def不包含alias
		public string def, def0;
		// 指向vcolDef中的index
		public int vcolDefIdx = -1;
		// 已应用到最终查询中
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
		protected string defaultRes = "t0.*"; // 缺省为 "t0.*" 加  default=true的虚拟字段
		protected string defaultSort = "t0.id";
		// for query
		protected int maxPageSz = 100;

		// for get/query
		// virtual columns definition
		protected List<VcolDef> vcolDefs; // elem: {res, join, default?=false}
		protected Dictionary<string, SubobjDef> subobj; // elem: { name => {sql, wantOne, isDefault}}

		// 回调函数集。在after中执行（在onAfter回调之后）。
		public delegate void OnAfterActions();
		protected OnAfterActions onAfterActions;

		// for get/query
		// 注意：sqlConf.res/.cond[0]分别是传入的res/cond参数, sqlConf.orderby是传入的orderby参数, 为空均表示未传值。
		private SqlConf sqlConf; // {@cond, @res, @join, orderby, @subobj, @gres}
		private bool isAggregationQuery; // 是聚合查询，如带group by或res中有聚合函数

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

		protected void initQuery()
		{
			string gres = param("gres", null, null, false) as string;
			string res = param("res", null, null, false) as string;
			sqlConf = new SqlConf() {
				res = new List<string>{},
				gres = gres,
				gcond = param("gcond", null, null, false) as string,
				cond = new List<string>{param("cond", null, null, false) as string},
				join = new List<string>(),
				orderby = param("orderby", null, null, false) as string,
				subobj = new Dictionary<string, SubobjDef>(),
				union = param("union", null, null, false) as string,
				distinct = (bool)param("distinct/b", false)
			};
			this.isAggregationQuery = sqlConf.gres != null;

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
			this.onQuery();

			bool addDefaultCol = false;
			// 确保res/gres参数符合安全限定
			if (gres != null) {
				this.filterRes(gres, true);
			}
			else if (res == null) {
				res = this.defaultRes;
				addDefaultCol = true;
			}

			if (res != null) {
				this.filterRes(res);
			}
			// 设置gres时，不使用default vcols/subobj
			if (addDefaultCol) {
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
				this.supportEasyui();
				if (this.sqlConf.orderby != null && this.sqlConf.union == null)
					this.sqlConf.orderby = this.filterOrderby(this.sqlConf.orderby);
			}

			// fixUserQuery
			String cond = this.sqlConf.cond[0];
			if (cond != null)
				this.sqlConf.cond[0] = fixUserQuery(cond);
			if (this.sqlConf.gcond != null)
				this.sqlConf.gcond = fixUserQuery(this.sqlConf.gcond);
		}

		protected void validate()
		{
			// TODO: check fields in metadata
			// foreach ($_POST as ($field, $val))
			if (this.readonlyFields != null)
			{
				foreach (var field in this.readonlyFields)
				{
					if (_POST[field] != null && !(ac == "add" && this.requiredFields.Contains(field)))
					{
						logit(string.Format("!!! warn: attempt to chang readonly field `{0}`", field));
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
							logit(string.Format("!!! warn: attempt to change readonly field `{0}`", field));
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

		public void before()
		{
			if (this.allowedAc != null && stdAc.Contains(ac) && !this.allowedAc.Contains(ac))
				throw new MyException(E_FORBIDDEN, string.Format("Operation `{0}` is not allowed on object `{1}`", ac, table));
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
			rowData.RemoveIf_jd(k => k[0] == '_' || k == "pwd");
			// TODO: flag_handleResult(rowData);
			this.onHandleRow(rowData);
		}

		// for query. "field1"=>"t0.field1"
		private String fixUserQuery(String q)
		{
			if (q.IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0) {
				throw new MyException(E_FORBIDDEN, "forbidden SELECT in param cond");
			}
			// "aa = 100 and t1.bb>30 and cc IS null" . "t0.aa = 100 and t1.bb>30 and t0.cc IS null" 
			var ret = Regex.Replace(q, @"[\w.]+(?=(\s*[=><]|(\s+(IS|LIKE))))", m => {
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
			return ret;
		}
		private void supportEasyui()
		{
			if (param("rows") != null) {
				env._GET["pagesz"] = (string)param("rows");
			}
			// support easyui: sort/order
			string sort = param("sort") as string;
			if (sort != null)
			{
				string orderby = sort;
				string order = param("order") as string;
				if (order != null)
					orderby += " " + order;
				this.sqlConf.orderby = orderby;
			}
			// 兼容旧代码: 支持 _pagesz等参数，新代码应使用pagesz
			foreach (var e in new string[] {"_pagesz", "_pagekey", "_fmt"})
			{
				if (param(e) != null) {
					env._GET[e] = (string)param(e);
				}
			}
		}
		// return: new field list
		private void filterRes(string res, bool gres=false)
		{
			List<string> cols = new List<string>();
			foreach (var col0 in res.Split(',')) 
			{
				string col = col0.Trim();
				string alias = null;
				string fn = null;
				if (col == "*" || col == "t0.*") 
				{
					this.addRes("t0.*", false);
					continue;
				}
				Match m;
				// 适用于res/gres, 支持格式："col" / "col col1" / "col as col1"
				if (! (m=Regex.Match(col, @"^(\w+)(?:\s+(?:AS\s+)?(\S+))?$", RegexOptions.IgnoreCase)).Success)
				{
					// 对于res, 还支持部分函数: "fn(col) as col1", 目前支持函数: count/sum，如"count(distinct ac) cnt", "sum(qty*price) docTotal"
					if (!gres && (m=Regex.Match(col, @"^(\w+)\([a-z0-9_.\'* ,+\/]+\)\s+(?:AS\s+)?(\S+)$", RegexOptions.IgnoreCase)).Success)
					{
						fn = m.Groups[1].Value.ToUpper();
						if (fn != "COUNT" && fn != "SUM")
							throw new MyException(E_FORBIDDEN, string.Format("SQL function not allowed: `{0}`", fn));
						this.isAggregationQuery = true;
					}
					else 
						throw new MyException(E_PARAM, string.Format("bad property `{0}`", col));
				}
				else
				{
					if (m.Groups[2].Length > 0) {
						col = m.Groups[1].Value;
						alias = m.Groups[2].Value;
					}
				}
				if (fn != null) 
				{
					this.addRes(col);
					continue;
				}

	// 			if (! ctype_alnum(col))
	// 				throw new MyException(E_PARAM, "bad property `col`");
				if (this.addVCol(col, true, alias) == false)
				{
					if (!gres && this.subobj != null && this.subobj.ContainsKey(col))
					{
						this.sqlConf.subobj[alias != null ? alias : col] = this.subobj[col];
					}
					else
					{
						col = "t0." + col;
						var col1 = col;
						if (alias != null)
						{
							col1 += " " + alias;
						}
						this.addRes(col1);
					}
				}
				// mysql可在group-by中直接用alias, 而mssql要用原始定义
				if (env.cnn.DbStrategy.acceptAliasInOrderBy())
					cols.Add(alias != null ? alias : col);
				else
					cols.Add(col);
			}
			if (gres)
				this.sqlConf.gres = string.Join(",", cols);
		}

		// 注意：mysql中order by/group by可以使用alias, 但mssql中不可以，需要换成alias的原始定义
		// 而在where条件中，alias都需要换成原始定义，见 fixUserQuery
		private string filterOrderby(string orderby)
		{
			var colArr = new List<string>();
			foreach (var col0 in orderby.Split(',')) {
				var col = col0.Trim();
				Match m;
				if (! (m=Regex.Match(col, @"^(\w+\.)?(\S+)(\s+(asc|desc))?$", RegexOptions.IgnoreCase)).Success)
					throw new MyException(E_PARAM, string.Format("bad property `{0}`", col));
				if (m.Groups[1].Value.Length > 0) // e.g. "t0.id desc"
				{
					colArr.Add(col);
					continue;
				}
				if (col.IndexOf(".") < 0)
				{
					col = Regex.Replace(col, @"^(\S+)", m1 =>
					{
						string col1 = m1.Groups[1].Value;
						col1 = col1.Replace("\"", "");
						if (this.addVCol(col1, true, "-") != false)
						{
							// mysql可在order-by中直接用alias, 而mssql要用原始定义
							if (! env.cnn.DbStrategy.acceptAliasInOrderBy())
								return this.vcolMap[col1].def;
							return col1;
						}
						return "t0." + col1;
					});
				}
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

			this.onAfter(ref ret);
			if (this.onAfterActions != null)
				this.onAfterActions();
		}

		public virtual object api_add()
		{
			this.validate();

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
				this._GET["id"] = this.id.ToString();
				ret = env.callSvc(this.table + ".get");
			}
			else
				ret = this.id;
			return ret;
		}

		public virtual void api_set()
		{
			this.onValidateId();
			this.id = (int)mparam("id");
			this.validate();

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
			this.onValidateId();
			this.id = (int)mparam("id");

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

		private JsObject queryAllCache = new JsObject();
		protected JsArray queryAll(string sql, bool assoc, bool tryCache)
		{
			JsArray ret = null;
			if (tryCache && queryAllCache != null)
			{
				object value;
				queryAllCache.TryGetValue(sql, out value);
				ret = value as JsArray;
			}
			if (ret == null)
			{
				ret = queryAll(sql, assoc);
				if (tryCache)
				{
					if (queryAllCache == null)
						queryAllCache = new JsObject();
					queryAllCache[sql] = ret;
				}
			}
			return ret;
		}
		private void handleSubObj(int id, JsObject mainObj)
		{
			var subobj = this.sqlConf.subobj;
			if (subobj != null) 
			{
				// opt: {sql, wantOne=false}
				foreach (var kv in subobj) {
					string k = kv.Key;
					var opt = kv.Value;
					if (opt.sql == null)
						continue;
					string sql1 = opt.sql.Replace("%d", id.ToString()); // e.g. "select * from OrderItem where orderId=%d"
					bool tryCache = sql1 == opt.sql;
					JsArray ret1 = queryAll(sql1, true, tryCache);
					if (opt.wantOne) 
					{
						if (ret1.Count > 0)
							mainObj[k] = ret1[0];
						else
							mainObj[k] = null;
					}
					else {
						mainObj[k] = ret1;
					}
				}
			}
		}

		// return: JsObject
		public virtual object api_get()
		{
			this.onValidateId();
			this.id = (int)mparam("id");
			this.initQuery();

			this.addCond("t0.id=" + this.id, true);
			StringBuilder sql = genQuerySql();
			object ret = queryOne(sql.ToString(), true);
			if (ret.Equals(false))
				throw new MyException(E_PARAM, string.Format("not found `{0}.id`=`{1}`", table, id));
			JsObject ret1 = ret as JsObject;
			this.handleSubObj(this.id, ret1);
			this.handleRow(ret1);

			return ret;
		}

		void outputCsvLine(JsArray row, string enc)
		{
			bool firstCol = true;
			foreach (object e in row)
			{
				if (firstCol)
					firstCol = false;
				else
					echo(',');
				string s = e.ToString().Replace("\"", "\"\"");
				if (enc != null)
				{
					byte[] bs = Encoding.GetEncoding(enc).GetBytes(s);
					echo('"', bs, '"');
				}
				else
				{
					echo('"', s, '"');
				}
			}
			echo("\n");
		}

		void table2csv(JsObject tbl, string enc = null)
		{
			outputCsvLine(tbl["h"] as JsArray, enc);
			foreach (JsArray row in tbl["d"] as JsArray) 
			{
				outputCsvLine(row, enc);
			}
		}

		void table2txt(JsObject tbl)
		{
			echo(string.Join("\t", (tbl["h"] as JsArray)), "\n");
			foreach (JsArray row in (tbl["d"] as JsArray)) 
			{
				echo(string.Join("\t", row), "\n");
			}
		}

		void handleExportFormat(string fmt, JsObject ret, string fname)
		{
			bool handled = false;
			if (fmt == "csv") 
			{
				header("Content-Type", "application/csv; charset=UTF-8");
				header("Content-Disposition", "attachment;filename=" + fname + ".csv");
				table2csv(ret);
				handled = true;
			}
			else if (fmt == "excel") 
			{
				header("Content-Type", "application/csv; charset=gb2312");
				header("Content-Disposition", "attachment;filename=" + fname + ".csv");
				table2csv(ret, "gb2312");
				handled = true;
			}
			else if (fmt == "txt") 
			{
				header("Content-Type", "text/plain; charset=UTF-8");
				header("Content-Disposition", "attachment;filename=" + fname + ".txt");
				table2txt(ret);
				handled = true;
			}
			if (handled)
				throw new DirectReturn();
		}

		public object api_query()
		{
			this.initQuery();

			int? pagesz = param("pagesz/i") as int?;
			int? pagekey = param("pagekey/i") as int?;
			bool enableTotalCnt = false;
			bool enablePartialQuery = true;

			if (pagekey == null) {
				pagekey = param("page/i") as int?;
				if (pagekey != null)
				{
					enableTotalCnt = true;
					enablePartialQuery = false;
				}
			}
			int maxPageSz = Math.Min(this.maxPageSz, PAGE_SZ_LIMIT);
			if (pagesz == null)
				pagesz = 20;
			if (pagesz < 0 || pagesz > maxPageSz)
				pagesz = maxPageSz;

			if (this.isAggregationQuery) {
				enablePartialQuery = false;
			}

			string orderSql = sqlConf.orderby;

			// setup cond for partialQuery
			if (orderSql == null && !this.isAggregationQuery)
				orderSql = this.filterOrderby(defaultSort);

			if (enableTotalCnt == false && pagekey != null && pagekey == 0)
			{
				enableTotalCnt = true;
			}

			// 如果未指定orderby或只用了id(以后可放宽到唯一性字段), 则可以用partialQuery机制(性能更好更精准), pagekey表示该字段的最后值；否则pagekey表示下一页页码。
			string partialQueryCond;
			if (enablePartialQuery) {
				if (Regex.IsMatch(orderSql, @"^(t0\.)?id\b"))
				{
					if (pagekey != null && pagekey != 0)
					{
						if (Regex.IsMatch(orderSql, @"\bid DESC", RegexOptions.IgnoreCase))
						{
							partialQueryCond = "t0.id<" + pagekey;
						}
						else
						{
							partialQueryCond = "t0.id>" + pagekey;
						}
						// setup res for partialQuery
						if (partialQueryCond != null)
						{
							// 							if (sqlConf["res"][0] != null && !Regex.IsMatch('/\bid\b/',sqlConf["res"][0])) {
							// 								array_unshift(sqlConf["res"], "t0.id");
							// 							}
							sqlConf.cond.Insert(0, partialQueryCond);
						}
					}
				}
				else
				{
					enablePartialQuery = false;
				}
			}

			string tblSql, condSql;
			StringBuilder sql = genQuerySql(out tblSql, out condSql);

			bool complexCntSql = false;
			if (sqlConf.union != null) {
				sql.Append("\nUNION\n").Append(sqlConf.union);
				complexCntSql = true;
			}
			if (sqlConf.gres != null) {
				sql.AppendFormat("\nGROUP BY {0}", sqlConf.gres);
				if (sqlConf.gcond != null)
					sql.AppendFormat("\nHAVING {0}", sqlConf.gcond);
				complexCntSql = true;
			}

			object totalCnt = null;

			if (enableTotalCnt) {
				string cntSql;
				if (! complexCntSql) {
					cntSql = "SELECT COUNT(*) FROM " + tblSql;
					if (condSql.Length > 0)
						cntSql += "\nWHERE " + condSql;
				}
				else {
					cntSql = "SELECT COUNT(*) FROM (" + sql + ") t0";
				}
				totalCnt = queryScalar(cntSql);
			}

			if (orderSql != null)
				sql.AppendFormat("\nORDER BY {0}", orderSql);

			if (enablePartialQuery) {
				sql.AppendFormat("\nLIMIT {0}", pagesz);
			}
			else {
				if (pagekey == null || pagekey == 0) {
					pagekey = 1;
					sql.AppendFormat("\nLIMIT {0}", pagesz);
				}
				else {
					sql.AppendFormat("\nLIMIT {0},{1}", (pagekey-1)*pagesz, pagesz);
				}
			}

			string sql1 = env.cnn.fixPaging(sql.ToString());
			var objArr = queryAll(sql1, true);

			// Note: colCnt may be changed in after().
			int fixedColCnt = objArr.Count()==0? 0: (objArr[0] as JsObject).Count();
			objArr.ForEach(rowData => {
				var row = rowData as JsObject;
				this.handleRow(row);
			});
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
			foreach (JsObject mainObj in objArr) {
				object id1;
				if (mainObj.TryGetValue("id", out id1))
				{
					handleSubObj((int)id1, mainObj);
				}
			}
			string fmt = param("fmt") as string;
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
			if (fmt != null && fmt != "list")
				handleExportFormat(fmt, ret, this.table);

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
			if ( (m=Regex.Match(res, @"^(\w+)\.(\w+)$")).Success) {
				colName = m.Groups[2].Value;
				def = res;
			}
			else if ( (m = Regex.Match(res, @"^(.*?)\s+(?:as\s+)?""?(\w+)""?$", RegexOptions.IgnoreCase | RegexOptions.Singleline)).Success) {
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

	根据列名找到vcolMap中的一项，添加到最终查询语句中.
	vcolMap是分析vcolDef后的结果，每一列都对应一项；而在一项vcolDef中可以包含多列。

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
			this.addVColDef(this.vcolMap[col].vcolDefIdx);
			if (alias != null) {
				if (alias != "-")
					this.addRes(this.vcolMap[col].def + " " + alias, false);
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
					foreach (var e in vcolDef.res) {
						this.addRes(e);
					}
				}
				++ idx;
			}
		}

		/*
		根据index找到vcolDef中的一项，添加join/cond到最终查询语句(但不包含res)。
		 */
		private ISet<int> m_vcolDefIndex = new HashSet<int>();
		private void addVColDef(int idx)
		{
			if (idx < 0 || m_vcolDefIndex.Contains(idx))
				return;

			var vcolDef = this.vcolDefs[idx];
			m_vcolDefIndex.Add(idx);
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
