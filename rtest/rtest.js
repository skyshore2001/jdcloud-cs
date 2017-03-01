// doc: https://jasmine.github.io/2.5/introduction

describe("工具函数", function() {

describe("param函数", function() {

	it("整型参数", function () {
		var ret = callSvrSync("fn", {f: "param", name: "id", id: 99});
		expect(ret).toBe(99);

		ret = callSvrSync("fn", {f: "param", name: "id", id: '9a'});
		expect(ret).toJDCallFail(E_PARAM);
	});

	it("字符串型参数", function () {
		var str = "jason smith";
		var ret = callSvrSync("fn", {f: "param", name: "str", coll: "P"}, $.noop, {str: str});
		expect(ret).toBe(str);
	});

	it("字符串型参数-防止XSS攻击", function () {
		var str = "a&b";
		var str2 = "a&amp;b";
		var ret = callSvrSync("fn", {f: "param", name: "str", coll: "P"}, $.noop, {str: str});
		expect(ret).toBe(str2);
	});

	it("数值型参数", function () {
		var ret = callSvrSync("fn", {f: "param", name: "amount/n", coll: 'P'}, $.noop, {amount: 99.9});
		expect(ret).toBe(99.9);

		ret = callSvrSync("fn", {f: "param", name: "amount/n", amount: '9a'});
		expect(ret).toJDCallFail(E_PARAM);
	});

	it("布尔型参数", function () {
		var ret = callSvrSync("fn", {f: "param", name: "wantArray/b", wantArray: 1});
		expect(ret).toBe(true);

		var ret = callSvrSync("fn", {f: "param", name: "wantArray/b", wantArray: '1a'});
		expect(ret).toJDCallFail(E_PARAM);
	});
	it("日期型参数", function () {
		var dt = new Date();

		var ret = callSvrSync("fn", {f: "param", name: "testDt/dt", testDt: dt.toISOString()});
		// 服务端返回日期格式为 '/Date(1488124800000)/'
		expect(typeof(ret) == "string").toBe(true);
		ret = parseDate(ret);
		expect(ret).toEqual(dt);

		var ret = callSvrSync("fn", {f: "param", name: "testDt/dt", testDt: 'today'});
		expect(ret).toJDCallFail(E_PARAM);
	});

	it("i+类型", function () {
		var ret = callSvrSync("fn", {f: "param", name: "idList/i+", idList: "3,4,5"});
		expect(ret).toEqual([3,4,5]);

		var ret = callSvrSync("fn", {f: "param", name: "idList/i+", idList: '3;4;5'});
		expect(ret).toJDCallFail(E_PARAM);
	});
	xit("压缩表类型", function () {
		var ret = callSvrSync("fn", {f: "param", name: "items/i:n:s", items: "100:1:洗车,101:1:打蜡"});
		expect(ret).toEqual([ [100, 1.0, "洗车"], [101, 1, "打蜡"]]);

		var ret = callSvrSync("fn", {f: "param", name: "items/i:n?:s?", items: "100:1,101::打蜡"});
		expect(ret).toEqual([ [100, 1.0, null], [101, null, "打蜡"]]);

		var ret = callSvrSync("fn", {f: "param", name: "items/i:n:s", items: '100:1,101:2'});
		expect(ret).toJDCallFail(E_PARAM);
	});

	it("mparam", function () {
		var ret = callSvrSync("fn", {f: "mparam", name: "id", id: 99});
		expect(ret).toEqual(99);

		ret = callSvrSync("fn", {f: "mparam", name: "id", id: 99, coll: "P"});
		expect(ret).toJDCallFail(E_PARAM);
	});
});

describe("数据库函数", function() {
	var tm_ = new Date();
	tm_.setMilliseconds(0); // 注意：mysql数据库不能存储毫秒
	var addr_ = "test-addr";
	var id_, want_;

	beforeEach(function() {
		genId();
	});

	// set id_, want_
	function genId()
	{
		if (id_ != null)
			return;

		var tmstr = formatDate(tm_);
		var ret = callSvrSync("fn", {f: "execOne", sql: "INSERT INTO ApiLog (tm, addr) VALUES ('" + tmstr + "', '" + addr_ + "')", getNewId: true});
		expect(typeof(ret) == "number").toBe(true);
		id_ = ret;

		want_ = {id: id_, tm: tm_, addr: addr_};
	}

	it("execOne", function () {
	});

	it("queryAll", function () {
		var ret = callSvrSync("fn", {f: "queryAll", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=" + id_, assoc: true});
		expect($.isArray(ret) && $.isPlainObject(ret[0])).toBe(true);
		formatField(ret[0]);
		expect(ret[0]).toEqual(want_);
	});
	it("queryAll-empty", function () {
		var ret = callSvrSync("fn", {f: "queryAll", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=-1", assoc: true});
		expect($.isArray(ret) && ret.length==0).toBe(true);
	});

	it("queryOne", function () {
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=" + id_, assoc: true});
		formatField(ret);
		expect(ret).toEqual(want_);
	});
	it("queryOne-array", function () {
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=" + id_});
		expect($.isArray(ret) && ret.length == 3).toBe(true);
		var ret1 = array_combine(["id","tm","addr"], ret);
		formatField(ret1);
		expect(ret1).toEqual(want_);
	});
	it("queryOne-empty", function () {
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT id,tm,addr FROM ApiLog WHERE id=-1"});
		expect(ret).toBe(false);
	});

	it("queryOne-scalar", function () {
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT tm FROM ApiLog WHERE id=" + id_});
		expect(parseDate(ret)).toEqual(tm_);
	});

	it("执行错误", function () {
		var ret = callSvrSync("fn", {f: "queryOne", sql: "SELECT FROM ApiLog WHERE id=" + id_, assoc: true});
		expect(ret).toJDCallFail(E_DB);
	});
});


});

describe("登录及权限", function() {
	var uname_ = "jdcloud", pwd_ = "1234";
	var uid_;

	beforeAll(function() {
		userLogout();
	});

	function userLogin()
	{
		if (uid_ != null)
			return;
		var ret = callSvrSync("login", {uname: uname_, pwd: pwd_});
		expect(ret).toEqual({id: jasmine.any(Number)});
		uid_ = ret.id;
	}
	function userLogout()
	{
		var ret = callSvrSync("logout");
		//expect(ret).toBe("OK");
		uid_ = null;
	}

	it("不存在的接口", function () {
		var ret = callSvrSync("xxx_no_method");
		expect(ret).toJDCallFail(E_PARAM);
	});

	it("未登录时调用要求登录的接口", function () {
		var ret = callSvrSync("whoami");
		expect(ret).toJDCallFail(E_NOAUTH);
	});

	it("login", function () {
		userLogin();
	});

	it("whoami", function () {
		userLogin();
		var ret = callSvrSync("whoami");
		expect(ret).toEqual({id: uid_});
	});
})

describe("对象型接口", function() {
	var id_;
	var postParam_; // {ac, addr}

	function generalAdd(withRes)
	{
		if (id_ != null)
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
				tm: jasmine.any(Date)
			}));
			// ac未在res中指定，不应包含
			expect(ret.ac).toBe(undefined);
			id_ = ret.id;
		}
	}

	it("add操作", function () {
		generalAdd();
	});
	xit("add操作-res", function () {
		generalAdd(true);
	});

	it("get操作", function () {
		generalAdd();
		var ret = callSvrSync("ApiLog.get", {id: id_});
		expect(ret).toJDContainKey(["id", "addr", "ac", "tm"], true);
	});

	it("get操作-res", function () {
		generalAdd();
		var ret = callSvrSync("ApiLog.get", {id: id_, res: "id,addr,tm"});
		formatField(ret);
		expect(ret).toEqual({id: id_, addr: postParam_.addr, tm: jasmine.any(Date)});
	});
})

