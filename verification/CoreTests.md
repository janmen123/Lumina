# 当前版本测试入口

使用 `RunRegression.ps1` 测试当前编译的 Lumina.exe；`RunCoreTests.ps1` 和 `RunPortableTests.py` 转发到相同入口。旧 CoreTests.cs、CoreTestsHarness.cs 以及带旧版本说明的文件仅保留为历史参考，不作为当前通过依据。

运行：`powershell -NoProfile -File verification\RunRegression.ps1 -Binary <Lumina.exe 的完整路径>`。

测试会验证临时数据目录隔离后才写文件，包含真实生产程序集的持久化、损坏恢复、删除记录、原子替换失败、拖动排序、窗口匹配和暂停规则、诊断脱敏与导出。不会对真实桌面应用注入点击或切换窗口。

`Lumina.exe --test-logic` 是纯逻辑检查；`--self-test` 会创建自己的 WPF 测试窗口。未人工验证的第三方应用行为、全屏游戏、多屏缩放、Explorer 重启需要独立验收。
