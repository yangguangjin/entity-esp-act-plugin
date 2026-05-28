# Entity ESP ACT Plugin

Entity ESP ACT Plugin 是一个用于 FF14 / ACT 的实体调试与 Overlay 插件。它在 ACT 插件页中提供配置界面，并通过透明 overlay 显示游戏内实体信息、读条信息、相关 ACT 日志和 VFX 观察结果，方便打本时快速编写和验证 Triggernometry / PictoACT 触发器。

## 作者联系方式

- 作者：光进不出
- QQ：1115284886

## 功能概览

- 实体 Overlay：在游戏窗口上显示实体标签、距离、BNpc、BNpcName、EObjName 等调试字段。
- 读条观察：支持显示内存读条条形和 ACT 14 日志读条进度条。
- ACT 日志关联：可采集并显示实体相关 ACT 日志，支持不同日志类型独立开关和简化显示。
- 实体生命周期：可按 ACT 日志活动给实体标签续期，默认 15 秒无活动后自动隐藏，减少短命机制物残留。
- 右侧日志面板：适合录屏分析，把近期实体相关日志固定显示在右侧。
- VFX 监控面板：在左上角显示近期 VFX 路径，并支持复制 Triggernometry 复现片段。
- 样式配置：支持背景块、文字、读条条形、锚点十字等颜色和透明度配置。
- 独立诊断工具：`tools/EntityEspProbe` 用于实体抓取、签名验证、VFX 候选观察等重型诊断，避免拖慢 ACT 插件页。

## 适用场景

- 打本时观察 Boss、场地物件、召唤物、不可选中实体等对象。
- 编写 Triggernometry 触发器时快速确认 EntityId、BNpcId、BNpcNameId、日志字段和 VFX 路径。
- 调试 PictoACT Omen、StaticVfx、ActorVfx、Channeling 等复现参数。
- 游戏更新后通过控制台 probe 快速定位实体表、相机、签名和 VFX 数据变化。

## 项目结构

```text
EntityEspActPlugin.sln
src/
  EntityEspActPlugin.Act/      ACT 插件入口、配置 UI、Overlay 窗口
  EntityEspActPlugin.Core/     实体读取、过滤、投影、日志、VFX 等核心逻辑
tests/
  EntityEspActPlugin.Tests/    轻量测试项目
tools/
  EntityEspProbe/              独立控制台诊断工具
docs/                          配置说明、维护说明、VFX/TRN 文档
lib/                           本地引用程序集说明，不提交私有 DLL
```

## 环境要求

- Windows
- .NET SDK，需能构建 `net48` 项目
- .NET Framework 4.8 Developer Pack / Targeting Pack
- Advanced Combat Tracker
- FF14 客户端

ACT 引用程序集需要放在本地：

```text
lib/Advanced_Combat_Tracker.dll
```

该 DLL 只用于本地编译，已被 `.gitignore` 排除，不随仓库分发。

## 构建

在仓库根目录执行：

```bash
dotnet build EntityEspActPlugin.sln -c Release
```

构建成功后，插件 DLL 位于：

```text
src/EntityEspActPlugin.Act/bin/Release/net48/EntityEspActPlugin.Act.dll
```

## 打包与发布

版本号统一维护在 `Directory.Build.props` 的 `<Version>` 字段，发布 tag 必须使用同版本的 `vX.Y.Z` 格式，例如当前版本 `0.1.2` 对应 tag `v0.1.2`。

本地生成可发布 zip：

```bash
build/package-release.sh
```

生成产物位于：

```text
artifacts/release/EntityEspActPlugin-vX.Y.Z.zip
```

发布到 GitHub：

```bash
git tag vX.Y.Z
git push origin vX.Y.Z
```

发布包必须在能访问真实 `lib/Advanced_Combat_Tracker.dll` 的环境构建，不能用 `ENTITY_ESP_ACT_STUBS` 构建结果发布；否则 ACT 会提示“该程序集没有实现ACT插件接口的类”。

