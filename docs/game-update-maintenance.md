# 游戏更新后的维护与快速定位

本文用于 FF14 游戏版本更新后，快速判断 Entity ESP ACT 插件是否需要更新内存签名、偏移或读取逻辑。

## 一键审计

游戏更新后先启动 FF14 到角色在线/副本内，再运行：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- update-audit
```

建议记录输出中的：

- `Game version`
- `ObjectTable` hits/resolved/entities
- `Control/Camera` matrix 状态
- `TargetSystem` hardTarget 状态
- `VFX Scene.World` hits/root/active 数量

## 哪些内容不依赖安装路径

插件分发后不再写死作者电脑路径：

- ACT 目录优先从正在运行的 `Advanced Combat Tracker.exe` 进程解析。
- 如果 ACT 进程路径不可读，则从插件运行目录向上寻找 `Advanced Combat Tracker.exe`。
- FF14 目录优先从正在运行的 `ffxiv_dx11.exe` 进程解析。
- 如果 FF14 未运行，则尝试常见 SquareEnix/WeGame 安装目录。
- 旧配置里如果残留作者本机 ACT/FF14 路径，会在加载时自动迁移为当前机器路径。

## 游戏更新后最可能需要重新定位的项目

### 1. ObjectTable

用途：读取实体列表、EntityId、BNpcId、位置、读条、OwnerId 等。

当前依赖：

- `GameObjectTableReader.ObjectTableSignature`
- `GameObjectTableReader.ExperimentalObjectTableOffset`
- `GameObjectSize = 0x1A0`
- `EntityIdOffset = 0x78`
- `BaseIdOffset = 0x84`
- `OwnerIdOffset = 0x88`
- `ObjectIndexOffset = 0x8C`
- `ObjectKindOffset = 0x90`
- `NameOffset = 0x30`
- `PositionOffset = 0xB0`
- `RotationOffset = 0xC0`
- `HitboxRadiusOffset = 0xD0`

审计判断：

- `ObjectTable hits=1` 且 `entities > 0`：通常可用。
- `hits=0` 但 fixed fallback 仍有实体：签名坏了，fallback 暂时可用但应更新签名。
- `entities=0` 或实体字段异常：ObjectTable 地址或 GameObject 字段偏移需要重找。

辅助命令：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- read-entities
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- scan-objects
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- find-objecttable-refs
```

### 2. Control.Instance / Camera

用途：世界坐标投影到屏幕坐标，Overlay 位置依赖它。

当前依赖：

- `ControlCameraReader.ControlInstanceSignature`
- `ControlCameraReader.ViewProjectionMatrixOffset = 0x76B0`

审计判断：

- `Control/Camera matrix=OK`：可用。
- `signature not found`：更新 Control.Instance 签名。
- `matrix is not plausible`：签名可能命中错误，或 `ViewProjectionMatrixOffset` 变了。

辅助命令：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- read-camera
```

### 3. TargetSystem 当前目标

用途：读取当前硬目标，用于锁定/置顶目标相关显示。

当前依赖：

- `ControlTargetSystemOffset = 0x190`
- `TargetSystemHardTargetOffset = 0x80`
- `GameObjectEntityIdOffset = 0x78`

审计判断：

- Camera OK 但 `hardTarget unavailable`：优先检查 TargetSystem 偏移。
- 目标 EntityId 读出但和游戏目标不一致：检查 hard target 指针或 GameObject EntityId 偏移。

辅助命令：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- read-target
```

### 4. VFX Scene.World active graph

用途：当前 VFX 面板的实时 active instance 读取，包含 path、position、caster/target、距离过滤；面板上半区显示实时存活 VFX，下半区显示冻结首次观测快照的历史日志，默认 30 秒保留并支持短命续显。

当前依赖：

