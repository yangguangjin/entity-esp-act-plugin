# VFX active instance 调研与 probe 记录

目标：从“扫描内存中的 `.avfx` 字符串”升级到“当前活跃 VFX 实例：path / caster / target / owner / position”。

## 已验证的本地现象

### 字符串扫描

当前插件内置 `.avfx` 字符串扫描能得到真实 path，但需要扫描 FF14 大量 readable memory，现场可到 5-6GB。

优点：稳定、无需知道结构。

缺点：
- 延迟明显。
- 只能知道资源 path。
- 不知道 active / owner / position / caster / target。
- 已加载但未播放的 path 与正在播放实例无法区分。

### `probe-character-vfx`

命令：

```bash
tools/EntityEspProbe/bin/Debug/net48/EntityEspProbe.exe probe-character-vfx 100
```

当前验证：
- `Character + 0x1988 -> VfxContainer` 可读。
- `VfxContainer + 0x18 -> VfxData[14]` 多数 Player/BattleNpc slot 为 0。
- 只观察到一个 EventNpc `slot[8]` 非零。

结论：
- `Character.VfxContainer` 不能作为主入口。
- 它可能只覆盖特定 actor-bound / omen / tether / channeling 类 VFX。
- 不能保证覆盖场地与 Boss 技能 active VFX。

### `probe-vfx-object`

命令：

```bash
tools/EntityEspProbe/bin/Debug/net48/EntityEspProbe.exe probe-vfx-object 220 40
```

当前验证：
- 盲扫 heap 上像 `VfxObject` 的结构噪声很大。
- 输出大量连续地址、path unresolved，属于 false positive。

结论：
- 不能靠“位置合理 + resourceInstance 指针形状”盲扫 `VfxObject`。
- 需要从真实创建函数返回的 `VfxObject*` 或对象列表/root 出发。

### `probe-vfx-world`

命令：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -c Release -- probe-vfx-world 8000 80
```

当前验证：
- `Client::Graphics::Scene::World.Instance` 签名 `48 8B 05 ?? ?? ?? ?? 48 8B 50 40` 命中 1 次。
- 现场解析到 `Scene.World` 指针，并从 `World + 0x30 / +0x40` 找到 scene root。
- 遍历 `ChildObject` / `NextSiblingObject` 图时，现场访问约 2700 个 scene object，可解析约 15 个 active VFX。
- 可稳定读取 `VfxObject` vtable、position、caster/target、`VfxResourceInstance` 和 `.avfx` path。
- 当前现场 VfxObject vtable 示例：`0x7FF66E88DDD0`；不同游戏版本/进程基址会变化，runtime 不硬编码该绝对地址。已确认能解析 `.avfx` 的 vtable 会被缓存为稳定 VFX 类型；未知 vtable 仍会尝试解析，失败后只短暂抑制，避免副本中新增 VFX 子类被首个环境 VFX vtable 挡住。

结论：
- 这是当前 ACT 插件 runtime 的主入口。
- 插件不再用 `.avfx` 全内存字符串扫描驱动 VFX 面板。
- VFX 面板 UI tick 只读取上一帧缓存；后台 `VfxSnapshotSampler` 按 `VfxSampleHz` 直接读取 `Scene.World` active VFX graph，并按玩家位置做距离过滤。

### `probe-vfx-chain`

命令：

```bash
addr=$(tools/EntityEspProbe/bin/Debug/net48/EntityEspProbe.exe scan-avfx-memory m0532sp_03c0x | sed -n 's/^  \(0x[0-9A-Fa-f]*\) .*/\1/p' | head -1)
tools/EntityEspProbe/bin/Debug/net48/EntityEspProbe.exe probe-vfx-chain "$addr"
```

当前验证：
- 即时 path 地址可读，例如 `vfx/monster/m0532/eff/m0532sp_03c0x.avfx`。
- 但 direct pointer refs 为 0。

结论：
- 扫描到的 path 字符串很可能是缓存/字符串池/资源包副本，不是 `ResourceHandle.FileName` 的直接 std::string buffer。
- 从 path 字符串地址反推 `ResourceHandle -> VfxResourceInstance -> VfxObject` 不可靠。

## 公开资料调研结论

最有价值资料来自：
- `0ceal0t/Dalamud-VFXEditor`
- `aers/FFXIVClientStructs`
- `OverlayPlugin/cactbot` LogGuide
- `perchbirdd/ResLogger2`

核心结论：
- 不应继续依赖全内存 `.avfx` 字符串扫描。
- active VFX 最靠谱路线是捕获 VFX 创建/移除函数。
- `ActorVfxCreate` 在创建瞬间能拿到 path、caster/target 参数，并返回/关联 `VfxObject*`。
- `StaticVfxCreate/Run/Remove` 用于场地/静态 VFX。
- `VfxObject` offset 足够读 position、caster/target id、resource instance。

## 当前客户端签名扫描结果

命令：

```bash
tools/EntityEspProbe/bin/Debug/net48/EntityEspProbe.exe probe-vfx-functions
```

当前结果：

```text
ActorVfxCreate hits=1 first=0x7FF6B1874980
ActorVfxRemove hits=1 first=0x7FF6B13AFDCC
StaticVfxRun hits=1 first=0x7FF6B18EA23D resolved=0x7FF6B147BE80 module+0x45BE80
StaticVfxRemove hits=1 first=0x7FF6B1479CD0
VfxObjectCreate hits=1 first=0x7FF6B1747FE7 resolved=0x7FF6B17B7A30 module+0x797A30
CallTrigger hits=1 first=0x7FF6B13D4903 resolved=0x7FF6B13DB9E0 module+0x3BB9E0
```

说明：
- 当前版本能定位 VFX 创建/移除相关函数。
- 下一步应围绕这些函数做 hook/helper，而不是继续盲扫对象。

## 关键结构

来自 FFXIVClientStructs：

```text
Client::Graphics::Scene::VfxObject
  Position: Object + 0x50
  ActorCaster: 0x128
  ActorTarget: 0x130
  StaticCaster: 0x1B8
  StaticTarget: 0x1C0
  VfxResourceInstance*: 0x2A0

