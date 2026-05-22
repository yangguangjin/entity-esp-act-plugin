# 本地资料索引

本目录保存方案 B（ACT 原生实时跟随实体插件）的关键开发资料快照，目的是减少后续频繁联网查询，并固定当前参考版本。

## ACT 插件开发

- `act/IActPluginV1-interface.md`
  - ACT 插件必须实现的接口。
  - 重点看 `InitPlugin` / `DeInitPlugin`。

- `act/IActPluginV1-InitPlugin.md`
  - 插件启动入口。
  - 用于初始化配置页、服务、overlay、扫描线程。

- `act/IActPluginV1-DeInitPlugin.md`
  - 插件卸载入口。
  - 必须释放 overlay、线程、句柄、HTTP 服务。

- `act/Plugin-Creation-Tips.md`
  - ACT 插件创建说明。
  - 重点：.NET Framework 4.x、引用 `Advanced Combat Tracker.exe`、插件 DLL 组织方式。

## FFXIV_ACT_Plugin

- `ffxiv-act-plugin/README-page.md`
  - FFXIV_ACT_Plugin 项目说明。
  - 重点：插件读取内存与网络数据；网络数据来源包括 raw socket、Npcap、Deucalion。

- `ffxiv-act-plugin/releases.md`
  - 发布记录快照。
  - 用于观察游戏更新后 memory signatures、logline、network opcode 等变化。

## OverlayPlugin

- `overlayplugin/devs.md`
  - OverlayPlugin 开发说明。
  - 重点：HTML overlay、`common.js`、`addOverlayListener`、`callOverlayHandler`、`getCombatants` 等。

- `overlayplugin/event_types.md`
  - OverlayPlugin 事件类型。
  - 用于了解 overlay 能直接收到哪些事件。

- `overlayplugin/faq.md`
  - 常见问题。
  - 重点：memory signatures/addresses 报错通常代表 parser 不支持当前游戏版本；Fullscreen 独占不适合普通 overlay。

- `overlayplugin/setup.md`
  - 安装设置资料。
  - 便于复查 ACT/OverlayPlugin 基础环境。

## Windows 透明 Overlay

- `windows-overlay/extended-window-styles.md`
  - Win32 扩展窗口样式。
  - 重点：`WS_EX_LAYERED`、`WS_EX_TRANSPARENT`、`WS_EX_TOPMOST`、`WS_EX_NOACTIVATE`。

- `windows-overlay/SetWindowLongPtr.md`
  - 设置窗口扩展样式的 Win32 API。
  - 用于实现点击穿透/置顶/不抢焦点。

- `windows-overlay/dwm-blur-overview.md`
  - DWM 透明/玻璃相关资料。
  - 可用于 overlay 原型参考。

## 维护建议

游戏更新后优先查看：

1. `ffxiv-act-plugin/releases.md`
2. `overlayplugin/faq.md`
3. `DEVELOPMENT-RESOURCES.md`

如果出现实体读取失效或投影错位，优先检查方案 B 中的：

- `CameraReader`
- `EntityMemoryReader`
- `Offsets`
- `Signatures`
- `ProjectionService`

## 注意

这些文件是资料快照，不保证永远最新。正式开发时，若遇到版本更新导致失效，需要重新联网检查上游 release / issue，并更新本目录快照。
