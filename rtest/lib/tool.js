var g_cfg = {
	url: "api.php",
};
var g_data = {
	inTest: true,
	critical: false,
	skip: false,
};

var E_PROTOCOL=-9999;
var E_AUTHFAIL=-1;
var E_OK=0;
var E_PARAM=1;
var E_NOAUTH=2;
var E_DB=3;
var E_SERVER=4;
var E_FORBIDDEN=5;

function getErrName(e)
{
	for (var k in window) {
		if (k.substr(0,2) == "E_" && window[k] === e)
			return k;
	}
	return e;
}

var T_HOUR = 3600 * 1000;
var T_DAY = T_HOUR*24;

// return: null/undefined - ignore processing; data - pre-processed app-level return; false - app-level failure handled by caller (and this.lastError=[code, msg, info?])
function defDataProc(rv)
{
	if (typeof rv !== "string")
		return rv;
	try {
		rv = $.parseJSON(rv);
	} catch (ex) {}

	if (rv && rv instanceof Array && rv.length >= 2 && typeof rv[0] == "number") {
		if (rv[0] == 0)
			return rv[1];
		g_data.lastError = rv;
	}
	else {
		g_data.lastError = [E_PROTOCOL, "服务器通讯协议异常!"]; // 格式不对
	}
	if (this.noex)
		return false;
}

/**
@fn callSvr(ac, param?, fn?, postParam?, ajaxOpt?)
@fn callSvr(ac, fn?, postParam?, ajaxOpt?)

调用前应正确配置g_cfg.url。

如果调用成功，返回有效数据([0, data]中的data部分)。
如果调用失败，错误写入 g_data.lastError.

当ajaxOpt.noex=1时，不弹出错误框。
当ajaxOpt.nofilter=1时，不做预处理，直接返回原始数据。
*/
function callSvr(ac, param, fn, postParam, ajaxOpt)
{
	if ($.isFunction(param)) {
		ajaxOpt = postParam;
		postParam = fn;
		fn = param;
		param = null;
	}

	var url = g_cfg.url  + "/" + ac;
	//var url = $("#url").val() + "?ac=" + ac;
	if (param) {
		if (url.indexOf("?") > 0) {
			url += "&" + $.param(param);
		}
		else {
			url += "?" + $.param(param);
		}
	}

	var retData = null;
	var opt = $.extend({
		type: postParam == null? "GET": "POST",
		success: fn,
		data: postParam,

		xhrFields: {
			withCredentials: true
		},

		dataFilter: function (data, type) {
			if (this.nofilter) {
				retData = data;
				return data;
			}
			rv = defDataProc.call(this, data);
			if (rv == null)
			{
				console.log(this.lastError);
				throw("abort");
			}
			retData = rv;
			return rv;
		}

	}, ajaxOpt);
	var ret = $.ajax(url, opt);
	console.log(retData);
}

/**
@fn callSvrSync(ac, param?, fn?, postParam?, ajaxOpt?)
@fn callSvrSync(ac, fn?, postParam?, ajaxOpt?)

同步调用，如果出错不报错，返回false，调用者可检查g_data.lastError获取错误内容，格式为 [errcode, errmsg]。
 */
function callSvrSync(ac, param, fn, postParam, ajaxOpt)
{
	if ($.isFunction(param)) {
		ajaxOpt = postParam;
		postParam = fn;
		fn = param;
		param = null;
	}
	ajaxOpt = $.extend(ajaxOpt, {async:false, noex:1});

	var ret = null;
	callSvr(ac, param, function (data) {
		ret = data;
	}, postParam, ajaxOpt);
	return ret;
}

var RE_CurrencyField = /(?:^(?:amount|price|total|qty)|(?:Amount|Price|Total|Qty))\d*$/;
var RE_DateField = /(?:^(?:dt|tm)|(?:Dt|Tm))\d*$/;
function formatField(obj)
{
	for (var k in obj) {
		if (obj[k] == null || typeof obj[k] !== 'string')
			continue;
		if (RE_DateField.test(k))
			obj[k] = parseDate(obj[k]);
		else if (RE_CurrencyField.test(k))
			obj[k] = parseFloat(obj[k]);
	}
	return obj;
}

