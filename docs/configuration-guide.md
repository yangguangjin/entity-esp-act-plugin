# Entity ESP ACT Plugin 配置说明

本文说明 Entity ESP 插件页面中各个配置项的用途、开关效果和常见使用示例。

作者联系方式：QQ1115284886  光进不出。

本项目使用 MIT License 开源，协议文件见仓库根目录 `LICENSE`。

当前默认配置已按用户当前实战配置同步。

分发版路径策略：
- 配置保存到当前 Windows 用户的 `%APPDATA%/Advanced Combat Tracker/Config/EntityEspPlugin.json`。
- ACT 目录优先从正在运行的 `Advanced Combat Tracker.exe` 识别；失败时从插件目录向上寻找。
- FF14 目录优先从正在运行的 `ffxiv_dx11.exe` 识别；失败时尝试常见 SquareEnix/WeGame 安装目录。
- 旧配置如果残留作者本机路径，会在加载配置时自动迁移为当前机器路径。

Release 构建后的插件 DLL 位于：

```text
src/EntityEspActPlugin.Act/bin/Release/net48/EntityEspActPlugin.Act.dll
```

把该 DLL 添加到 ACT 插件页即可加载；DLL 更新后必须重启 ACT 才会加载新代码。

## 1. 基础控制

### 启用 Overlay

字段：`Enabled`

效果：
- 开启：显示 Entity ESP overlay。
- 关闭：隐藏 overlay，但配置仍保留。

示例：
- 打本写 TRN 时开启。
- 正常游玩、不需要实体信息时关闭。

### 数据源

字段：`DataSource`

可选：
- `Real`：读取真实 FF14 客户端内存和 ACT 日志。
- `Mock`：使用测试数据。

效果：
- `Real` 用于实战。
- `Mock` 用于测试 overlay 样式、布局和基础渲染，不依赖游戏进程。

推荐：

```text
DataSource=Real
```

### Diagnostics

插件页面里的 Diagnostics 主要用于轻量环境检查：
- ACT 路径。
- FF14 路径。
- 游戏版本。
- 当前数据源状态。

约定：
- 昂贵的内存扫描、实体抓取、队伍抓取、VFX 路径抓取，不放在 ACT Diagnostics。
- 这些工作优先使用 `EntityEspProbe` 控制台工具。

原因：
- ACT 插件页刷新慢。
- UI 线程不能做全 `.text` 扫描、全内存扫描或长时间文件扫描。
- 控制台工具更适合实战快速迭代。

## 2. 实体显示过滤

### 只显示读条实体

字段：`ShowOnlyCasting`

效果：
- 开启：只显示正在读条的实体。
- 关闭：显示符合其他过滤条件的所有实体。

示例：
- 只想抓 Boss/小怪读条：开启。
- 想观察机制实体、场地实体、NPC：关闭。

### 显示不可选中实体

字段：`ShowUntargetable`

效果：
- 开启：不可选中实体也可能显示。
- 关闭：过滤掉不可选中实体。

示例：
- 有些机制实体不可选中，但会读条或参与机制，排查时开启。
- 正常低遮挡打本时可关闭。

### 过滤自己

字段：`FilterSelf`

效果：
- 开启：不显示自己。
- 关闭：显示自己实体。

推荐：

```text
FilterSelf=true
```

原因：
- 插件目标是抓 Boss / NPC / 机制实体，不是看自己。

### 过滤队友玩家

字段：`FilterPartyMembers`

效果：
- 开启：过滤队友玩家实体。
- 关闭：显示队友玩家实体。

队友来源：
- `IsSelf`
- `IsPartyMember`
- ACT parsed `0B PartyList`
- ACT network `11` roster
- 最多缓存 24 人 roster

重要：
- 插件不会把所有 `Kind=Player` 都当队友。
- PvP / 大规模场景里，敌对玩家也是 `Player`，不能直接过滤。
- 只有 roster 里的玩家才视为队友。

示例：
- 4 人本：network `11` rosterCount=4。
- 8 人本：network `11` rosterCount=8。
- 24 人本：network `11` rosterCount=24。

### 过滤自己/队友拥有的实体

字段：`FilterPlayerOwnedEntities`

效果：
- 开启：过滤 OwnerId 属于自己/队友的实体。
- 关闭：显示玩家拥有的实体。

用于过滤：
- 召唤物。
- 宠物。
- 部分玩家技能实体。

注意：
- 不会过滤敌对 NPC 的机制实体。
- 只按自己/队友 roster 和 OwnerId 判断。

### 最大距离

字段：`MaxDistance`

效果：
- 只显示距离自己不超过该值的实体。

当前默认：

```text
MaxDistance=100
```

示例：
- 只看近场机制：60。
- 大场地调试：100 或 150。

### 最大显示实体数

字段：`MaxDisplayedEntities`

效果：
- 限制 overlay 同时显示的实体数量。
- 避免 PvP / 24 人 / 多小怪场景刷屏。

当前默认：

```text
MaxDisplayedEntities=30
```

建议：
- 低遮挡：20-30。
- 排查实体遗漏：50。

### EntityId 黑名单

字段：`EntityIdBlacklist`

效果：
- 按临时 EntityId 过滤实体。

适合：
- 临时屏蔽当前场景某个刷屏实体。

不适合：
- 长期配置。

原因：
- EntityId 是临时运行时 ID，下次进本可能变化。

