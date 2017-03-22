// doc: https://jasmine.github.io/2.5/introduction

/**
准备：

User表中添加数据：

insert into User (uname, pwd, name) values ('rtest', '1234', 'jdcloud-test')
 */
// tools

// ctx={uname, pwd, uid?}
var loginCtx = {uname: "rtest", pwd: "1234"};

function userLogin()
{
	var ctx = loginCtx;
	if (ctx.uid != null)
		return;
	var ret = callSvrSync("login", {uname: ctx.uname, pwd: ctx.pwd});
	expect(ret).toEqual({id: jasmine.any(Number)});
	ctx.uid = ret.id;
}

function userLogout()
{
	var ret = callSvrSync("logout");
	//expect(ret).toEqual("OK");
	loginCtx.uid = null;
}

describe("工具函数", function() {

describe("param函数", function() {

	it("整型参数", function () {
		var ret = callSvrSync("fn", {f: "param", name: "id", id: 99});
		expect(ret).toEqual(99);

		ret = callSvrSync("fn", {f: "param", name: "id", id: '9a'});
		expect(ret).toJDRet(E_PARAM);
	});

	it("字符串型参数", function () {
		var str = "jason smith";
		var ret = callSvrSync("fn", {f: "param", name: "str", coll: "P"}, $.noop, {str: str});
		expect(ret).toEqual(str);
	});

	it("字符串型参数-防止XSS攻击", function () {
		var str = "a&b";
		var str2 = "a&amp;b";
		var ret = callSvrSync("fn", {f: "param", name: "str", coll: "P"}, $.noop, {str: str});
		expect(ret).toEqual(str2);
	});

	it("数值型参数", function () {
		var ret = callSvrSync("fn", {f: "param", name: "amount/n", coll: 'P'}, $.noop, {amount: 99.9});
		expect(ret).toEqual(99.9);

		ret = callSvrSync("fn", {f: "param", name: "amount/n", amount: '9a'});
		expect(ret).toJDRet(E_PARAM);
	});

	it("布尔型参数", function () {
		var ret = callSvrSync("fn", {f: "param", name: "wantArray/b", wantArray: 1});
		expect(ret).toEqual(true);

		var ret = callSvrSync("fn", {f: "param", name: "wantArray/b", wantArray: '1a'});
		expect(ret).toJDRet(E_PARAM);
	});
	it("日期型参数", function () {
		var dt = new Date();
		dt.setMilliseconds(0);

		var ret = callSvrSync("fn", {f: "param", name: "testDt/dt", testDt: dt.toISOString()});
		// 服务端返回日期格式为 '/Date(1488124800000)/'
		ret = parseDate(ret);
		expect(ret).toEqual(dt);

		var ret = callSvrSync("fn", {f: "param", name: "testDt/dt", testDt: 'bad-datetime'});
		expect(ret).toJDRet(E_PARAM);
	});

	it("i+类型", function () {
		var ret = callSvrSync("fn", {f: "param", name: "idList/i+", idList: "3,4,5"});
		expect(ret).toEqual([3,4,5]);

		var ret = callSvrSync("fn", {f: "param", name: "idList/i+", idList: '3;4;5'});
		expect(ret).toJDRet(E_PARAM);
	});
	it("压缩表类型-varr", function () {
		var ret = callSvrSync("fn", {f: "param", name: "items/i:n:s:b:dt", items: "100:1:洗车:1:2016-10-11,101:1:打蜡:0:2017-10-11"});
		expect(ret).toEqual([ [100, 1.0, "洗车", true, daca.anyDate("2016-10-11")], [101, 1, "打蜡", false, daca.anyDate("2017-10-11")]]);

		var ret = callSvrSync("fn", {f: "param", name: "items/i:n?:s?", items: "100:1,101::打蜡"});
		expect(ret).toEqual([ [100, 1.0, null], [101, null, "打蜡"]]);

		var ret = callSvrSync("fn", {f: "param", name: "items/i:n:s", items: '100:1,101:2'});
		expect(ret).toJDRet(E_PARAM);
	});

	it("mparam", function () {
		var ret = callSvrSync("fn", {f: "mparam", name: "id", id: 99});
		expect(ret).toEqual(99);

		ret = callSvrSync("fn", {f: "mparam", name: "id", id: 99, coll: "P"});
		expect(ret).toJDRet(E_PARAM);
	});
});

describe("数据库函数", function() {
	var rowCtx_ = {
		tm: new Date(),
		addr: "test-addr",
		id: null,
	};
	rowCtx_.tm.setMilliseconds(0);  // 注意：mysql数据库不能存储毫秒

	function genId()
	{
		if (rowCtx_.id != null)
			return;

		var tmstr = formatDate(rowCtx_.tm);
		var ret = callSvrSync("fn", {f: "execOne", sql: "INSERT INTO ApiLog (tm, addr) VALUES ('" + tmstr + "', '" + rowCtx_.addr + "')", getNewId: true});
		expect(ret).toEqual(jasmine.any(Number));
		rowCtx_.id = ret;
	}

	it("execOne", function () {
		genId();
	});

	it("queryAll", function () {
		genId();
		var ret = callSvrSync("fn", {f: "queryAll", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=" + rowCtx_.id, assoc: true});
		expect(ret).toEqual(jasmine.any(Array));
		expect(ret[0]).toEqual({id: rowCtx_.id, tm: daca.anyDate(rowCtx_.tm), addr: rowCtx_.addr});
	});
	it("queryAll-empty", function () {
		genId();
		var ret = callSvrSync("fn", {f: "queryAll", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=-1", assoc: true});
		expect(ret).toEqual([]);
	});

	it("queryOne", function () {
		genId();
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=" + rowCtx_.id, assoc: true});
		expect(ret).toEqual({id: rowCtx_.id, tm: daca.anyDate(rowCtx_.tm), addr: rowCtx_.addr});
	});
	it("queryOne-array", function () {
		genId();
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=" + rowCtx_.id});
		expect($.isArray(ret) && ret.length == 3).toEqual(true);
		expect(ret).toEqual([rowCtx_.id, daca.anyDate(rowCtx_.tm), rowCtx_.addr]);
	});
	it("queryOne-empty", function () {
		genId();
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=-1"});
		expect(ret).toEqual(false);
	});

	it("queryOne-scalar", function () {
		genId();
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT tm FROM ApiLog WHERE id=" + rowCtx_.id});
		expect(ret).toEqual(daca.anyDate(rowCtx_.tm));
	});

	it("执行错误", function () {
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT FROM ApiLog WHERE id=" + rowCtx_.id, assoc: true});
		expect(ret).toJDRet(E_DB);
	});
});

});

describe("登录及权限", function() {

	beforeAll(function() {
		userLogout();
	});

	it("不存在的接口", function () {
		var ret = callSvrSync("xxx_no_method");
		expect(ret).toJDRet(E_PARAM);
	});

	it("未登录时调用要求登录的接口", function () {
		var ret = callSvrSync("whoami");
		expect(ret).toJDRet(E_NOAUTH);
	});

	it("login", function () {
		userLogin();
	});

	it("whoami", function () {
		userLogin();
		var ret = callSvrSync("whoami");
		expect(ret).toEqual({id: loginCtx.uid});
	});
});

describe("对象型接口", function() {
	var id_;
	var postParam_; // {ac, addr}

	beforeAll(function() {
		userLogout();
	});

	function generalAdd(withRes)
	{
		if (id_ != null && !withRes)
			return;

		var rd = Math.random();
		postParam_ = {ac: "ApiLog.add", addr: "test-addr" + rd};
		var param = {};
		if (withRes) {
			param.res = "id,addr,tm";
		}
		var ret = callSvrSync("ApiLog.add", param, $.noop, postParam_);

		if (!withRes) {
			expect(ret).toEqual(jasmine.any(Number));
			id_ = ret;
		}
		else {
			expect(ret).toEqual(jasmine.objectContaining({
				id: jasmine.any(Number),
				addr: postParam_.addr,
				tm: jasmine.any(String)
				//tm: jasmine.any(Date)
			}));
			// ac未在res中指定，不应包含
			expect(ret).not.toJDObj(["ac"]);
			id_ = ret.id;
		}
	}

	it("add操作", function () {
		generalAdd();
	});
	it("add操作-res", function () {
		generalAdd(true);
	});
	it("add操作-必填字段", function () {
		// ac为必填字段
		var ret = callSvrSync("ApiLog.add", $.noop, {addr: "my addr"});
		expect(ret).toJDRet(E_PARAM);
	});

	it("get操作", function () {
		generalAdd();
		var ret = callSvrSync("ApiLog.get", {id: id_});
		expect(ret).toJDObj(["id", "addr", "ac", "tm"], true);
		// ua是隐藏字段，不应返回
		expect(ret).not.toJDObj(["ua"]);
	});

	it("get操作-res", function () {
		generalAdd();
		var ret = callSvrSync("ApiLog.get", {id: id_, res: "id,addr,tm"});
		formatField(ret);
		expect(ret).toEqual({id: id_, addr: postParam_.addr, tm: jasmine.any(Date)});
	});
	it("set操作", function () {
		generalAdd();

		var newAddr = "new-addr";
		var ret = callSvrSync("ApiLog.set", {id: id_}, $.noop, {ac: "new-ac", addr: newAddr, tm: '2010-01-01'});
		expect(ret).toEqual("OK");

		var ret = callSvrSync("ApiLog.get", {id: id_, res: 'ac,addr,tm'});
		formatField(ret);
		// ac,tm是只读字段，设置无效。应该仍为当前日期
		expect(ret).toEqual({ac: postParam_.ac, addr: newAddr, tm: jasmine.any(Date)});
		expect(ret.tm.getFullYear()).toEqual(new Date().getFullYear());
	});

	it("query操作", function () {
		generalAdd();

		var pagesz = 3;
		var ret = callSvrSync("ApiLog.query", {_pagesz: pagesz});
		expect(ret).toJDTable(["id", "ac", "addr", "tm"]);
		// 至少有一条
		expect(ret.d.length).toBeGreaterThan(0);
		if (ret.nextkey) {
			expect(ret.d.length).toEqual(pagesz);
		}
		else {
			expect(ret.d.length).toBeLessThanOrEqual(pagesz);
		}
		// 不包含"ua"属性
		expect(ret.h).not.toContain("ua");
	});
	it("query操作-res/cond", function () {
		generalAdd();

		var pagesz = 3;
		var ret = callSvrSync("ApiLog.query", {_pagesz: pagesz, res: "id,ac", cond: "id=" + id_});
		expect(ret).toEqual({h: ["id", "ac"], d: jasmine.any(Array)});
		// 只有一条
		expect(ret.d.length).toEqual(1);
	});
	it("query操作-gres统计", function () {
		//generalAdd();

		var pagesz = 3;
		var dt = new Date();
		dt.setTime(dt - T_DAY*7); // 7天内按ac分组统计
		var ret = callSvrSync("ApiLog.query", {gres:"ac", res:"count(*) cnt, sum(id) fakeSum", cond: "tm>='" + formatDate(dt) + "' and ac IS NOT NULL", orderby: "cnt desc", _pagesz: pagesz});
		expect(ret).toJDTable(["ac", "cnt", "fakeSum"]);
	});
	it("query操作-gres统计-中文", function () {
		var pagesz = 3;
		var dt = new Date();
		dt.setTime(dt - T_DAY*7); // 7天内按ac分组统计
		var ret = callSvrSync("ApiLog.query", {gres:"ac 动作", res:"count(*) 总数, sum(id) 总和", cond: "tm>='" + formatDate(dt) + "'", orderby: "总数 desc", _pagesz: pagesz});
		expect(ret).toJDTable(["动作", "总数", "总和"]);

		var ret = callSvrSync("ApiLog.query", {gres:"ac \"动作\"", res:"count(*) \"总数\", sum(id) \"总和\"", cond: "tm>='" + formatDate(dt) + "'", orderby: "\"总数\" desc"});
		expect(ret).toJDTable(["动作", "总数", "总和"]);
	});
	it("query操作-list", function () {
		generalAdd();

		var pagesz = 3;
		var ret = callSvrSync("ApiLog.query", {_pagesz: pagesz, _fmt: "list"});
		expect(ret).toJDList(["id", "ac", "addr", "tm"]);
		// 至少有一条
		expect(ret.list.length).toBeGreaterThan(0);
		if (ret.nextkey) {
			expect(ret.list.length).toEqual(pagesz);
		}
		else {
			expect(ret.list.length).toBeLessThanOrEqual(pagesz);
		}
		// 不包含"ua"属性
		expect(ret.list[0].hasOwnProperty("ua")).toBeFalsy();
	});

	function testExport(fmt, sp)
	{
		var ret = callSvrSync("ApiLog.query", {_pagesz: 3, _fmt: fmt, res: "id,ac,addr,tm"}, $.noop, null, {nofilter:1});
		var arr = ret.split("\n");
		expect(arr.length >= 2).toEqual(true); // 至少2行，标题和首行

		var cols = arr[0].split(sp);
		expect(cols.length).toEqual(4); // 标题列

		cols = arr[1].split(sp);
		expect(cols.length).toEqual(4); // 数据列
	}

	it("query操作-导出txt", function () {
		testExport("txt", "\t");
	});
	it("query操作-导出csv", function () {
		testExport("csv", ",");
	});
	it("query操作-导出excel", function () {
		testExport("excel", ",");
	});

	it("del操作", function () {
		generalAdd();

		var ret = callSvrSync("ApiLog.del", {id: id_});
		expect(ret).toEqual("OK");

		var ret = callSvrSync("ApiLog.get", {id: id_});
		expect(ret).toJDRet(E_PARAM);
	});
});

describe("对象型接口-异常", function() {
	beforeAll(function() {
		userLogout();
	});

	it("id不存在", function () {
		$.each(["get", "set", "del"], function () {
			var ret = callSvrSync("ApiLog." + this);
			expect(ret).toJDRet(E_PARAM);

			if (this != "set") {
				var ret = callSvrSync("ApiLog." + this, {id: -9});
				expect(ret).toJDRet(E_PARAM);
			}
		});
	});

	it("表不存在", function () {
		var ret = callSvrSync("ApiLog123.query");
		expect(ret).toJDRet(E_NOAUTH);
	});

	it("操作不存在", function () {
		var ret = callSvrSync("ApiLog.cancel");
		expect(ret).toJDRet(E_PARAM);
	});

	it("字段不存在", function () {
		var ret = callSvrSync("ApiLog.query", {res: "id,id123"});
		expect(ret).toJDRet(E_DB);
	});

	it("限制cond", function () {
		var ret = callSvrSync("ApiLog.query", {cond: "userId in (SELECT id FROM User)"});
		expect(ret).toJDRet(E_FORBIDDEN);
	});

	it("限制res", function () {
		var ret = callSvrSync("ApiLog.query", {res: "Max(id) maxId"});
		expect(ret).toJDRet(E_FORBIDDEN);
	});
});

describe("UserApiLog", function() {
	var id_;
	var postParam_; // {ac, addr}

	beforeAll(function() {
		userLogout();
	});

	function generalAdd()
	{
		if (id_ != null)
			return;

		var rd = Math.random();
		postParam_ = {ac: "UserApiLog.add", addr: "test-addr" + rd};
		var param = {};
		var ret = callSvrSync("UserApiLog.add", param, $.noop, postParam_);

		expect(ret).toEqual(jasmine.any(Number));
		id_ = ret;
	}

	it("UserApiLog.query-noauth", function () {
		var ret = callSvrSync("UserApiLog.query");
		expect(ret).toJDRet(E_NOAUTH);
	});
	it("UserApiLog.set-forbidden", function () {
		userLogin();
		var ret = callSvrSync("UserApiLog.set");
		expect(ret).toJDRet(E_FORBIDDEN);
	});

	it("UserApiLog.add", function () {
		userLogin();
		generalAdd();
	});
	it("UserApiLog.query", function () {
		userLogin();
		generalAdd();

		var ret = callSvrSync("UserApiLog.query", {_pagesz:5, cond: "id<=" + id_});
		expect(ret).toJDTable(["id", "userName", "!last3LogAc", "!user", "!last3Log"]);
		var arr = rs2Array(ret);
		// 至少有一条
		expect(arr.length).toBeGreaterThan(0);
		expect(arr[0].id).toEqual(id_);
		expect(arr[0].userName != null).toEqual(true);
	});
	it("query-在cond条件中使用虚拟字段", function () {
		userLogin();
		generalAdd();

		var ret = callSvrSync("UserApiLog.query", {_pagesz:5, cond: "userId IS NOT NULL AND userId<>0 AND userName LIKE '%test%'"});
		expect(ret).toJDTable(["id", "userName", "!last3LogAc", "!user", "!last3Log"]);
		var arr = rs2Array(ret);
		// 至少有一条
		expect(arr.length).toBeGreaterThan(0);
	});
	it("UserApiLog.query-vcol", function () {
		userLogin();
		generalAdd();

		var ret = callSvrSync("UserApiLog.query", {res: "id, last3LogAc", _pagesz:5, cond: "id<=" + id_});
		expect(ret).toJDTable(["id", "!userName", "*last3LogAc", "!user", "!last3Log"]);
		var arr = rs2Array(ret);
		// 至少有一条
		expect(arr.length).toBeGreaterThan(0);
		expect(arr[0].id).toEqual(id_);

		// 子项1-3条之间
		var list = list2varr(arr[0].last3LogAc);
		var n = list.length;
		expect(n >= 0 && n <= 3).toEqual(true);
		// 每条有两列:id,ac
		expect(list[0].length == 2).toEqual(true);
	});
	it("UserApiLog.query-subobj", function () {
		userLogin();
		generalAdd();

		var ret = callSvrSync("UserApiLog.query", {res: "id, user, last3Log", _pagesz:5, cond: "id<=" + id_});
		expect(ret).toJDTable(["id", "!userName", "!last3LogAc", "user", "last3Log"]);
		var arr = rs2Array(ret);
		// 至少有一条
		expect(arr.length).toBeGreaterThan(0);
		expect(arr[0].id).toEqual(id_);
		expect(arr[0].user).toEqual({
			id: loginCtx.uid, name: jasmine.any(String)
		});
		expect($.isArray(arr[0].last3Log) && arr[0].last3Log.length > 0).toEqual(true);
	});

	it("UserApiLog.listByAc-非标方法", function () {
		userLogin();
		generalAdd();

		var ret = callSvrSync("UserApiLog.listByAc", {ac: "UserApiLog.add", _pagesz:1});
		expect(ret).toJDList(["id", "userName", "!last3LogAc", "!user", "!last3Log"]);
		var arr = ret.list;
		// 至少有一条, 由于设置了_pagesz=1，刚好一条
		expect(arr.length == 1).toEqual(true);
		expect(arr[0].id).toEqual(id_);
		expect(arr[0].userName != null).toEqual(true);
	});
});
