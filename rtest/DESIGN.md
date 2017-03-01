# 筋斗云框架自动测试接口设计

## 测试工具函数

测试接口：

	fn(f, ...) -> { ret }

参数：

- f: 函数名
- 其它参数见该函数的参数表。

### 获取参数

服务端实现：

	param(name, defVal?, coll?)
	mparam(name, coll?)

- name: 指定参数名，其中可以含有类型标识，如"cnt/i", "wantArray/b"等。
- coll: 'G' 表示GET参数, 'P'表示POST参数，不指定表示GET/POST均可。

### 数据库函数

服务端实现：

	queryAll(sql)
	queryOne(sql)
	execOne(sql, getNewId?=false)

- getNewId: 为true则返回INSERT语句执行后新得到的自增id

## 函数型接口

实现下列接口，并定义用户登录权限。

	login(uname, pwd) -> {id}
	whoami() -> {id}
	logout

其中login接口应初始化权限；whoami接口检查权限：

	whoami

	- 权限：AUTH_USER
	- 返回与login相同的内容。

	
## 对象型接口

数据表

@ApiLog: id, ac, tm, addr, ua

### 基本CRUD

	ApiLog.add()(ac, tm?, addr?) -> id
	ApiLog.add(res)(ac, tm?, addr?=test_addr) -> {按res指定返回}

	ApiLog.get(id) -> {id, ...}
	ApiLog.get(id, res) -> {按res指定返回}

	ApiLog.set(id)(fields...)

应用逻辑

- 权限: AUTH_GUEST
- 必填字段：ac
- 只读字段：ac,tm
- 隐藏字段：ua