示例：

```text
4000BAE9, 4000BB07
```

### BNpc 黑名单

字段：`BNpcBlacklist`

效果：
- 按稳定 BNpcId 过滤实体。

适合：
- 长期过滤某类怪物或常驻机制实体。

当前默认：

```text
13961, 10489, 1008, 10487, 7245, 10490, 13498, 16926, 13505, 13507, 13506, 6982, 952
```

示例：
- 某类小怪总是无用，可以把它的 BNpcId 加入这里。

### BNpcName 黑名单

字段：`BNpcNameBlacklist`

效果：
- 按稳定 BNpcNameId 过滤实体。

适合：
- 同名/同类实体需要长期过滤。

和 BNpc 黑名单区别：
- BNpcId 更偏具体实体类型。
- BNpcNameId 更偏名称表 ID。

## 3. 调试显示

### 调试显示已过滤实体及原因

字段：`ShowFilteredEntitiesForDebug`

效果：
- 开启：被过滤掉的实体也会显示，并标出过滤原因。
- 关闭：只显示未过滤实体。

用途：
- 排查“为什么实体没显示”。
- 验证黑名单、队友过滤、距离过滤是否生效。

推荐：
- 正常打本关闭。
- 调试时短时间开启。

### 调试高亮样式

字段：`DebugOverlayStyle`

效果：
- 开启：使用调试样式突出显示过滤状态。
- 关闭：使用正常低遮挡样式。

推荐：
- 正常打本关闭。

## 4. 标签字段

### EntityId

字段：`LabelFields.EntityId`

效果：
- 显示实体临时 ID。

示例：

```text
EntityId:4000BAE9
```

用途：
- 关联 ACT 日志里的实体 ID。
- 写 TRN 正则时确认 src/tgt。

推荐：开启。

### Kind

字段：`LabelFields.Kind`

效果：
- 显示实体类型。

示例：

```text
Kind:BattleNpc
Kind:Player
Kind:EventObj
```

用途：
- 排查实体分类。

推荐：默认关闭，避免遮挡。

### 距离

字段：`LabelFields.Distance`

效果：
- 显示实体和自己的距离。

用途：
- 调整 `MaxDistance`。

推荐：默认关闭。

### HP

字段：`LabelFields.Hp`

效果：
- 显示实体当前 HP、最大 HP 和百分比。
- 只有内存快照读到角色类实体 HP 时才显示；场地物件、没有 HP 的对象不会强行显示 `0/0`。

数据来源：
- Real 数据源：从 FF14 内存对象的 `CharacterData.Health / MaxHealth` 读取。
- Mock 数据源：使用示例 HP，方便测试样式。
- 不使用 ACT `27 HP` / `105 CurrentHP` 日志来填标签；这些 ACT 日志仍只用于实体活动续期、立即隐藏和相关日志显示。

示例输出：

```text
HP:123456/200000 (61.7%)
```

推荐：
- 写触发器或排查实体状态时开启。
- 正常打本如果遮挡太多则关闭。

### 坐标

字段：`LabelFields.Position`

效果：
- 显示实体当前 X/Y/Z 坐标。

数据来源：
- Real 数据源：从 FF14 内存对象表 `GameObject.Position` 读取，和 Overlay 投影使用的是同一份坐标。
- 不使用 ACT `10F/110/105 PosX/PosY/PosZ` 日志来填标签；这些 ACT 坐标日志仍只用于活动续期和日志展示。

示例输出：

```text
Pos:72.00,357.50,48.13
```

推荐：
- 调试实体落点、写 TRN/复现数据时开启。
- 平时默认关闭，避免标签过长。

### BNpcId

字段：`LabelFields.BNpcId`

效果：
- 显示稳定 BNpcId。

示例：

```text
BNpcId:0x4A4C
```

用途：
- 建长期黑名单。
- 写 TRN 时区分同名实体。

推荐：开启。

### BNpcNameId

字段：`LabelFields.BNpcNameId`

效果：
- 显示稳定 BNpcNameId。

用途：
- 建名称级黑名单。
- 排查同名/同类机制实体。

默认：关闭。

### BNpcName

字段：`LabelFields.BNpcName`

效果：
- 显示客户端读取到的实体名。

示例：

```text
BNpcName:青龙
BNpcName:山之式鬼
```

用途：
- 肉眼确认实体。

推荐：开启。

注意：
- 写 TRN 正则时不要依赖名字，优先用 ID。
- 名字会受语言、RSV、重名影响。

### EObjNameId

字段：`LabelFields.EObjNameId`

效果：
- 显示 EventObj 名称 ID。

用途：
- 排查场地物件、交互物件。

默认：关闭。

### Cast

字段：`LabelFields.Cast`

效果：
- 在实体标签中显示当前读条。

注意：
- 当前插件配置 UI 会强制：

```text
_config.LabelFields.Cast = false
```

原因：
- 读条信息已经通过实体相关 ACT 日志显示。
- 直接显示 Cast 字段容易遮挡。

### CastTargetId

字段：`LabelFields.CastTargetId`

效果：
- 显示读条目标 ID。

注意：
- 当前配置 UI 会强制关闭。
- 推荐使用 14/107/108 日志查看目标和落点。

## 5. 样式和性能

### 字号

字段：`Style.FontSize`

当前默认：

```text
15
```

效果：
- 控制 overlay 文字大小。

