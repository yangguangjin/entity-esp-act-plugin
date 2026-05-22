# 开发资料与维护注意事项

## 1. 关键结论

方案 B 是 ACT 原生插件路线：不依赖卫月/Dalamud，但要实现实时跟随实体文字，必须自行解决以下问题：

- ACT 插件生命周期和配置页。
- 实体列表、坐标、读条、BNpcID、OwnerID 数据采集。
- 相机矩阵 / 投影矩阵 / viewport 获取。
- `WorldToScreen` 世界坐标到屏幕坐标投影。
- 透明置顶、鼠标穿透 overlay 窗口绘制。
- 游戏更新后内存签名、偏移、结构维护。

严格来说，ACT 日志和 OverlayPlugin 现有 overlay 能提供大量战斗数据，但通常不能直接给“实体在屏幕上的坐标”。要实现文字贴实体头顶，核心工作是 `CameraReader + WorldToScreen`。

## 2. 官方/社区资料

### 2.1 ACT 插件开发接口

资料：

- ACT 插件接口文档：`https://advancedcombattracker.com/apidoc/html/T_Advanced_Combat_Tracker_IActPluginV1.htm`
- ACT 插件创建提示：`https://github.com/EQAditu/AdvancedCombatTracker/wiki/Plugin-Creation-Tips`

要点：

- ACT 插件是 .NET Framework 4.x assembly。
- 插件必须实现 `Advanced_Combat_Tracker.IActPluginV1`。
- 接口核心方法：
  - `InitPlugin(...)`：ACT 启动插件时调用。
  - `DeInitPlugin()`：ACT 卸载插件时调用，必须释放资源。
- Visual Studio 项目需要引用 `Advanced Combat Tracker.exe`。
- DLL 插件比源码插件更适合本项目，因为本项目需要 overlay、渲染、配置、可能还有 native/Win32 调用。

实现注意：

- 插件只保留一个实现 `IActPluginV1` 的类。
- `DeInitPlugin` 必须停止扫描线程、HTTP 服务、overlay 窗口、释放句柄。
- 不要把第三方 DLL 丢到 ACT 根目录污染其他插件，优先使用插件子目录并处理 `AssemblyResolve`。

### 2.2 FFXIV_ACT_Plugin

资料：

- FFXIV ACT Plugin：`https://github.com/ravahn/FFXIV_ACT_Plugin`

要点：

- FFXIV_ACT_Plugin 读取本机内存和网络数据。
- 它支持多种网络数据来源：raw socket、Npcap、Deucalion named pipe。
- 上游仓库说明该插件源码目前不公开，仓库主要跟踪 release 和 issue。
- 插件更新常与 FF14 游戏版本、网络 opcode、内存结构、签名有关。

对本项目的意义：

- 可以尽量复用 FFXIV_ACT_Plugin / OverlayPlugin 暴露的数据。
- 但如果需要实时投影和屏幕坐标，大概率仍要自行读取相机矩阵。
- 游戏更新后，如果 FFXIV_ACT_Plugin 都报 memory signature/address 错误，本插件的 CameraReader/EntityScanner 也需要重点检查。

### 2.3 OverlayPlugin 开发资料

资料：

- OverlayPlugin dev 文档：`https://overlayplugin.github.io/OverlayPlugin/devs/`
- OverlayPlugin FAQ：`https://overlayplugin.github.io/docs/faq/`

要点：

- OverlayPlugin 的 overlay 本质是网页，使用 HTML/CSS/JS。
- `common.js` 提供 `addOverlayListener`、`callOverlayHandler`、`startOverlayEvents`。
- 文档提到一些 handler，包括 `getCombatants`，但部分接口文档不完整。
- FAQ 明确：如果出现 memory signatures/addresses 报错，通常表示 parser 不支持当前游戏版本，需要更新 FFXIV_ACT_Plugin。
- OverlayPlugin 建议游戏使用 Borderless 模式；Fullscreen 独占会导致普通窗口 overlay 不能绘制在游戏上方。

对本项目的意义：