// formatDate(dt, "D") -> "2016-1-1"
// formatDate(dt) -> "2016-1-1 10:10:10"
function formatDate(dt, fmt)
{
	if (fmt == null)
		return dt.getFullYear() + "-" + (dt.getMonth()+1) + "-" + dt.getDate() + " " + dt.getHours() + ":" + dt.getMinutes() + ":" + dt.getSeconds();
	if (fmt == "D")
		return dt.getFullYear() + "-" + (dt.getMonth()+1) + "-" + dt.getDate();
}

function parseDate(str)
{
	if (str == null)
		return null;
	if (str instanceof Date)
		return str;
	// unix timestamp
	if (typeof(str) == "number")
	{
		return new Date(str * 1000);
	}

	var ms = str.match(/^(\d+)(?:[-\/.](\d+)(?:[-\/.](\d+))?)?/);
	if (ms == null) {
		ms = str.match(/Date\((\d+)\)/);
		if (ms != null) {
			var val = parseInt(ms[1]);
			return new Date(val);
		}
		return null;
	}
	var y, m, d;
	var now = new Date();
	if (ms[3] !== undefined) {
		y = parseInt(ms[1]);
		m = parseInt(ms[2])-1;
		d = parseInt(ms[3]);
		if (y < 100)
			y += 2000;
	}
	else if (ms[2] !== undefined) {
		y = now.getFullYear();
		m = parseInt(ms[1])-1;
		d = parseInt(ms[2]);
	}
	else {
		y = now.getFullYear();
		m = now.getMonth();
		d = parseInt(ms[1]);
	}
	var h, n, s;
	h=0; n=0; s=0;
	ms = str.match(/(\d+):(\d+)(?::(\d+))?/);
	if (ms != null) {
		h = parseInt(ms[1]);
		n = parseInt(ms[2]);
		if (ms[3] !== undefined)
			s = parseInt(ms[3]);
	}
	var dt = new Date(y, m, d, h, n, s);
	if (isNaN(dt.getYear()))
		return null;
	return dt;
}

function array_combine(a, b)
{
	var ret = {}
	for (i=0; i<a.length; ++i) {
		ret[a[i]] = b[i];
	}
	return ret;
}

function assert(cond, errmsg)
{
	if (! cond)
		throw errmsg;
}