建议：
- 超宽屏/高分辨率：15-18。
- 遮挡严重：12-14。

### 背景透明度

字段：`Style.Opacity`

当前默认：

```text
0.001
```

效果：
- 控制标签背景块透明度。

重要：
- 背景透明度为 0 时，插件应彻底不绘制背景块、边框、文字描边、读条底色等雾化装饰。
- 当前默认接近全透明，保持低遮挡。

### 文字颜色

字段：`Style.TextColor`

效果：
- 控制主文字颜色。

示例：

```text
#FFFFFFFF
```

### 读条条形颜色

字段：`Style.CastBarColor`

效果：
- 控制读条条形颜色。

前提：

```text
ShowCastBar=true
```

### 锚点十字颜色

字段：`Style.AnchorColor`

效果：
- 控制实体屏幕锚点十字颜色。

用途：
- 帮助确认实体投影位置。

### 背景块颜色

字段：`Style.BackgroundColor`

效果：
- 控制标签背景块颜色。

注意：
- 最终可见程度还受 `Style.Opacity` 控制。

### 显示内存读条条形

字段：`ShowCastBar`

效果：
- 开启：当实体内存字段 `IsCasting / CastCurrent / CastMax` 有效时显示细条进度。
- 关闭：不显示这类内存读条条形。

默认：开启。

说明：
- 这是实体内存读条，不是 ACT `14 StartsCasting` 日志读条。
- 如果某些实体内存读条字段没有被正确读取，可能不显示。

### 显示 14 日志读条进度条

字段：`ShowActCastProgressBar`

效果：
- 开启：收到实体 `14 StartsCasting` 日志后，按日志里的 castTime 显示一条细长倒计时进度条。
- 关闭：不显示这条日志驱动的读条进度条。

默认：开启。

显示规则：
- 生命周期由 `14` 日志的读条时间决定。
- 不受 `实体旁日志保留秒数` 和 `右侧日志保留秒数` 影响。
- 读条结束后会自动消失。
- 条形高度很细，约 3 px，尽量贴近截图里经验条那种低遮挡横条。

示例：

```text
14:4000CB9C:青龙:37E4:荒魂燃烧:4000CB9C:青龙:4.700:...
```

收到后显示：

```text
4.7s
[细长黄色进度条，宽度跟上方实体 ID 标签接近，随时间推进]
```

说明：
- 不显示技能名，避免和 ACT 日志重复。
- 读条宽度不跟长日志文本扩展，只跟实体标签宽度接近。

适用场景：
- 写 TRN 时观察 Boss 当前读条剩余时间。
- ACT 日志显示秒数设得很短时，读条进度仍按技能 castTime 单独显示。

### 实体扫描频率

字段：`EntityScanHz`

当前默认：

```text
45
```

效果：
- 控制读取实体和构建显示状态的频率。

建议：
- 低压力：30。
- 当前默认：45。
- 高刷新验证：60。

注意：
- 用户游戏帧率约 165Hz，但 overlay 不需要跟满游戏帧率。
- 实体扫描过高会增加 ACT/WinForms 压力。

### 渲染 FPS

字段：`RenderFps`

当前默认：

```text
90
```

效果：
- 控制 overlay 重绘频率。

建议：
- 低压力：60。
- 当前默认：90。
- 不建议长期拉太高。

## 6. 实体相关 ACT 日志

### 启用实体相关 ACT 日志采集

字段：`ShowRelatedActLogs`

效果：
- 开启：采集并缓存能关联到实体的 ACT 战斗日志。
- 关闭：不采集实体相关日志；实体旁日志和右侧固定日志面板都会为空。

用途：
- 快速写 TRN 正则。
- 关联实体 ID、技能 ID、状态 ID、落点坐标。

显示位置：
- `ShowRelatedActLogsNearEntity`：实体旁显示 ACT 日志。
- `ShowRelatedActLogPanel`：右侧固定日志面板，适合录屏回看。

### 实体旁显示 ACT 日志

字段：`ShowRelatedActLogsNearEntity`

效果：
- 开启：在实体标签下显示该实体最近相关 ACT 日志。
- 关闭：实体旁只显示标签/读条，不显示日志。

适合：
- 现场看某个实体正在发生什么。
- 实体数量少、日志不密集的调试场景。

### 右侧固定日志面板(录屏分析)

字段：`ShowRelatedActLogPanel`

效果：
- 开启：在右侧固定面板显示最近采集到的实体相关日志流。
- 关闭：不显示右侧日志面板。

特点：
- 右侧面板按日志出现时间记录，不要求实体当前在镜头内、投影成功或出现在 overlay 标签列表里。
- 每行以 `#EntityId` 开头，方便和场上短标签或日志里的实体 ID 对应。
- 长日志会自动换行完整显示，不再用 `...` 截断。
- 适合实体多、实体旁日志互相重叠、镜头转开后仍要录屏回看日志的场景。

### 按 ACT 日志活动隐藏静止实体

字段：`UseActLogActivityLifetime`

当前默认：

```text
false
```

效果：
- 关闭：只要实体通过距离、黑名单、队友等普通过滤，就会继续显示。
- 开启：实体第一次被对象表看到时先显示一段默认生命；如果后续没有相关 ACT 日志活动，超时后从 overlay 隐藏。

推荐场景：
- 机制物、短命实体、召唤物太多，想让不再变化的 EntityId 标签自动消失。
- 写 Triggernometry 时只保留最近有日志变化的实体，减少画面常驻噪声。

