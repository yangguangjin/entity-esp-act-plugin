# Entity ESP ACT Plugin Notes

本文只记录当前 Entity ESP ACT Plugin 项目的开发和维护注意事项。

## 项目定位

Entity ESP ACT Plugin 是 FF14 / ACT 的实体观察与调试插件，用于在游戏窗口上显示实体、读条、相关 ACT 日志和 VFX 信息，辅助编写 Triggernometry / PictoACT 触发器。

## 开发原则

- ACT 插件页保持轻量，避免在 UI 线程执行大范围内存扫描。
- Overlay 渲染和实体扫描频率要可控，优先保证 ACT 和游戏响应速度。
- 真实内存定位、签名验证、实体抓取、VFX 候选分析优先放到 `tools/EntityEspProbe`。
- 插件 DLL 继续面向 `net48`，方便 ACT 直接加载。
- 尽量保持单 DLL 插件部署，减少 ACT 依赖程序集探测问题。

## 本地构建

```bash
dotnet build EntityEspActPlugin.sln -c Release
```

构建输出：

```text
src/EntityEspActPlugin.Act/bin/Release/net48/EntityEspActPlugin.Act.dll
```

## ACT 引用程序集

本地编译需要 ACT 引用程序集：

```text
lib/Advanced_Combat_Tracker.dll
```

该 DLL 不提交到 GitHub，只保留 `lib/README.md` 说明。

## 验证建议

- 文档改动后至少确认 `git diff` 内容正确。
- 代码或项目文件改动后执行 Release 构建。
- 涉及 overlay 或 ACT 插件页时，构建后复制 DLL 到 ACT 插件目录并重启 ACT 验证。
- 涉及内存读取、签名或 VFX 扫描时，优先用 `EntityEspProbe` 单独验证。

## 开源信息

- 作者：光进不出
- QQ：1115284886
- License：MIT