var JDUtil = {
/**
@fn JDUtil.parseFields(fields) -> [ {name, exists, notNull} ]

- fields: 必须字段列表.

示例：

	fields = ["id", "name"];  // 必须含有id, name两个字段
	fields = ["*id", "name"]; // 必须含有id, name两个字段且id字段必须有非空值
	fields = ["*id", "name", "!pwd"]; // 必须含有id, name两个字段且id字段必须有非空值, 不能包含pwd字段。

*/
	parseFields: function (fields) {
		var ret = [];
		if (fields == null)
			return ret;
		$.each(fields, function (i, e) {
			var one = { name: e, exists: true, notNull: false };
			if (e[0] == "!") {
				one.name = e.substr(1);
				one.exists = false;
			}
			else if (e[0] == "*") {
				one.name = e.substr(1);
				one.notNull = true;
			}
			ret.push(one);
		});
		return ret;
	},

/**
@fn JDUtil.validateRet(ret, expectedCode)

验证callSvrSync调用返回的ret符合筋斗云格式且返回码为expectedCode.
一般使用toJDRet.

	var ret = callSvrSync(ac, ...);
	// JDUtil.validateRet(ret, E_DB);
	expect(ret).toJDRet(E_DB);

 */
	validateRet: function (ret, expectedCode) {
		if (expectedCode == 0) {
			assert(ret !== false, "Expected successful call");
		}
		else {
			assert(ret === false, "Expected fail code to be " + getErrName(expectedCode) + ", actual successful");
			assert(g_data.lastError[0] == expectedCode, "Expected fail code to be " + getErrName(expectedCode) + ", actual " + getErrName(g_data.lastError[0]));
		}
	},

/**
@fn JDUtil.validateTable(ret, fields)

验证callSvrSync调用返回的ret符合筋斗云压缩表格式，如果指定fields，则要求必须出现指定列名。
一般使用toJDTable:

	var ret = callSvrSync(ac, ...); // ret = { h: ["id", "name"], d: [ [ 101, "andy"], [102, "beddy"] ]
	// JDUtil.validateRet(ret, ["id", "name"]);
	expect(ret).toJDRet(["id", "name"]); // 必须含有id, name两个字段
	expect(ret).toJDRet(["*id", "name"]); // 必须含有id, name两个字段且id字段必须有非空值
	expect(ret).toJDRet(["*id", "!pwd"]); // 必须含有id且值为非空, 不能包含pwd字段

 */
	validateTable: function (obj, fields) {
		assert($.isPlainObject(obj) && $.isArray(obj.h) && $.isArray(obj.d), "Expected jdcloud table format `{h, d}`, actual " + JSON.stringify(obj));

		var fieldSpec = this.parseFields(fields);
		var notNullFieldIdxs = [];
		if (obj.d.length > 0) {
			$.each(fieldSpec, function (i, e) {
				var idx = obj.h.indexOf(e.name);
				if (e.exists) {
					assert(idx >=0, "Expected table field `" + e.name + "`");
				}
				else {
					assert(idx <0, "Expected NO table field `" + e.name + "`");
				}
				if (e.notNull)
					notNullFieldIdxs.push(idx);
			});
		}
		var cnt = obj.h.length;
		$.each(obj.d, function (i, e) {
			assert($.isArray(e), "Row " + i + " is NOT an array");
			assert(e.length == cnt, "Expected column count=" + cnt + ", actual count=" + e.length + " (row " + i + ")");
			$.each(notNullFieldIdxs, function (i1, e1) {
				assert(e[e1], "Expect field " + obj.h[e1] + " NOT null (row " + i + ", col " + e1 + ")");
			});
		});
	},
/**
@fn JDUtil.validateList(ret, fields)

验证callSvrSync调用返回的ret符合筋斗云返回的表格式: { list=[obj] }

	var ret = { list: [ {id: 101, name: "andy"}, {id: 102, name: "beddy"} ] };
	JDUtil.validateRet(ret, ["id", "name"]);

 */
	validateList: function (obj, fields) {
		assert($.isPlainObject(obj) && $.isArray(obj.list), "Expected JDList: `{list}`, actual " + JSON.stringify(obj));
		this.validateObjArray(obj.list, fields);
	},

/**
@fn JDUtil.validateObj(ret, fields)

验证ret是对象且包含指定fields。

	var ret = {id: 101, name: "andy"};
	JDUtil.validateObjArray(ret, ["id", "name"]);  // 必须含有id, name两个字段
	JDUtil.validateObjArray(ret, ["*id", "name"]); // 必须含有id, name两个字段且id字段必须有非空值
	JDUtil.validateObjArray(ret, ["*id", "name", "!pwd"]); // 必须含有id, name两个字段且id字段必须有非空值, 不能包含pwd字段。

 */
	validateObj: function (obj, fields) {
		assert($.isPlainObject(obj), "Expected a plain object, actual " + JSON.stringify(obj));

		var fieldSpec = this.parseFields(fields);
		$.each (fieldSpec, function (i, e) {
			if (e.exists)
				assert(obj.hasOwnProperty(e.name), "Expected object to have property: `" + e.name + "`");
			else
				assert(! obj.hasOwnProperty(e.name), "Expected object NOT to have property: `" + e.name + "`");
			if (e.notNull) {
				assert(obj[e.name] != null, "Object property `" + e.name + "` is expected NOT null");
			}
		});
	},
/**
@fn JDUtil.validateObjArray(ret, fields)

验证ret符合对象数组格式，如果指定fields，则要求必须出现指定列名。

	var ret = [ {id: 101, name: "andy"}, {id: 102, name: "beddy"} ];
	JDUtil.validateObjArray(ret, ["id", "name"]);  // 必须含有id, name两个字段
	JDUtil.validateObjArray(ret, ["*id", "name"]); // 必须含有id, name两个字段且id字段必须有非空值
	JDUtil.validateObjArray(ret, ["*id", "name", "!pwd"]); // 必须含有id, name两个字段且id字段必须有非空值, 不能包含pwd字段。

 */
	validateObjArray: function (obj, fields) {
		assert($.isArray(obj), "Expected obj array, actual " + JSON.stringify(obj));
		$.each(obj, function (i, e) {
			JDUtil.validateObj(e, fields);
		});
	}

};

function rs2Array(rs)
{
	var ret = [];
	var colCnt = rs.h.length;

	for (var i=0; i<rs.d.length; ++i) {
		var obj = {};
		var row = rs.d[i];
		for (var j=0; j<colCnt; ++j) {
			obj[rs.h[j]] = row[j];
		}
		ret.push(obj);
	}
	return ret;
}