推送 tag 后，GitHub Actions 会执行 Release 流程；如果 CI 环境没有提供真实 ACT 引用，打包步骤会失败，避免上传 ACT 无法加载的 stub 包。可在本机执行 `build/package-release.sh` 生成 zip 后，用 GitHub Release 页面或 `gh release upload vX.Y.Z artifacts/release/EntityEspActPlugin-vX.Y.Z.zip --clobber` 上传。若 tag 与 `Directory.Build.props` 中的版本不一致，打包步骤会失败，避免错误版本发布。

## 安装到 ACT

1. 打开 ACT。
2. 进入 `Plugins` 页面。
3. 点击 `Browse...`。
4. 选择 Release 构建出的 `EntityEspActPlugin.Act.dll`。
5. 点击 `Add/Enable Plugin`。
6. 在插件页确认出现 `Entity ESP`。

更新 DLL 后建议重启 ACT，确保 ACT 加载的是最新代码。

## 基础使用

1. 在插件页面勾选 `启用 overlay`。
2. `DataSource` 实战使用 `Real`，样式测试可使用 `Mock`。
3. 按需要开启实体标签字段，例如 `EntityId`、`BNpcId`、`BNpcName`、距离。
4. 打本写触发器时，可开启实体旁 ACT 日志或右侧固定日志面板。
5. 需要观察 VFX 时，开启 `启用 VFX 监控与左上角列表`，然后使用 `复制最近 VFX 的 TRN 复现片段`。

更多字段说明见：`docs/configuration-guide.md`。

## 推荐配置场景

### 实战低遮挡

```text
DataSource=Real
启用 overlay=true
背景透明度=0 或较低数值
实体扫描 Hz=30
渲染 FPS=60
只开启当前需要的标签字段
```

适合打本时临时观察实体，不明显遮挡游戏画面。

### 写 Triggernometry

```text
启用实体相关 ACT 日志采集=true
实体旁显示 ACT 日志=true
右侧固定日志面板(录屏分析)=true
按需开启 03/04/14/15/16/1A/1E/23/2A 等日志类型
```

适合快速确认日志字段、施法者、目标、状态、头标、拉线、出生/消失等信息。

### VFX 观察

```text
启用 VFX 监控与左上角列表=true
VFX 保留窗口秒数=30
VFX NEW高亮秒数=5
VFX 最大显示行数=8
```

适合观察机制触发时出现的 `.avfx` 路径，并复制 TRN 复现片段。

## 诊断与维护

ACT 插件页的 Diagnostics 只做轻量环境检查，例如 ACT 路径、FF14 路径、进程状态、游戏窗口坐标等。

重型工作请使用控制台工具：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -c Release -- --help
```

建议把以下工作放到 `EntityEspProbe`：

- 实体表和相机定位。
- 签名验证。
- 大范围内存扫描。
- 队伍来源验证。
- VFX 候选采集和分析。

这样做可以避免 ACT UI 卡顿，也方便在不开 ACT 插件页的情况下快速迭代。

## 文档

- `docs/configuration-guide.md`：插件页面配置说明和使用示例。
- `docs/game-update-maintenance.md`：游戏更新后的维护检查流程。
- `docs/trn-vfx-replay.md`：Triggernometry VFX 复现说明。
- `docs/vfx-active-instance-research.md`：VFX 活跃实例观察记录。

## 注意事项

- 本插件用于本机调试和触发器编写辅助，不会让其他玩家看到标记。
- 游戏更新后，实体、相机或 VFX 相关内存结构可能变化，需要重新验证。
- 如果 overlay 或 ACT 插件页卡顿，优先降低渲染 FPS、实体扫描 Hz，并关闭不需要的日志类型。
- 不建议在 ACT UI 线程中加入全内存扫描或全 `.text` 扫描。

## 开源协议

本项目使用 MIT License，详见 `LICENSE`。