续期来源示例：

```text
[22:15:13.762] 105:Add:40002BCA:BNpcID:4851:PosX:72.0000:PosY:357.5001:PosZ:48.0000
[22:15:20.000] 27:40002BCA:Boss:188300:180000
[22:15:40.000] 10F:40002BCA:Boss:72.0000:357.5001:48.0000
```

上面这些 Add / HP 更新 / 坐标更新都会把 `40002BCA` 的显示生命续到 `EntityActivityLifetimeSeconds`。

移除示例：

```text
[22:18:09.261] 105:Remove:40002BCA
```

收到 `19 Death`、`27 HP=0`、`105 CurrentHP=0`、`04 RemoveCombatant` 或 `105:Remove` 时，默认会立即隐藏；如果开启“短命实体删除/死亡后续显”，且实体从第一次被看到/创建到终止事件小于 `ShortLivedEntityMaxAgeSeconds`，则会改用最后一次实体快照继续显示 `ShortLivedEntityHoldSeconds` 秒，方便回看瞬间创建又删除的机制物。没有这些终止事件、但也没有后续日志活动时，会等生命超时后隐藏。

注意：
- 这个开关独立于 `ShowRelatedActLogsNearEntity`，即使不在实体旁显示日志，也可以用 ACT 日志续期实体生命。
- 这个开关也高于 `RelatedActLogFilters.*` 日志类型显示过滤：即使 `27 HP 更新`、`105 CombatantMemory` 或 `10F 瞬移/位置` 在实体旁日志里关闭，它们仍会参与实体生命续期。
- 续期判断按当前 `EntityId`，不是 BNpcId；同类实体重新生成后通常会有新的 EntityId。
- ACT 解析日志和网络日志都可识别：解析日志是 `105:Add:...` / `27:...` 这种 hex + 冒号格式，网络日志是 `261|...` / `39|...` 这种 decimal + 竖线格式。

### 实体 ACT 活动续期秒数

字段：`EntityActivityLifetimeSeconds`

当前默认：

```text
15
```

效果：
- 控制“新看到实体默认显示多久”和“每次 ACT 日志活动后续期多久”。

范围：
- 插件 UI 中限制为 1-120 秒。

建议：
- 实战低遮挡：8-15。
- 写 TRN / 录屏分析：15-30。
- 如果实体只需要跟随一次短日志出现，设 3-8；如果日志很稀疏，设 20-30。

### 短命实体删除/死亡后续显

字段：`PreserveShortLivedEntitiesAfterTerminal`

当前默认：

```text
true
```

效果：
- 开启：如果某个实体从第一次被对象表看到或 ACT 创建日志续期，到收到 `04/105 Remove`、`19 Death`、`27 HP=0`、`105 CurrentHP=0` 的时间小于 N 秒，则不立刻从 overlay 消失，而是用最后一次实体快照继续显示 M 秒。
- 关闭：终止事件仍按原逻辑立即隐藏。

用途：
- 有些机制实体创建后马上删除，现场没来得及看清 EntityId / BNpc / 位置。
- 写 TRN 或录屏复盘时，需要让“瞬间出现又消失”的实体标签多停留几秒。

注意：
- 续显使用最后一次对象表快照，所以位置/名字/BNpc 等字段是删除前最后读到的值。
- 只对短命实体生效；超过 N 秒才删除/死亡的普通实体仍会立即隐藏。
- 该逻辑只影响实体标签保留，不改变右侧固定日志面板的日志记录。

### 短命判定秒数 N

字段：`ShortLivedEntityMaxAgeSeconds`

当前默认：

```text
2
```

范围：
- 插件 UI 中限制为 0.1-30 秒。

建议：
- 瞬间机制物：1-3。
- 如果副本内短命对象生成稍慢，可设 3-5。
- 不建议太大，否则正常死亡/删除的实体也会被当成短命对象续显。

### 短命后续显秒数 M

字段：`ShortLivedEntityHoldSeconds`

当前默认：

```text
10
```

范围：
- 插件 UI 中限制为 0.5-60 秒。

建议：
- 实战低遮挡：3-6。
- 写 TRN / 录屏分析：8-15。
- 如果配合右侧固定日志面板复盘，可设到 10-20。

### 实体旁日志保留秒数

字段：`RelatedActLogSeconds`

当前默认：

```text
6
```

效果：
- 控制实体旁每个实体的日志显示窗口。
- 只影响实体旁日志，不影响右侧固定日志面板。

建议：
- 读条/技能确认：6-10。
- 低遮挡：3-5。

### 实体旁日志最大条数

字段：`RelatedActLogMaxLinesPerEntity`

当前默认：

```text
8
```

效果：
- 每个实体旁最多显示几条相关日志。
- 只影响实体旁日志，不影响右侧固定日志面板。

建议：
- 低遮挡：1-3。
- 调试：5-10。

### 右侧日志保留秒数

字段：`RelatedActLogPanelSeconds`

当前默认：

```text
6
```

效果：
- 控制右侧固定日志面板保留最近多少秒的日志。
- 只影响右侧固定日志面板，不影响实体旁日志。

范围：
- 0.5-60 秒。

建议：
- 录屏分析：10-20。
- 实战低遮挡：关闭右侧面板或设为 3-6。

### 右侧日志最大条数