VfxResourceInstance
  VfxResourceUnk*: 0x08

VfxResourceUnk
  ApricotResourceHandle*: 0x18

ResourceHandle
  FileName std::string: 0x48

GameObject
  EntityId: 0x78
  OwnerId: 0x88
  Position: 0xB0
  DrawObject*: 0x100
```

## 推荐下一步实现路线

### 1. 当前 runtime：ACT 插件直接读 Scene.World active VFX graph

ACT 插件仍是 out-of-process 读 FF14 内存，不 hook FF14 函数；用户可见的 VFX 面板仍整合在 ACT 插件中，由插件生命周期和配置按钮自动启停。

当前 runtime 读取链路：

```text
VfxMonitorController
  -> ActiveVfxMemoryService
  -> Scene.World.Instance signature
  -> World + 0x30 / +0x40 scene roots
  -> traverse ChildObject / NextSiblingObject
  -> VfxObjectProbeCache(confirmed vtable always probe, unknown vtable retry, failed vtable short TTL skip)
  -> VfxObject +0x2A0 VfxResourceInstance
  -> +0x08 -> +0x18 -> ResourceHandle.FileName +0x48
  -> VfxMonitorEntry(Source=ActiveInstance, path, position, caster/target)
  -> ActiveVfxDisplayCache(log-style 10s retention + short-lived N/M hold)
  -> VfxDistanceFilter with ObjectTable self position
  -> VfxMonitorForm per RenderFps tick
