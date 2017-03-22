# 筋斗云回归测试框架

## 使用

先在rtest.html中修改g_cfg.url对应后端服务基础地址，然后在浏览器打开该文件即可测试。

- DESIGN.md是测试用例相关的接口设计。
- rtest.js是所有的测试用例定义。

特性：

- 如果第一个用例或标记为critical的用例失败，整个suite失败，其它用例直接忽略。
- 如果第一个suite失败，则测试中止，忽略其它测试。
- 运行过程中，可以手工点击"Stop"按钮停止测试。


## 开发测试用例

核心使用jasmine库，是一个以BDD(行为驱动开发)为理念的测试框架。

Jasmine is a behavior-driven development framework for testing JavaScript code. 

这里用到的jasmine是在版本2.5.2基础上增加了一些修改(lib/jasmine-2.5.2)，以更好地支持对后端Web接口的测试。
查看这些修改可在jasmine源码中搜索LJ标识。

lib/tool.js是对筋斗云后端的通用支持，定义了常用的 callSvr/callSvrSync函数，toJDTable/toJDList/toJDObj断言等。

## 参考

- jasmine首页：https://jasmine.github.io/
- jasmine文档：https://jasmine.github.io/2.5/introduction