字段：`RelatedActLogPanelMaxLines`

当前默认：

```text
8
```

效果：
- 控制右侧固定日志面板最多显示多少条日志。
- 只影响右侧固定日志面板，不影响实体旁日志。

范围：
- 1-80 条。

建议：
- 录屏分析：20-40。
- 屏幕空间紧张：8-15。

### 两组显示窗口是否冲突

结论：不冲突。

说明：
- 实体旁日志和右侧固定日志面板使用同一份缓存，但查询时各自按自己的秒数和条数过滤。
- 短窗口查询不会删除缓存里的旧日志，因此不会影响更长的右侧面板窗口。
- 例如实体旁日志 6 秒、右侧日志 10 秒时：实体旁只显示 6 秒内日志，右侧仍能显示 10 秒内日志。

### 过滤自己/队友 14 读条日志

字段：`FilterPlayerAndPartyLog14`

效果：
- 开启：过滤自己、队友、自己/队友拥有实体发出的 `14 StartsCasting`。
- 关闭：显示这些读条日志。

目的：
- 队友技能读条噪声很多，会淹没 Boss/机制读条。

重要：
- 不按 `caster==target` 粗暴过滤。
- 不过滤敌对 NPC / Boss / 场地目标读条。
- PvP 中不会把所有玩家都当队友。

### 过滤自己/队友 1A 状态日志

字段：`FilterPlayerAndPartyLog1A`

效果：
- 开启：过滤来源为自己/队友/自己队友拥有实体的 `1A StatusAdd`。
- 关闭：显示这些状态日志。

目的：
- 队友 buff、玩家技能状态非常多，会刷屏。

注意：
- 这是独立开关，不影响 `14`。
- NPC/机制实体来源的 `1A` 会保留。

## 7. 日志类型开关和简化显示

每种日志至少有两个开关：

```text
启用：是否显示该类型日志
简化显示：是否把原始日志压缩成 TRN 常用字段
```

部分常用日志有多个互斥简化模式：
- `14 StartsCasting`：`简化显示` / `简化显示2` 二选一，也可以都不选保留清理后的原始字段。
- `1A StatusAdd`：`简化显示` / `简化显示2` / `简化显示3` 三选一，也可以都不选保留清理后的原始字段。

显示清理规则：
- 所有显示位置都会移除 `Log:` 前缀。
- 所有显示位置都会移除 `[HH:mm:ss.fff]` 这种方括号时间。
- 原始日志显示会尽量从 ACT 行类型开始，例如 `14:...`、`1A:...`、`107:...`。
- 内部解析仍使用原始日志，不影响关联实体、技能 ID、状态 ID 或坐标。

当前默认：
- `14 StartsCasting` 启用。
- `1A StatusAdd` 启用。
- 其他多数关闭。
- 简化显示默认全部关闭，保留清理后的原始字段。

### 14 读条开始 StartsCasting

字段：

```text
RelatedActLogFilters.Log14
RelatedActLogSimplify.Log14
RelatedActLogSimplify.Log14Alt
```

原始日志示例：

```text
[14:20:56.777] StartsCasting 14:100472EA:挽明暗轧止:6503:闪灼:40006E25:来访石像魔:1476:-739.87:718.68:0.20:0.06
```

清理后原始显示：

```text
14:100472EA:挽明暗轧止:6503:闪灼:40006E25:来访石像魔:1476:-739.87:718.68:0.20:0.06
```

简化显示：

```text
14 Cast src=100472EA 6503:闪灼 -> tgt=40006E25 来访石像魔 t=1476 pos=(-739.87,718.68,0.20) h=0.06
```

简化显示2：

```text
凯夫卡 -> 技能名：众神之像 | 技能ID：28D7 | 时间：4.700
```

选择建议：
- 写 TRN 正则：用 `简化显示`，字段最全。
- 录屏回看机制：用 `简化显示2`，更容易看出谁在读什么技能。
- 需要复制原始字段：不勾简化。

TRN 用途：
- castid / abilityId。
- source entity。
- target entity。
- cast time。
- 起始位置和朝向。

### 1A 状态获得 StatusAdd

字段：

```text
RelatedActLogFilters.Log1A
RelatedActLogSimplify.Log1A
RelatedActLogSimplify.Log1AAlt
RelatedActLogSimplify.Log1AAlt2
```

原始日志示例：

```text
[23:51:16.400] StatusAdd 1A:5CB:魔法储存：爆炎:9999.00:400077BA:凯夫卡:400077C2:凯夫卡:00:22392:9926597
```

清理后原始显示：

```text
1A:5CB:魔法储存：爆炎:9999.00:400077BA:凯夫卡:400077C2:凯夫卡:00:22392:9926597
```

简化显示：

```text
1A StatusAdd 808:魔法受伤加重 dur=15.0 src=40006E25 Boss -> tgt=100472EA 玩家 stack=0
```

简化显示2：

```text
Buff名:魔法储存：爆炎 | BuffID:5CB | Buff时间:9999.00 | Buff类型:00   - - ->>>   凯夫卡
```

简化显示3：

```text
Buff名:魔法储存：爆炎 | BuffID:5CB | Buff时间:9999.00   - - ->>>   凯夫卡
```

选择建议：
- 写 TRN 正则：用 `简化显示` 或不勾简化，看 source/target/entityId 更完整。
- 录屏回看谁获得了什么 buff：用 `简化显示2`。
- 屏幕更窄、不需要 stack/type：用 `简化显示3`。

