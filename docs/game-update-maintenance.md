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
- `VFX string scan` paths 数量

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

### 4. VFX 字符串扫描

用途：当前 Live VFX 面板的资源路径级显示。

当前依赖：

- 进程可读内存区域枚举。
- `.avfx` ASCII 路径字符串提取规则。

审计判断：

- `paths > 0`：资源路径扫描可用。
- `paths=0`：检查可读内存枚举、字符串提取规则，或游戏资源字符串布局是否变化。

辅助命令：

```bash
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- scan-avfx-memory
```

### 5. Active VFX Instance 方向

目标：从资源路径级升级到真正的活跃 VFX 实例，包含 path、owner actor、position、caster/target。

当前结论：

- `Character.VfxContainer` 已用 `probe-character-vfx` 验证，当前现场多数 Player/BattleNpc slot 为 0，不能作为主入口。
- 盲扫 `VfxObject` 结构噪声很大，不能进入 runtime。
- 从 `.avfx` 字符串地址反查引用为 0，说明字符串扫描命中的可能是缓存/字符串池副本，不能可靠反推 ResourceHandle/VfxObject。
- 公开资料和 VFXEditor 代码表明，正确路线是捕获 VFX 创建/移除函数，而不是事后扫内存。

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

active instance helper 仍需验证这些结构偏移：

- `VfxObject.Position`：`+0x50`
- `VfxObject.ActorCaster`：`+0x128`
- `VfxObject.ActorTarget`：`+0x130`
- `VfxObject.StaticCaster`：`+0x1B8`
- `VfxObject.StaticTarget`：`+0x1C0`
- `VfxObject.VfxResourceInstance`：`+0x2A0`
- `GameObject.EntityId` / `OwnerId` / `Position`

推荐架构：ACT 插件不要直接 out-of-process hook FF14；先做 Dalamud/helper/injected probe 捕获 create/remove，再通过 named pipe/local feed 给 ACT overlay 消费。详见 `docs/vfx-active-instance-research.md`。

## 发布前检查清单

1. 删除或迁移作者本机路径配置。
2. 运行 `update-audit`。
3. 进入 4/8/24 人环境各至少一次，确认 roster 过滤。
4. 确认 Overlay build/render 没有全 `.text` 扫描或昂贵探针。
5. 确认 ACT 插件部署仍是单 DLL，避免依赖程序集探测失败。
6. 如 ObjectTable/Camera/VFX 任一审计失败，先用 probe 定位，不要直接把未验证偏移写进 ACT 插件。
