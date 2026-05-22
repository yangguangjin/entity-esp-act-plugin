# 方案 B：ACT 原生实时跟随实体插件需求技术设计

## 1. 方案定位

方案 B 是不依赖卫月/Dalamud 的 ACT 原生插件方案。

目标是实现真正的“实体头顶实时跟随文字”。

核心思路：

```text
ACT 插件 DLL
  ↓
读取实体列表、坐标、读条、BNpcID、OwnerID
  ↓
读取相机矩阵 / 投影矩阵 / 游戏窗口 viewport
  ↓
WorldToScreen：世界坐标 → 屏幕坐标
  ↓
透明置顶 overlay 在实体头顶画文字
```

与方案 A 的区别：

- 方案 A 用 PictoACT 固定坐标画图，文字是屏幕固定面板。
- 方案 B 自己做投影和 overlay 绘制，文字能跟随实体移动。

## 2. 目标与非目标

### 2.1 目标

必须实现：

- ACT 插件加载、卸载、配置页。
- 实体列表实时扫描。
- 实体 ID、BNpcID、BNpcNameID、OwnerID、坐标、读条信息采集。
- 相机矩阵读取。
- WorldToScreen 投影。
- 透明置顶鼠标穿透 overlay。
- 实体头顶文字实时跟随。
- 队友实体过滤。
- 队友技能实体黑名单。
- BNpcID / BNpcNameID / EObjNameID 黑白名单。
- cast-only 模式。
- 热键开关。
- 配置持久化。

### 2.2 非目标

第一阶段不做：

- 游戏内真实 3D 字体 Mesh。
- 修改游戏 NamePlate。
- 对其他玩家可见的标记。
- 自动机制解法。
- 复杂时间轴系统。
- 完整替代 Triggernometry。

说明：

- 本方案的“3D 文字”是 screen-space overlay。
- 文字位置来自实体 3D 世界坐标投影，因此视觉上贴在实体头顶。
- 它不修改游戏世界，也不让其他玩家看到。

## 3. 总体架构

```text
EntityEspActPlugin.dll
  ├─ PluginMain
  │   ├─ ACT 插件生命周期
  │   ├─ 配置页注册
  │   └─ 服务启动/停止
  │
  ├─ EntityScanner
  │   ├─ 读取实体列表
  │   ├─ 读取读条状态
  │   └─ 生成 EntitySnapshot
  │
  ├─ CameraReader
  │   ├─ 读取相机位置
  │   ├─ 读取 ViewMatrix
  │   ├─ 读取 ProjectionMatrix
  │   └─ 维护版本化偏移/签名
  │
  ├─ ProjectionService
  │   └─ WorldToScreen
  │
  ├─ FilterService
  │   ├─ 队友过滤
  │   ├─ OwnerID 过滤
  │   ├─ 黑名单/白名单
  │   └─ 模式过滤
  │
  ├─ OverlayHost
  │   ├─ 透明窗口
  │   ├─ 鼠标穿透
  │   └─ 跟随游戏窗口
  │
  ├─ Renderer
  │   ├─ 绘制文字
  │   ├─ 绘制读条
  │   ├─ 绘制背景框
  │   └─ 绘制调试信息
  │
  ├─ ConfigService
  │   ├─ 读取配置
  │   ├─ 保存配置
  │   └─ 默认配置
  │
  └─ IntegrationServer 可选
      ├─ HTTP pin 接口
      └─ Triggernometry 联动
```

## 4. 项目结构建议

```text
EntityEspActPlugin/
  EntityEspActPlugin.csproj
  PluginMain.cs
  Services/
    EntityScanner.cs
    CameraReader.cs
    ProjectionService.cs
    FilterService.cs
    OverlayHost.cs
    ConfigService.cs
    HotkeyService.cs
    IntegrationServer.cs
  Models/
    EntitySnapshot.cs
    EntityDisplayState.cs
    CameraSnapshot.cs
    EspConfig.cs
    FilterResult.cs
    PinRequest.cs
  Rendering/
    OverlayWindow.cs
    Renderer.cs
    TextStyle.cs
  UI/
    ConfigPanel.cs
  Utils/
    Win32.cs
    MatrixUtils.cs
    Logger.cs
```