TRN 用途：
- effectId。
- duration。
- source。
- target。
- stack / type。

### 107 精确读条坐标

字段：

```text
RelatedActLogFilters.Log107
RelatedActLogSimplify.Log107
```

简化示例：

```text
107 CastPos src=4000BAE9 aid=37C3 pos=(98.45,98.21,0.03) h=1.79
```

TRN 用途：
- Boss 起手位置。
- 精确读条坐标。
- 朝向。

建议：
- 需要画矩形/扇形/直线 AOE 时开启。

### 108 技能落点

字段：

```text
RelatedActLogFilters.Log108
RelatedActLogSimplify.Log108
```

简化示例：

```text
108 AbilityPos src=4000BAE9 aid=37C4 seq=00004132 flag=1 pos=(99.992,99.992,-0.015) h=0.671
```

TRN 用途：
- 技能实际落点。
- 地面目标。
- 部分 AOE 的最终位置。

### 其他日志类型简表

```text
03  AddCombatant      实体生成
04  RemoveCombatant   实体消失
15  Ability           单体/普通命中
16  AOE Ability       AOE 命中
17  CastCancel        读条取消/中断
18  DoT/HoT Tick      dot/hot tick
19  Death             死亡
1B  HeadMarker        头标
1C  Waymark           场地标点
1D  Sign              目标标点
1E  StatusRemove      状态移除
21  ActorControl      控制事件/机制事件
23  Tether            拉线
26  StatusList        状态列表
27  HP Update         HP 更新
2A  StatusList3       扩展状态列表
105 CombatantMemory   ACT combatant memory
10F SetPos            瞬移/位置
110 SpawnExtra        出生/位置扩展
111 ActorControlSelf  self 控制扩展
112 ActorControlTarget target 控制扩展
```

建议：
- 写读条触发器：开 14 + 107。
- 写落点触发器：开 108。
- 写状态触发器：开 1A + 1E。
- 写头标：开 1B。
- 写拉线：开 23。
- 排查实体生成/消失：开 03 + 04。

## 8. 施法者日志黑名单

这些黑名单只影响相关 ACT 日志显示，不一定影响实体显示。

### 读条施法者 EntityId 黑名单

字段：`RelatedActLogCasterEntityIdBlacklist`

效果：
- 如果读条施法者 EntityId 在列表里，该读条日志不显示。

适合：
- 临时过滤某个当前场景刷屏实体。

### 读条施法者 BNpc 黑名单

字段：`RelatedActLogCasterBNpcBlacklist`

效果：
- 如果读条施法者 BNpcId 在列表里，该读条日志不显示。

当前默认和 `BNpcBlacklist` 一致。

适合：
- 长期过滤某类无用施法者。

### 读条施法者 BNpcName 黑名单

字段：`RelatedActLogCasterBNpcNameBlacklist`

效果：
- 如果读条施法者 BNpcNameId 在列表里，该读条日志不显示。

适合：
- 按名称类过滤施法者。

## 9. 常见配置示例

### 示例 A：低遮挡打本默认

目标：尽量不挡画面，只看 Boss/机制实体核心信息。

```text
Enabled=true
DataSource=Real
FilterSelf=true
FilterPartyMembers=true
FilterPlayerOwnedEntities=true
ShowFilteredEntitiesForDebug=false
DebugOverlayStyle=false
MaxDistance=100
MaxDisplayedEntities=30
Style.Opacity=0.001
LabelFields.EntityId=true
LabelFields.BNpcId=true
LabelFields.BNpcName=true
ShowRelatedActLogsNearEntity=true
ShowRelatedActLogPanel=false
RelatedActLogFilters.Log14=true
RelatedActLogFilters.Log1A=true
RelatedActLogMaxLinesPerEntity=3
```

### 示例 B：写 TRN 读条触发器

目标：抓 Boss castid、source、target、位置。

建议：

```text
ShowRelatedActLogs=true
RelatedActLogFilters.Log14=true
RelatedActLogFilters.Log107=true
RelatedActLogSimplify.Log14=true
RelatedActLogSimplify.Log14Alt=false
RelatedActLogSimplify.Log107=true
FilterPlayerAndPartyLog14=true
```

看到日志：

```text
14 Cast src=4000BAE9 37C3:阴阳五行 -> tgt=4000BAE9 青龙 t=3.700 pos=(98.45,98.21,0.03) h=1.79
107 CastPos src=4000BAE9 aid=37C3 pos=(98.45,98.21,0.03) h=1.79
```

可以写 TRN 正则：

```text
^.{15}\S+ 14:[^:]*:[^:]*:37C3:
```

或更稳定按 source/skill：

```text
^.{15}\S+ 107:4000BAE9:37C3:
```

### 示例 C：写 TRN 状态触发器

目标：抓 effectId 和目标。

建议：

```text
ShowRelatedActLogs=true
RelatedActLogFilters.Log1A=true
RelatedActLogSimplify.Log1A=true
RelatedActLogSimplify.Log1AAlt=false
RelatedActLogSimplify.Log1AAlt2=false
FilterPlayerAndPartyLog1A=false
```

临时关闭 `FilterPlayerAndPartyLog1A` 的原因：
- 有些机制状态是发给玩家的。
- 写触发器时需要先看到完整状态日志。
- 写完后可以再打开过滤，降低噪声。

