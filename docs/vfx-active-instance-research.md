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

### 1. 先做 helper/probe，不直接进 ACT runtime

ACT 插件是 out-of-process 读 FF14 内存，不适合直接 hook FF14 函数。

推荐做一个独立 helper：
- Dalamud 测试插件，或
- 注入式 probe/helper。

捕获：
- `ActorVfxCreate`
- `ActorVfxRemove`
- `StaticVfxCreate/Run`
- `StaticVfxRemove`

记录：

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

### 2. ACT 插件消费 helper 输出

为了分发和稳定，可以让 ACT 插件只消费 helper 输出：
- 本地 named pipe
- localhost websocket/http
- shared memory
- 临时内存 ring buffer

不要把 hook 逻辑塞进 ACT 插件主 DLL。

### 3. 保留字符串扫描作为 fallback

如果 helper 不存在：
- 继续使用当前增量热点字符串扫描。
- UI 明确标注这是 `path scan fallback`，不是 active instance。

如果 helper 存在：
- UI 显示 active VFX instance。
- 可展示 owner/position/caster/target。

## ACT 网络日志的作用

ACT / cactbot 网络日志不能提供 `.avfx path`，但能提供 action 语义：
- `20` StartsCasting
- `21/22` Ability / AOEAbility
- `263/264` ability extra / ground position
- `35` tether
- `37` action sync

推荐做法：
- helper 记录 VFX instance。
- ACT 网络日志记录 action/source/target/position。
- 按时间窗、sourceId、targetId、位置距离进行关联。

## 当前结论

- 仅靠 ACT out-of-process 内存读取，想稳定拿 active VFX instance 很难。
- 公开资料证明 hook 创建函数是正路。
- 当前客户端签名可定位函数。
- 下一步应实现 VFX hook helper/probe，再把结果接入 ACT overlay。