## 5. 数据模型

### 5.1 EntitySnapshot

```csharp
public sealed class EntitySnapshot
{
    public ulong Address { get; set; }
    public uint EntityId { get; set; }
    public string EntityIdHex { get; set; } = "";
    public string Name { get; set; } = "";

    public EntityKind Kind { get; set; }
    public uint BNpcId { get; set; }
    public uint BNpcNameId { get; set; }
    public uint EObjNameId { get; set; }
    public uint OwnerId { get; set; }

    public Vector3 Position { get; set; }
    public float Heading { get; set; }
    public float HitboxRadius { get; set; }
    public float DistanceToPlayer { get; set; }

    public bool IsTargetable { get; set; }
    public bool IsVisible { get; set; }
    public bool IsPartyMember { get; set; }
    public bool IsSelf { get; set; }

    public bool IsCasting { get; set; }
    public uint CastId { get; set; }
    public float CastCurrent { get; set; }
    public float CastMax { get; set; }
    public uint CastTargetId { get; set; }

    public DateTime LastSeen { get; set; }
}
```

### 5.2 EntityDisplayState

```csharp
public sealed class EntityDisplayState
{
    public EntitySnapshot Snapshot { get; set; } = default!;
    public Vector2 ScreenPosition { get; set; }
    public bool OnScreen { get; set; }
    public string LabelText { get; set; } = "";
    public uint Color { get; set; }
    public bool Pinned { get; set; }
    public DateTime PinExpireAt { get; set; }
    public string FilterReason { get; set; } = "";
}
```

### 5.3 EspConfig

```csharp
public sealed class EspConfig
{
    public bool Enabled { get; set; } = true;
    public bool ShowCastingOnly { get; set; } = false;
    public bool ShowUntargetable { get; set; } = true;
    public bool FilterSelf { get; set; } = true;
    public bool FilterPartyPlayers { get; set; } = true;
    public bool FilterPartyOwned { get; set; } = true;
    public bool FilterPartySkillEntity { get; set; } = true;

    public float MaxDistance { get; set; } = 100f;
    public int MaxDisplayedEntities { get; set; } = 30;
    public int EntityScanHz { get; set; } = 20;
    public int RenderFps { get; set; } = 60;

    public float FontSize { get; set; } = 14f;
    public float Opacity { get; set; } = 0.85f;
    public bool ShowCastBar { get; set; } = true;
    public bool ShowFilterReason { get; set; } = false;

    public HashSet<uint> BNpcBlacklist { get; set; } = new();
    public HashSet<uint> BNpcNameBlacklist { get; set; } = new();
    public HashSet<uint> EObjNameBlacklist { get; set; } = new();
    public HashSet<uint> BNpcWhitelist { get; set; } = new();
}
```

## 6. 实体采集设计

### 6.1 数据来源

优先级：

```text
1. FFXIV_ACT_Plugin / OverlayPlugin 已暴露 combatants 数据
2. ACT 网络日志补充 03 / 14 / 105 信息
3. 自研内存读取补齐对象表、OwnerID、BNpcID、读条、坐标
```

说明：

- 如果现有 ACT 插件接口能拿到实体列表，优先复用。
- 如果拿不到足够字段，需要自研内存读取。
- 仅靠 ACT parsed log 不足以实时跟随移动实体。

### 6.2 扫描频率

建议：

```text
EntityScanHz = 20
即每 50ms 扫描一次实体快照
```

原因：

- 实体移动跟随需要高于日志频率。
- 20Hz 已足够平滑。
- 渲染可以 60FPS，但实体数据不用每帧全量读取。

### 6.3 实体范围

第一阶段扫描：

```text
BattleNpc
EventNpc
EventObj 可选
当前目标
正在读条实体
```

默认不显示：

```text
玩家队友
自己
OwnerID 属于队友的实体
无效实体
死亡/不可见实体，可配置
距离过远实体
```

## 7. 相机矩阵与投影设计