- `Scene.World.Instance` 签名：`48 8B 05 ?? ?? ?? ?? 48 8B 50 40`
- `World + 0x30 / +0x40` scene root 指针。
- `Client::Graphics::Scene::Object` 链：`ChildObject +0x30`、`NextSiblingObject +0x28`。
- `VfxObject.Position +0x50`。
- `VfxObject.ActorCaster +0x128` / `ActorTarget +0x130`。
- `VfxObject.StaticCaster +0x1B8` / `StaticTarget +0x1C0`。
- `VfxObject.VfxResourceInstance +0x2A0`。
- `VfxResourceInstance +0x08 -> +0x18 -> ResourceHandle.FileName +0x48`。
- `ActiveVfxDisplayCache` 历史日志普通保留窗口：`VfxDisplaySeconds = 30`。
- 短命续显配置：`VfxShortLivedMaxAgeSeconds = 2`，`VfxShortLivedHoldSeconds = 15`。

审计判断：

- `worldSig hits=1` 且 `withPath > 0`：通常可用。
- `worldSig hits=0`：更新 `Scene.World.Instance` 签名。
- `visited > 0` 但 `withPath=0`：优先检查 root offset、VfxObject offset、ResourceHandle path 链。
- `withPath > 0` 但距离全为 `n/a`：检查 ObjectTable slot 0 自身坐标读取。
- `withPath > 0` 但面板仍看不清短命 VFX：检查配置页 `VFX 日志式显示秒数`、`VFX 短命判定秒数 N`、`VFX 短命续显秒数 M`。

辅助命令：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- probe-vfx-world 8000 80
```

### 5. VFX 字符串扫描离线 fallback

用途：离线候选 path 采集和 active graph 失效时的资源路径排查；不再作为 ACT VFX 面板 runtime 数据源。

当前依赖：

- 进程可读内存区域枚举。
- `.avfx` ASCII 路径字符串提取规则。

辅助命令：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- scan-avfx-memory
```

### 6. Active VFX create/remove 后续补强

目标：补齐不挂在 `Scene.World` graph 上的特殊 VFX，并记录更完整的生命周期。

当前结论：

- `Scene.World` active graph 已经能作为 ACT runtime 主入口。
- `Character.VfxContainer` 已用 `probe-character-vfx` 验证，当前现场多数 Player/BattleNpc slot 为 0，不能作为主入口。
- 盲扫 `VfxObject` 结构噪声很大，不能进入 runtime。
- 从 `.avfx` 字符串地址反查引用为 0，说明字符串扫描命中的可能是缓存/字符串池副本，不能可靠反推 ResourceHandle/VfxObject。
- 公开资料和 VFXEditor 代码表明，create/remove hook 仍是补完整生命周期的正路，但不应直接塞进 ACT 主 DLL。

游戏更新后需要重点维护这些签名：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- probe-vfx-functions
```

当前应关注：

- `ActorVfxCreate`
- `ActorVfxRemove`
- `StaticVfxRun` / `StaticVfxCreate`
- `StaticVfxRemove`
- `VfxObjectCreate`
- `CallTrigger`

active instance / graph helper 仍需验证这些结构偏移：

- `VfxObject.Position`：`+0x50`
- `VfxObject.ActorCaster`：`+0x128`
- `VfxObject.ActorTarget`：`+0x130`
- `VfxObject.StaticCaster`：`+0x1B8`
- `VfxObject.StaticTarget`：`+0x1C0`
- `VfxObject.VfxResourceInstance`：`+0x2A0`
- `GameObject.EntityId` / `OwnerId` / `Position`

推荐架构：ACT 插件 runtime 优先直接遍历 `Scene.World` active graph；如果后续发现 graph 覆盖不全，再用 Dalamud/helper/injected probe 捕获 create/remove 生命周期，并通过 named pipe/local feed 给 ACT overlay 消费。详见 `docs/vfx-active-instance-research.md`。

## 发布前检查清单

1. 删除或迁移作者本机路径配置。
2. 运行 `update-audit`。
3. 进入 4/8/24 人环境各至少一次，确认 roster 过滤。
4. 确认 Overlay build/render 没有全 `.text` 扫描或昂贵探针。
5. 确认 ACT 插件部署仍是单 DLL，避免依赖程序集探测失败。
6. 如 ObjectTable/Camera/VFX 任一审计失败，先用 probe 定位，不要直接把未验证偏移写进 ACT 插件。
