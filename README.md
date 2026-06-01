# Entity ESP ACT Plugin

Entity ESP ACT Plugin 是一个用于 FF14 / ACT 的实体调试与 Overlay 插件。它在 ACT 插件页中提供配置界面，并通过透明 overlay 显示游戏内实体信息、读条信息、相关 ACT 日志和 VFX 观察结果，方便打本时快速编写和验证 Triggernometry / PictoACT 触发器。

## 作者联系方式

- 作者：光进不出
- QQ：1115284886

## 功能概览

- 实体 Overlay：在游戏窗口上显示实体标签、距离、HP、坐标、BNpc、BNpcName、EObjName 等调试字段。
- 读条观察：支持显示内存读条条形和 ACT 14 日志读条进度条。
- ACT 日志关联：可采集并显示实体相关 ACT 日志，支持不同日志类型独立开关和简化显示。
- 实体生命周期：可按 ACT 日志活动给实体标签续期，默认 15 秒无活动后自动隐藏，减少短命机制物残留。
- 右侧日志面板：适合录屏分析，把近期实体相关日志固定显示在右侧。
- VFX 实时内存面板：插件内置管理的独立 VFX 面板；通过配置显示/隐藏，按 `VFX 内存采样 Hz` 后台遍历 FF14 `Scene.World` 的 active `VfxObject`，上方显示当前实时存活 VFX，下方用历史日志区保留首次观测快照、位置、距离、caster/target；面板工具栏支持复制全部、复制选中/当前行、复制去重路径列表和暂停刷新。
- 样式配置：支持背景块、文字、读条条形、锚点十字等颜色和透明度配置；配置 UI 已按基础、标签、日志、VFX、样式、名单和诊断等功能分组，并用标题横线分隔。
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

版本号统一维护在 `Directory.Build.props` 的 `<Version>` 字段，发布 tag 必须使用同版本的 `vX.Y.Z` 格式，例如当前版本 `0.1.6` 对应 tag `v0.1.6`。

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
3. 按需要开启实体标签字段，例如 `EntityId`、HP、坐标、`BNpcId`、`BNpcName`、距离。
4. 打本写触发器时，可开启实体旁 ACT 日志或右侧固定日志面板。
5. 需要观察 VFX 时，在配置页开启 `显示 VFX 监控面板`；面板随插件/配置自动启停，按 `VFX 内存采样 Hz` 后台读取 `Scene.World` active VFX，上方实时区看当前存活，下方历史区回看首次观测记录；复制时优先用面板工具栏，手动框选前可勾选 `暂停刷新`。

更多字段说明见：`docs/configuration-guide.md`。

## 推荐配置场景

### 实战低遮挡

```text
DataSource=Real
启用 overlay=true
背景透明度=0 或较低数值
实体扫描 Hz=30
渲染 FPS=60
VFX 内存采样 Hz=10
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
显示 VFX 监控面板=true
VFX 最远显示距离=100
VFX 日志式显示秒数=30
VFX 最大显示条数=12
VFX 内存采样 Hz=10
VFX 短命判定秒数 N=2
VFX 短命续显秒数 M=15
渲染 FPS=60 或 165
```

适合观察机制触发时当前存活或刚刚消失的 `.avfx` 实例。当前实现不再用“扫描一次 sleep 一次”的 PathScanFallback 面板，也不在 WinForms 面板 UI tick 中同步做重型内存遍历；`ActiveVfxMemoryService` 按 `VFX 内存采样 Hz` 在后台读取 `Client::Graphics::Scene::World`，遍历 active `VfxObject`，解析 `VfxResourceInstance -> ResourceHandle.FileName`，VFX 面板 UI 最多 30 FPS 读取上一帧缓存并刷新文本。面板分成两块：上方 `========== 当前实时存活 VFX ==========` 保持内存实时扫描语义，距离和 position 会按采样时玩家坐标刷新；下方 `========== 历史 VFX 日志 ==========` 记录首次观测快照，距离和 position 不会因为玩家移动而被覆盖。active VFX 离开 `Scene.World` 后会像日志一样默认保留 30 秒；如果某个 VFX 从首次到末次 active 小于等于 N 秒，则在普通 30 秒窗口后额外续显 M 秒，面板标记为 `HOLD`，方便看清一闪而过的短命特效。距离过滤按玩家当前位置计算；面板工具栏可直接复制全部、复制选中/当前行、复制去重 `.avfx` 路径列表，并可暂停刷新方便手动框选。runtime 不再把第一个 VFX vtable 当成唯一白名单，未知 vtable 会周期性短 TTL 重试，减少进入副本后只剩环境 VFX、不捕获新机制 VFX 的情况；如果 FF14 结构或签名失效，先用 `tools/EntityEspProbe -- probe-vfx-world` 验证。

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
- 如果 overlay、VFX 面板或 ACT 插件页卡顿，优先降低渲染 FPS、实体扫描 Hz、VFX 内存采样 Hz，并关闭不需要的日志类型。
- 不建议在 ACT UI 线程中加入全内存扫描或全 `.text` 扫描。

## 开源协议

本项目使用 MIT License，详见 `LICENSE`。