### 7.1 需要的数据

必须获得：

```text
ViewMatrix
ProjectionMatrix
ViewProjectionMatrix
CameraPosition
GameViewportRect
GameWindowClientRect
DpiScale
```

### 7.2 CameraReader

职责：

- 定位游戏进程。
- 读取相机结构。
- 读取或计算 ViewProjectionMatrix。
- 处理游戏更新后的偏移维护。
- 输出 `CameraSnapshot`。

```csharp
public sealed class CameraSnapshot
{
    public Matrix4x4 ViewMatrix { get; set; }
    public Matrix4x4 ProjectionMatrix { get; set; }
    public Matrix4x4 ViewProjectionMatrix { get; set; }
    public Vector3 CameraPosition { get; set; }
    public RectangleF Viewport { get; set; }
    public bool IsValid { get; set; }
}
```

### 7.3 WorldToScreen

伪代码：

```csharp
public bool WorldToScreen(Vector3 world, CameraSnapshot camera, out Vector2 screen)
{
    var clip = Vector4.Transform(new Vector4(world, 1f), camera.ViewProjectionMatrix);

    if (clip.W <= 0.01f)
    {
        screen = default;
        return false;
    }

    var ndcX = clip.X / clip.W;
    var ndcY = clip.Y / clip.W;

    if (ndcX < -1f || ndcX > 1f || ndcY < -1f || ndcY > 1f)
    {
        screen = default;
        return false;
    }

    screen = new Vector2(
        camera.Viewport.Left + (ndcX + 1f) * 0.5f * camera.Viewport.Width,
        camera.Viewport.Top + (1f - ndcY) * 0.5f * camera.Viewport.Height
    );

    return true;
}
```

注意：

- FF14 坐标轴和矩阵方向需要实测校正。
- 如果投影左右/上下反了，需要调整矩阵乘法顺序或 NDC 转换。
- 实体头顶位置不能只用实体原点，需要加高度偏移。

### 7.4 头顶高度计算

建议：

```csharp
float headOffset = Math.Max(1.8f, entity.HitboxRadius * 1.2f);
var labelWorld = entity.Position + new Vector3(0, headOffset, 0);
```

需要实测 FF14 坐标轴：

- 如果 Y 是高度，用 `Y + offset`。
- 如果 Z 是高度，用 `Z + offset`。
- 以实际内存坐标为准。

### 7.5 投影调试模式

必须提供：

- 显示当前目标投影点。
- 显示玩家自身投影点。
- 显示屏幕中心十字。
- 显示矩阵有效/无效状态。
- 显示 viewport 和 DPI。

原因：

- CameraReader 是最大风险点。
- 需要快速判断是矩阵错、坐标轴错、DPI 错，还是窗口区域错。

## 8. Overlay 渲染设计

### 8.1 窗口要求

Overlay 窗口必须：

```text
透明
置顶
鼠标穿透
不抢焦点
跟随游戏窗口位置和尺寸
支持多显示器
支持 DPI 缩放
```

Win32 扩展样式：

```text
WS_EX_LAYERED
WS_EX_TRANSPARENT
WS_EX_TOPMOST
WS_EX_NOACTIVATE
```

### 8.2 渲染技术选型

推荐优先级：

```text
1. Direct2D + DirectWrite
2. SkiaSharp
3. ImGui.NET + DirectX
4. WPF，仅原型验证，不推荐长期高频渲染
```

原因：

- 文字数量可能较多。
- 需要稳定 30-60 FPS。
- Direct2D/DirectWrite 对 Windows 透明 overlay 更合适。

### 8.3 标签样式

默认标签：

```text
40001234 | BNpc 20123
Cast C341 1.2/4.0
```

样式：

```text
白字
黑色描边或半透明黑底
读条实体红色边框
Pinned 实体黄色高亮
队友过滤调试灰色
```

### 8.4 读条条形

如果 `ShowCastBar=true`：

```text
文字下方画一条 60px 宽读条
进度 = CastCurrent / CastMax
颜色 = 红/橙
```

### 8.5 重叠处理