### 示例 D：录屏回看右侧日志面板

目标：实体很多时，避免日志堆在实体旁互相遮挡；把详细日志集中到右侧面板。

建议：

```text
ShowRelatedActLogs=true
ShowRelatedActLogsNearEntity=false
ShowRelatedActLogPanel=true
RelatedActLogPanelSeconds=10
RelatedActLogPanelMaxLines=30
RelatedActLogFilters.Log14=true
RelatedActLogFilters.Log1A=true
RelatedActLogSimplify.Log14Alt=true
RelatedActLogSimplify.Log1AAlt=true
RelatedActLogSimplify.Log1A=false
RelatedActLogSimplify.Log1AAlt2=false
```

说明：
- `ShowRelatedActLogsNearEntity=false` 可以减少实体附近遮挡。
- `ShowRelatedActLogPanel=true` 会在右侧显示当前可见实体日志流。
- 右侧面板长日志会自动换行完整显示。
- `RelatedActLogPanelSeconds` 和 `RelatedActLogPanelMaxLines` 只控制右侧面板。

看到日志示例：

```text
#40007C9A 凯夫卡 -> 技能名：众神之像 | 技能ID：28D7 | 时间：4.700
#400077C2 Buff名:魔法储存：爆炎 | BuffID:5CB | Buff时间:9999.00 | Buff类型:00   - - ->>>   凯夫卡
```

### 示例 E：排查为什么实体没显示

步骤：

```text
ShowFilteredEntitiesForDebug=true
DebugOverlayStyle=true
MaxDisplayedEntities=80
MaxDistance=150
```

查看标签里的过滤原因：
- self
- party
- player-owned
- distance
- blacklist
- untargetable

排查完恢复：

```text
ShowFilteredEntitiesForDebug=false
DebugOverlayStyle=false
MaxDisplayedEntities=30
MaxDistance=100
```

### 示例 E：24 人/PvP 队友噪声过滤

目标：过滤自己左侧 24 人团队列表，不误过滤敌对玩家。

配置：

```text
FilterPartyMembers=true
FilterPlayerOwnedEntities=true
FilterPlayerAndPartyLog14=true
FilterPlayerAndPartyLog1A=true
```

验证：

```text

dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- capture-duty 24
```

期望：

```text
rosterSource=network type 11
rosterCount=24
matchedRosterPlayers=xx / 24
```

其中 `matchedRosterPlayers` 小于 24 通常表示有队友暂时不在当前 ObjectTable 可见范围，不代表 roster 失败。

## 10. 保存、还原和重启

### 保存配置

点击：`保存配置`

效果：
- 写入 `EntityEspPlugin.json`。
- 刷新 Diagnostics 环境信息。
- 如果 Overlay 已打开，会重启 Overlay 应用配置。

### 一键还原默认配置

点击：`一键还原默认配置`

效果：
- 用代码里的 `new EspConfig()` 覆盖当前配置。
- 当前默认已同步为用户当前配置。

注意：
- 这不会自动变成你之后手动改的新配置。
- 如果以后想把新配置再次设为默认，需要再次更新代码默认值。

### 重启 ACT

需要重启 ACT 的情况：
- DLL 更新后。
- 插件代码变化后。

不需要重启 ACT 的情况：
- 只是改页面配置并点击保存。

## 11. 控制台工具示例

后续游戏内存信息、队伍来源、实体抓取、签名验证、VFX 路径抓取，一律优先用控制台 tool，不放进 ACT Diagnostics。

常用命令：

```text
# 4 人本 roster + ObjectTable 抓取
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- capture-duty 4

# 8 人本 roster + ObjectTable 抓取
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- capture-duty 8

# 24 人本 roster + ObjectTable 抓取
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- capture-duty 24

# 当前实体列表
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- read-entities

# 当前硬目标
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- read-target

# 相机矩阵
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- read-camera
```

典型 8 人本结果：

```text
rosterSource=network type 11
rosterCount=8
players=8
matchedRosterPlayers=8 / 8
```

典型 24 人本结果：

```text
rosterSource=network type 11
rosterCount=24
players=65
matchedRosterPlayers=22 / 24
```

解释：
- rosterCount 是 ACT 网络日志里的队伍/团队人数。
- players 是当前 ObjectTable 可见玩家数。
- matchedRosterPlayers 是 roster 中当前能在 ObjectTable 里匹配到的人数。

## 12. 客户端 .avfx 路径抓取

### 显示左上角 VFX 列表

字段：`ShowRecentVfxPanel`

效果：
- 开启：在游戏左上角显示一个小透明窗口，实时列出插件内置监控扫描到的 `.avfx`。
- 关闭：隐藏这个左上角 VFX 列表。

默认：开启。

相关字段：
- `RecentVfxWindowSeconds`：VFX 保留窗口秒数，只控制已经扫到的路径在面板里保留多久，不控制扫描速度。
- `RecentVfxDisplaySeconds`：`NEW` 高亮秒数，只控制新路径显示 `NEW` 的时间，不控制扫描速度。
- `RecentVfxMaxLines`：左上角最多显示多少行 VFX 路径。

默认：

```text
RecentVfxWindowSeconds = 30
RecentVfxDisplaySeconds = 12
RecentVfxMaxLines = 12
```