function list2varr(ls)
{
	var ret = [];
	$.each(ls.split(','), function () {
		if (this.length == 0)
			return;
		ret.push(this.split(':'));
	});
	return ret;
}

/**
@var myMatchers

扩展断言。

在myReporter中已初始化，可直接在it块中使用。

	ret = callSvrSync("param", {name: "id", id: '9a'});
	expect(ret).toJDRet(E_PARAM);

 */
var myMatchers = {
	toJDRet: function (util, testers) {
		return {
			compare: function (actual, expected) {
				var ret = {pass: false, message: null}
				try {
					JDUtil.validateRet(actual, expected);
					ret.pass = true;
					ret.message = "Expected fail code NOT to be " + getErrName(expected);
				}
				catch (ex) {
					ret.message = ex;
				}
				return ret;
			}
		}
	},
	toJDObj: function (util, testers) {
		return {
			// notNull?=false
			compare: function (actual, fields, notNull) {
				var ret = {pass: false, message: null}
				try {
					JDUtil.validateObj(actual, fields, notNull);
					ret.pass = true;
					// ret.message = "Expected NOT an object with keys: " + fields.join(',');
				}
				catch (ex) {
					ret.message = ex;
				}
				return ret;
			}
		}
	},
	toJDTable: function (util, testers) {
		return {
			compare: function (actual, fields) {
				var ret = {pass: false, message: null};
				try {
					JDUtil.validateTable(actual, fields);
					ret.pass = true;
					//ret.message = "Expected NOT JDList";
				}
				catch (ex) {
					ret.message = ex;
				}
				return ret;
			}
		}
	},
	toJDList: function (util, testers) {
		return {
			compare: function (actual, fields) {
				var ret = {pass: false, message: null};
				try {
					JDUtil.validateList(actual, fields);
					ret.pass = true;
					//ret.message = "Expected NOT JDList";
				}
				catch (ex) {
					ret.message = ex;
				}
				return ret;
			}
		}
	}
};

/**
@var myReporter

如果suite中第一个case或标记了g_data.critical=true的case失败，则忽略整个suite中其它case;
如果第一个suite失败，则中止执行。

初始化：

	it ("critical case", function () {
		this.critical = true;
		// 一旦失败则取消suite中其它case执行
		// suite中第一个case自动设置为critical
	});

*/
var myReporter = {
	suiteStarted: function (result, suite) {
		this.specNo_ = 0;
		jasmine.getEnv().addMatchers(myMatchers);
	},
	suiteDone: function (result, suite) {
		if (++this.suiteNo_ == 1 && result.status != "finished")
			jasmine.getEnv().pend();
	},

	specStarted: function (result, spec) {
		if (++ this.specNo_ == 1)
			spec.context.critical = true;
	},
	specDone: function (result, spec) {
		if (spec.context.critical && result.status == "failed") {
			spec.suite.pend();
		}
	},

	specNo_: 0,
	suiteNo_: 0,
};

jasmine.getEnv().addReporter(myReporter);
jasmine.getEnv().throwOnExpectationFailure(true);

function initDaca(self)
{
/**
@fn daca.anyDate()

检测是否为unix timestamp格式或字符串日期格式。

示例：匹配任意日期：

	var ret = 1476115200;
	// var ret = "/Date(1476115200000)/";
	expect(ret).toEqual(daca.anyDate());

示例：匹配指定日期：

	var ret = parseDate("2016-1-1");
	expect(ret).toEqual(daca.anyDate("2016-1-1"));

 */
	self.anyDate = function (dt) {
		return new AnyDate(dt);
	};
	self.AnyDate = AnyDate;
	function AnyDate(dt)
	{
		if (dt != null)
			this.dt = parseDate(dt);
	}
	AnyDate.prototype.asymmetricMatch = function(other) {
		var e = parseDate(other);
		var ret = e != null && e.constructor === Date;
		if (ret && this.dt != null)
			return e - this.dt == 0;
		return ret;
	};
	AnyDate.prototype.jasmineToString = function() {
		if (this.dt != null)
			return "<daca.anyDate(" + formatDate(this.dt) + ")>";
		return '<daca.anyDate>';
	};
}

var daca = {};
initDaca(daca);