问题：多个实体投影到相近屏幕位置时文字重叠。

第一阶段策略：

- 按 Y 坐标排序。
- 简单垂直错位。
- 每个标签最多偏移 3 次。

后续可做：

- 力导向布局。
- 屏幕边缘吸附。
- 只显示最近/读条/白名单实体。

## 9. 过滤系统设计

### 9.1 判定顺序

```text
1. Whitelist 命中 → 显示
2. Disabled → 不显示
3. Self 命中 → 过滤
4. Party player 命中 → 过滤
5. OwnerID 属于自己或队友 → 过滤
6. 队友技能实体黑名单命中 → 过滤
7. cast-only 模式：非读条实体过滤
8. MaxDistance 超出 → 过滤
9. 屏幕外 → 不绘制
10. 显示
```

### 9.2 队友过滤

需要维护 party ID 集合：

```csharp
HashSet<uint> PartyEntityIds;
```

来源：

- ACT 队伍数据。
- FFXIV_ACT_Plugin combatants。
- 日志补充。

### 9.3 OwnerID 过滤

如果：

```text
entity.OwnerId == selfId
entity.OwnerId in PartyEntityIds
```

则默认过滤。

用于过滤：

- 召唤兽
- 炮塔
- 仙女
- 队友技能实体
- 玩家伴随对象

### 9.4 黑白名单

黑名单类型：

```text
BNpcID
BNpcNameID
EObjNameID
CastID 可选
```

白名单优先级最高。

原因：

- 黑名单可能误伤机制实体。
- 白名单允许强制显示关键机制实体。

### 9.5 过滤原因

调试模式显示：

```text
filtered: party
filtered: owner
filtered: bnpc blacklist
filtered: distance
```

用途：

- 调试误过滤。
- 收集黑名单。
- 验证队友技能实体来源。

## 10. 配置与控制

### 10.1 配置文件

路径：

```text
%APPDATA%/Advanced Combat Tracker/Config/EntityEspPlugin.json
```

示例：

```json
{
  "Enabled": true,
  "ShowCastingOnly": false,
  "ShowUntargetable": true,
  "FilterSelf": true,
  "FilterPartyPlayers": true,
  "FilterPartyOwned": true,
  "FilterPartySkillEntity": true,
  "MaxDistance": 100,
  "MaxDisplayedEntities": 30,
  "EntityScanHz": 20,
  "RenderFps": 60,
  "FontSize": 14,
  "Opacity": 0.85,
  "ShowCastBar": true,
  "ShowFilterReason": false,
  "BNpcBlacklist": [123, 456],
  "BNpcNameBlacklist": [789],
  "EObjNameBlacklist": [],
  "BNpcWhitelist": [20123]
}
```

### 10.2 ACT 配置页

必须提供：

- 总开关。
- cast-only 开关。
- 过滤队友开关。
- 过滤 OwnerID 开关。
- 黑名单/白名单编辑。
- 字号、透明度、最大距离。
- 投影调试模式。
- overlay 位置测试。

### 10.3 热键

建议：

```text
F8      开关显示
F9      cast-only 切换
F10     调试模式切换
```

注意：

- 热键可配置。
- 不应与游戏常用热键冲突。

## 11. ACT / Triggernometry 联动

### 11.1 本地 HTTP 接口

插件可启动本地服务：

```text
127.0.0.1:47774
```

接口：

```text
POST /pin       临时高亮实体
POST /filter    修改过滤项
POST /clear     清除 pin 和临时状态
GET  /status    查看插件状态
```

`/pin` 示例：

```json
{
  "entityId": "40001234",
  "label": "重要分身",
  "color": "#ffcc00",
  "ttl": 10
}
```

用途：

- Triggernometry 根据机制日志标记实体。
- ACT 日志触发时高亮指定分身。
- 与现有 XML 工作流兼容。

### 11.2 日志监听

插件可以监听 ACT 日志用于补充：

```text
03:AddCombatant
14:StartsCasting
105:Add / 105:Change
21:团灭/通关
```

用途：

- 更新实体历史。
- 判断副本状态。
- 清理临时 pin。