显示位置：
- 固定在游戏左上角。
- 不绑定实体。
- 不显示在实体标签上方。
- 不追加到 `Log:` 行尾。
- 不和 `14` 日志读条进度条混合。

显示内容：

```text
Live VFX keep 30s / NEW 12s / max 12
NEW 03s vfx/common/eff/m0532_stlp2c0x.avfx
LIVE 03s vfx/monster/m0532/eff/m0532sp_03c0x.avfx
```

原因：
- `.avfx` 可能是实体特效、场地特效、通用施法特效或资源加载特效。
- 它不一定能归属到某个实体。
- 不走 abilityId 候选逻辑，避免场地特效或非实体特效看不见。

实时监控说明：
- 不再启动独立 `EntityEspProbe.exe`。
- 不再写入 `cast-vfx-*.log` 作为实时显示中间文件。
- 插件加载时自动启动内置后台扫描线程。
- ACT 卸载插件或 ACT 关闭时停止扫描线程。
- Overlay 直接读取内存扫描服务的实时快照。
- 监控使用增量热点扫描：每轮优先扫上次命中过 `.avfx` 的热点内存区域，再分片推进其他区域，避免每轮完整扫 5~6GB 造成明显延迟。
- `NEW` 表示最近新出现的路径；`LIVE` 表示最近扫描仍存在的路径，更接近当前画面/当前资源池正在用到的特效。
- 配置页的 `启用 VFX 监控与左上角列表` 是总开关：开启时启动后台扫描并显示左上角面板；关闭时隐藏面板并停止后台扫描线程。
- 插件页的 `复制最近 VFX 的 TRN 复现片段` 会把最近一条路径转成 ActorVfx / Channeling / PictoACT StaticVfx 片段复制到剪贴板，详见 `docs/trn-vfx-replay.md`。
- 如果左上角显示 `no live .avfx yet`，说明监控线程还没完成首次有效扫描或 FF14 进程暂不可读。

用途：
- 当 Boss 释放技能时，抓取客户端真实加载的 `.avfx` 路径。
- 后续辅助写 Triggernometry / PostNamazu ActorVfx / PictoACT AOE。

重要限制：
- ACT 解析日志和 Network 日志本身不包含 `.avfx` 路径。
- `.avfx` 路径来自 FF14 客户端内存中的真实资源字符串。
- 不要在 ACT 插件 UI 线程做全内存扫描。
- 采集结果是候选路径，不是一次就能确认 1:1 技能映射。

### 扫描当前客户端已加载 .avfx

```text
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- scan-avfx-memory
```

带关键词过滤：

```text
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- scan-avfx-memory m0532
```

示例输出：

```text
uniqueAvfx=1099
vfx/monster/m0532/eff/m0532sp_02c0x.avfx
vfx/monster/m0532/eff/m0532sp_19c0x.avfx
```

### 单次技能窗口抓取

```text
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- capture-cast-vfx 8
```

效果：
- 抓取前 baseline。
- 等待 8 秒。
- 抓取后对比新增 `.avfx`。
- 同时打印窗口内 `20/263/264` 技能日志。

### 一段时间自动监控

```text
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- monitor-cast-vfx 300 2
```

参数：
- `300`：监控 300 秒。
- `2`：每 2 秒扫描一次。

效果：
- 适合开打前启动。
- 打本期间自动记录新增 `.avfx` 和技能日志。
- 输出保存到：

```text
tools\EntityEspProbe\captures\cast-vfx-yyyyMMdd-HHmmss.log
```

监控正常结束后会自动生成候选映射：

```text
tools\EntityEspProbe\captures\ability-vfx-candidates-yyyyMMdd-HHmmss.json
```

如果中途手动停止，也可以事后分析：

```text
dotnet run --project tools/EntityEspProbe/EntityEspProbe.csproj -- analyze-cast-vfx tools/EntityEspProbe/captures/cast-vfx-20260523-173554.log
```

### 候选映射含义

示例：

```json
{
  "abilityId": "37C3",
  "abilityName": "阴阳五行",
  "sourceNames": {
    "青龙": 1
  },
  "candidateAvfx": [
    {
      "path": "vfx/monster/m0532/eff/m0532sp_19c0x.avfx",
      "score": 1
    }
  ]
}
```

解释：
- `abilityId` 是技能 ID，写 TRN 时优先用它。
- `candidateAvfx` 是时间窗口内新增的真实 `.avfx` 候选。
- `score` 是多轮采集中同一技能附近重复出现的次数。
- 分数越高越可能是真关联。

注意：
- 2 秒窗口比 5 秒窗口更准。
- 同一窗口如果多个技能同时出现，候选会混杂。
- 多打几轮后用重复交集确认。

### TRN / PostNamazu ActorVfx 示例

确认路径后，可以用 ActorVfx 播放原生特效：

```xml
<Action ActionType="NamedCallback"
        NamedCallbackName="ActorVfx"
        NamedCallbackParam="${_entity[${sid}].Address}, ${_entity[${sid}].Address}, vfx/monster/m0532/eff/m0532sp_19c0x.avfx" />
```

如果要画稳定 AOE 范围，优先结合：
- `14` / `20` 技能 ID。
- `107` / `263` 起手坐标和朝向。
- `108` / `264` 技能落点。
- PictoACT 的 Circle / Rect / Fan 绘制。

`.avfx` 更适合确认客户端原生视觉，AOE 几何仍建议用日志坐标和 PictoACT 明确绘制。
