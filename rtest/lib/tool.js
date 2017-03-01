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

var T_HOUR = 3600;
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
		async: false,

		xhrFields: {
			withCredentials: true
		},

		dataFilter: function (data, type) {
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

function formatDate(dt)
{
	return dt.getFullYear() + "-" + (dt.getMonth()+1) + "-" + dt.getDate() + " " + dt.getHours() + ":" + dt.getMinutes() + ":" + dt.getSeconds();
}

function parseDate(str)
{
	if (str == null)
		return null;
	if (str instanceof Date)
		return str;
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

/**
@var myMatchers

扩展断言。应以 toJD 开头。

初始化：

	beforeEach(function() {
		jasmine.addMatchers(myMatchers);
	});

使用：

	ret = callSvrSync("param", {name: "id", id: '9a'});
	expect(ret).toJDCallFail(E_PARAM);

 */
var myMatchers = {
	toJDCallFail: function (util, testers) {
		return {
			compare: function (actual, expected) {
				var ret = {pass: false, message: null}
				if (actual === false) {
					if (g_data.lastError[0] == expected) {
						ret.pass = true;
						ret.message = "Expected fail code NOT to be " + getErrName(expected);
					}
					else {
						ret.message = "Expected fail code to be " + getErrName(expected) + ", actual " + getErrName(g_data.lastError[0]);
					}
				}
				else {
					ret.message = "Expected fail code to be " + getErrName(expected) + ", actual successful";
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

	beforeEach(function() {
		if (g_data.skip)
			pending();
	});

	it ("critical case", function () {
		g_data.critical = true;
		// 一旦失败则取消suite中其它case执行
		// suite中第一个case自动设置为critical
	});

*/
var myReporter = {
	suiteStarted: function (result, suite) {
		this.specNo_ = 0;
	},
	suiteDone: function (result, suite) {
		if (++this.suiteNo_ == 1 && this.skipSuite_)
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
//jasmine.getEnv().addMatchers(myMatchers);