- 如果只是做实体表，可以做 OverlayPlugin HTML overlay。
- 如果要实体头顶文字，HTML overlay 仍需要屏幕坐标；这部分不是 OverlayPlugin 标准能力的稳定输出。
- 本项目可以参考 OverlayPlugin 的窗口覆盖经验，但推荐自己创建 native overlay 以获得更稳定的绘制和点击穿透控制。

### 2.4 透明置顶点击穿透窗口

资料方向：

- Win32 extended styles：`WS_EX_LAYERED`、`WS_EX_TRANSPARENT`、`WS_EX_TOPMOST`、`WS_EX_NOACTIVATE`。
- 可用 C# P/Invoke 设置 `GetWindowLongPtr` / `SetWindowLongPtr`。
- 渲染技术可选 Direct2D、SkiaSharp、ImGui.NET、WPF。

建议：

- P0 原型可以用 WinForms/WPF 快速验证。
- 长期方案建议 Direct2D + DirectWrite 或 SkiaSharp。
- overlay 应覆盖游戏 client rect，不是整个窗口外框。
- 必须处理 DPI 缩放和多显示器。

## 3. 游戏更新时如何维护

### 3.1 哪些东西最容易在游戏更新后坏

按风险排序：

1. 相机矩阵签名/偏移
   - `ViewMatrix`
   - `ProjectionMatrix`
   - `ViewProjectionMatrix`
   - Camera object pointer

2. 对象表结构
   - 实体列表基址
   - 实体结构字段偏移
   - Position / Heading / HitboxRadius
   - OwnerID / BNpcID / BNpcNameID
   - CastID / CastCurrent / CastMax

3. 网络 opcode / ACT 日志字段
   - `03`、`14`、`105` 等行可能增加字段或含义变化。
   - CN/国际服版本可能不同。

4. Overlay 窗口坐标
   - 游戏窗口模式变化。
   - DPI 设置变化。
   - 多显示器位置变化。

5. FFXIV_ACT_Plugin / OverlayPlugin 依赖更新
   - 上游插件更新后接口行为可能变化。
   - 旧版本 parser 可能无法连接游戏。

### 3.2 版本更新后的检查清单

每次游戏大版本、热修、国服客户端更新后，按顺序检查：

```text
1. FFXIV_ACT_Plugin 是否能正常工作
   - ACT FFXIV Settings → Test game connection
   - 无 memory signature/address 报错
   - ACT 主界面能看到战斗数据

2. EntityScanner 是否能读实体
   - 能看到自己
   - 能看到当前目标
   - 能看到 BattleNpc
   - BNpcID / BNpcNameID 是否合理
   - Position 是否随移动变化

3. CameraReader 是否有效
   - Matrix 是否非零
   - Viewport 是否正确
   - CameraPosition 是否随视角/位置变化

4. WorldToScreen 是否准确
   - 自己脚下/头顶投影位置正确
   - 当前目标文字贴近头顶
   - 转动视角时不漂移
   - 实体到屏幕外时隐藏

5. 读条字段是否正确
   - CastID 正确
   - CastCurrent 增长
   - CastMax 正确

6. 过滤是否正常
   - 队友玩家过滤
   - OwnerID 过滤
   - 黑白名单

7. Overlay 是否正常
   - 透明
   - 置顶
   - 鼠标穿透
   - 不抢焦点
   - DPI/窗口模式无偏移
```

### 3.3 维护策略

#### 3.3.1 把不稳定代码隔离

必须把高风险代码集中在少数模块：

```text
CameraReader.cs
EntityMemoryReader.cs
Offsets.cs
Signatures.cs
ProjectionService.cs
```

其他模块不要直接依赖裸偏移。

原因：

- 游戏更新时只需要集中修这几个模块。
- 过滤、渲染、配置不应受偏移变化影响。

#### 3.3.2 版本化偏移与签名

建议：

```csharp
public sealed class GameVersionProfile
{
    public string Version { get; set; } = "";
    public SignatureSet Signatures { get; set; } = new();
    public OffsetSet Offsets { get; set; } = new();
}
```

配置：

```text
profiles/
  cn-7.3.json
  cn-7.4.json
  global-7.3.json
```