## 12. 性能设计

### 12.1 更新频率

建议：

```text
实体扫描：20Hz
渲染：60FPS，可配置 30FPS
配置保存：事件触发，不轮询
HTTP 服务：异步处理
```

### 12.2 优化策略

- 先过滤，再投影。
- 屏幕外不绘制。
- 超出最大距离不投影。
- 最多显示 N 个实体。
- 文本内容不变时复用布局。
- CameraSnapshot 每帧读取一次，不对每个实体重复读取。

### 12.3 线程模型

建议：

```text
ACT 主线程：插件生命周期和 UI
扫描线程：定时读取实体快照
渲染线程：overlay render loop
HTTP 线程：可选联动接口
```

注意：

- 跨线程共享数据使用 immutable snapshot 或 ReaderWriterLock。
- 不要在 ACT 主线程做重 IO 或高频内存读取。

## 13. 风险与缓解

### 13.1 相机矩阵维护风险

风险：

- 游戏版本更新导致偏移失效。
- 矩阵方向或乘法顺序错误。
- 视角切换时投影异常。

缓解：

- CameraReader 单独封装。
- 加签名扫描和版本检测。
- 提供投影调试模式。
- 版本更新只集中维护 CameraReader。

### 13.2 DPI / 窗口模式风险

风险：

- 无边框窗口、窗口模式、DPI 缩放导致 overlay 偏移。

缓解：

- 获取游戏窗口 client rect。
- overlay 跟随 client rect，而不是整个窗口 rect。
- 显式处理 DPI scale。
- 支持手动 X/Y 偏移修正。

### 13.3 性能风险

风险：

- 实体过多时 CPU/GPU 占用上升。

缓解：

- 限制最大显示实体数。
- cast-only 模式。
- 距离过滤。
- 黑名单过滤。

### 13.4 过滤误伤风险

风险：

- 黑名单把机制实体过滤掉。

缓解：

- 白名单优先。
- 显示过滤原因。
- 支持副本级配置。

## 14. 实施阶段

### P0：技术验证

目标：证明实时跟随可行。

内容：

- ACT 插件加载成功。
- overlay 窗口透明置顶鼠标穿透。
- 获取当前目标或一个固定实体坐标。
- 读取相机矩阵。
- WorldToScreen 成功。
- 文字跟随实体移动和视角旋转。

验收：

- 实体移动时文字实时更新。
- 转动视角时文字保持贴近实体头顶。
- 实体出屏幕时文字隐藏。

### P1：实体列表与读条

内容：

- 扫描实体列表。
- 显示 ID、BNpcID、BNpcNameID。
- 显示 CastID、读条进度。
- 显示不可选中实体。

验收：

- 新实体出现后能显示文字。
- 读条实体显示读条 ID 和进度条。
- 不可选中但存在对象表的实体能显示。

### P2：过滤系统

内容：

- 队友过滤。
- OwnerID 过滤。
- 队友技能实体黑名单。
- 白名单覆盖。
- cast-only 模式。

验收：

- 队友玩家默认不显示。
- 队友召唤物/宠物默认不显示。
- 黑名单实体不显示。
- 白名单实体强制显示。
- 过滤原因调试可用。

### P3：配置与联动

内容：

- ACT 配置页。
- 配置 JSON 持久化。
- 热键。
- HTTP pin 接口。
- Triggernometry 联动。

验收：

- 配置重启后保留。
- 热键可切换显示。
- Triggernometry 能高亮指定实体。

## 15. 结论

方案 B 是实现“不依赖卫月但实时跟随实体文字”的主方案。

优点：

- 真正实时跟随实体。
- 不依赖卫月/Dalamud。
- 可显示任意文字、读条、颜色、过滤原因。
- 可与 ACT/Triggernometry 联动。

缺点：

- 实现难度最高。
- 需要维护相机矩阵和对象结构。
- 需要处理 DPI、窗口模式、overlay 性能。

如果后续决定实现，建议先只做 P0：当前目标实体文字跟随验证。只要 P0 成功，后续实体列表、过滤、读条都是工程扩展问题。