```

这个方案解决的问题：
- 不再“扫描一次 sleep 一次”。
- 不再做 `.avfx` 全内存扫描驱动 UI。
- 能显示当前 `Scene.World` 中仍 active 的 VFX 实例，作为面板上方实时存活区，距离和 position 随当前玩家坐标刷新。
- 能把首次观测到的 VFX 快照写入面板下方历史日志区，距离和 position 不会因为玩家移动而覆盖。
- 能把刚消失的 active VFX 像日志一样默认保留 30 秒。
- 短命 VFX 从首次到末次 active 小于等于 N 秒时，可在普通窗口后额外续显 M 秒并标记 `HOLD`。
- 能读 position 并按 `VfxMaxDistance` 过滤。
- 面板工具栏能复制全部文本、复制选中/当前行、复制去重 `.avfx` 路径列表，并可暂停刷新，避免高频刷新时手动选择被重置。
- 未知 vtable 不再因已发现一个 VFX vtable 就永久跳过；进入副本后如果出现新的 VFX 子类，runtime 会周期性尝试解析并在成功后加入确认集。

### 2. Probe 仍作为维护验证工具

需要游戏更新后继续验证：
- `probe-vfx-world`：验证 `Scene.World` 签名、root、active VfxObject path。
- `probe-vfx-functions`：验证 `ActorVfxCreate` / `StaticVfxRun` / `VfxObjectCreate` 等签名，作为后续 create/remove 生命周期补强。
- `probe-character-vfx`：只作为 actor-bound/omen/tether 方向参考，不作为主入口。

### 3. 字符串扫描降级为离线候选工具

`.avfx` 字符串扫描仍可在 `EntityEspProbe` 中用于离线候选采集和历史对比，但不再作为 ACT 插件 VFX 面板 runtime 数据源。保留原因：
- 写触发器时仍可用 `scan-avfx-memory` / `capture-cast-vfx` 做候选 path 收集。
- 当 `Scene.World` 签名或 VfxObject 结构在游戏更新后失效时，可用它辅助判断客户端资源 path 是否仍能读到。

不再用于 runtime 的原因：
- 扫描有频率和分片范围。
- 只能读资源字符串，不能证明 active。
- 没有 position/caster/target。
- 性能和实时性不满足每帧需求。

### 4. 后续可补强：create/remove 生命周期

如果后续发现某些 VFX 不挂在 `Scene.World` object graph 上，下一步再用 helper/Dalamud/injected probe 捕获：
- `ActorVfxCreate`
- `ActorVfxRemove`
- `StaticVfxCreate/Run`
- `StaticVfxRemove`

目标记录字段：

```text
timestamp
path
VfxObject*
caster GameObject*
target GameObject*
VfxObject.Position
VfxObject.ActorCaster / ActorTarget
VfxObject.StaticCaster / StaticTarget
caster/target EntityId / OwnerId / Position
```

## ACT 网络日志的作用

ACT / cactbot 网络日志不能提供 `.avfx path`，但能提供 action 语义：
- `20` StartsCasting
- `21/22` Ability / AOEAbility
- `263/264` ability extra / ground position
- `35` tether
- `37` action sync

推荐做法：
- 插件内 VFX 面板记录当前 `Scene.World` active VFX instance。
- ACT 网络日志记录 action/source/target/position。
- 按时间窗、sourceId、targetId、位置距离进行关联。

## 当前结论

- 当前 ACT 插件 runtime 已切到 `ActiveVfxMemoryService`：直接读 `Scene.World` active VFX graph，不再用 PathScanFallback 扫描线程驱动 UI。
- VFX 面板已拆成上方实时存活区和下方历史日志区：实时区保持内存实时距离/position，历史区由 `ActiveVfxDisplayCache` 保留首次观测快照。
- `ActiveVfxDisplayCache` 会把刚消失的 active VFX 按日志式窗口默认保留 30 秒；短命 VFX 可在普通窗口后额外续显 M 秒，`HOLD` 只表示显示保留，不表示仍在 active graph。
- 现场 probe 已验证 `probe-vfx-world` 能解析 active VFX path、position、caster/target 与 `VfxResourceInstance`。
- 公开资料证明 hook 创建函数仍是进一步补全生命周期的正路，但 hook 不应直接塞进 ACT 插件主 DLL。
- 字符串扫描只保留为 `EntityEspProbe` 离线候选/维护工具，不作为当前用户可见 VFX 面板的数据源。