原因：

- 国服/国际服版本不同。
- 热修可能只改部分偏移。
- 方便回滚。

#### 3.3.3 优先签名扫描，少写硬编码基址

建议：

- 用 signature/pattern scan 找关键结构。
- 签名命中后再加相对偏移。
- 日志输出签名命中地址和模块基址。

原因：

- ASLR 会让模块基址变化。
- 纯硬编码地址不可维护。

#### 3.3.4 增加自检页面

ACT 插件配置页增加 `Diagnostics`：

```text
Game process found: yes/no
Game version: xxx
Entity table valid: yes/no
Camera matrix valid: yes/no
Viewport: x,y,w,h
Self entity: id / pos
Current target: id / pos / projected xy
Last projection error: xxx
```

原因：

- 用户反馈“文字不准”时，能快速定位问题。
- 判断是实体读取坏、矩阵坏、投影坏，还是 overlay 窗口偏移。

#### 3.3.5 保留调试日志

日志文件：

```text
%APPDATA%/Advanced Combat Tracker/Config/EntityEspPlugin/logs/latest.log
```

记录：

- 插件加载版本。
- 游戏进程信息。
- 签名扫描结果。
- 矩阵有效性。
- 投影测试结果。
- 异常堆栈。

### 3.4 游戏更新后常见故障与判断

#### 情况 A：完全没有实体

可能原因：

- FFXIV_ACT_Plugin 未工作。
- Entity table 签名失效。
- 插件没找到游戏进程。

检查：

- ACT FFXIV Settings 的 test connection。
- Diagnostics 的 Entity table valid。
- 日志中的签名扫描结果。

#### 情况 B：有实体，但文字位置全错

可能原因：

- Camera matrix 错。
- 矩阵乘法顺序错。
- 坐标轴高度轴用错。
- viewport/DPI 错。

检查：

- 当前目标投影。
- 自己位置投影。
- 屏幕中心和 viewport。
- 转动视角时投影变化是否合理。

#### 情况 C：文字有固定偏移

可能原因：

- overlay 对齐的是窗口外框，不是 client rect。
- DPI 缩放没处理。
- 多显示器坐标原点错。

检查：

- GameWindowClientRect。
- DPI scale。
- overlay window bounds。

#### 情况 D：读条不显示

可能原因：

- Cast 字段偏移失效。
- 只读了日志开始，没有内存读条进度。
- 过滤规则把实体过滤了。

检查：

- ACT `14` 日志是否存在。
- EntitySnapshot 的 CastId / CastCurrent / CastMax。
- FilterReason。

#### 情况 E：队友技能实体太多

可能原因：

- OwnerID 读取失败。
- 黑名单不足。
- 队友技能实体没有 OwnerID。

处理：

- 打开 ShowFilterReason。
- 记录实体 BNpcID / BNpcNameID / EObjNameID。
- 加入副本/职业黑名单。

## 4. P0 开发资料优先级

如果后续开始写代码，建议先按这个顺序查资料和验证：

1. ACT `IActPluginV1` 最小插件加载。
2. 透明置顶 click-through overlay 原型。
3. 获取游戏窗口 client rect 和 DPI。
4. 获取实体列表中当前目标或自己坐标。
5. 读取相机矩阵。
6. WorldToScreen 当前目标。
7. 文本跟随当前目标。
8. 再扩展到全实体列表。

不要一开始就做完整过滤/UI/HTTP 联动。

## 5. 参考链接

- ACT IActPluginV1：`https://advancedcombattracker.com/apidoc/html/T_Advanced_Combat_Tracker_IActPluginV1.htm`
- ACT Plugin Creation Tips：`https://github.com/EQAditu/AdvancedCombatTracker/wiki/Plugin-Creation-Tips`
- FFXIV_ACT_Plugin：`https://github.com/ravahn/FFXIV_ACT_Plugin`
- OverlayPlugin dev：`https://overlayplugin.github.io/OverlayPlugin/devs/`
- OverlayPlugin FAQ：`https://overlayplugin.github.io/docs/faq/`
