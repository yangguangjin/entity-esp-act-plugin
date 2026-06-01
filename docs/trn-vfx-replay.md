# TRN VFX 快速复现与分类

本文用于把 Entity ESP 监控到的 `.avfx` 路径手动填入 Triggernometry / PostNamazuExtension 里复现。

## 使用方式

当前插件页已移除旧的 `复制最近 VFX 的 TRN 复现片段` 按钮。需要复现时：

1. 在插件配置页开启 `显示 VFX 监控面板`。
2. 进入副本或触发机制，让 VFX 面板出现 `.avfx` 路径。
3. 手动复制面板里的完整 `.avfx` 路径。
4. 按下方 ActorVfx / Channeling / PictoACT 示例填入 Triggernometry。
5. `.avfx` 只用于复现客户端原生视觉；真正稳定的 AOE 范围仍优先用 `14/107/108` 日志坐标和 PictoACT 几何绘制。

## ActorVfx

截图中的用法是正确的：

```text
CallbackName: ActorVfx
CallbackParam: ${_me.Address}, ${_me.Address}, vfx/common/eff/m0941_seahorse_c0h.avfx
```

含义：

```text
sourceAddress, targetAddress, fullAvfxPath
```

常用快速测试：

```text
${_me.Address}, ${_me.Address}, <完整 .avfx 路径>
```

这样会在自己身上/自己脚下附近播放特效，用于确认路径能不能被 PostNamazuExtension 调起来。

## Channeling

如果路径是：

```text
vfx/channeling/eff/chn_x6rc_fr_share01x.avfx
vfx/channeling/eff/chn_x6rc_fr_tgae01x.avfx
```

它通常属于 channeling/连线/分摊/大圈提示类。

可以用底层 `ActorVfx` 测：

```text
CallbackName: ActorVfx
CallbackParam: ${_me.Address}, ${_me.Address}, vfx/channeling/eff/chn_x6rc_fr_share01x.avfx
```

也可以用 `Channeling` 快捷封装：

```text
CallbackName: Channeling
CallbackParam: ${_me.Address}, ${_me.Address}, chn_x6rc_fr_share01x
```

`Channeling` 会自动拼：

```text
vfx/channeling/eff/{name}.avfx
```

## 钢铁 / 月环 / 踩踏 / 点名 AOE

这些一般不是 `vfx/channeling/eff` 这一套。

更常见分类：

| 机制视觉 | 推荐 TRN 复现/绘制方式 | 说明 |
|---|---|---|
| 分摊连线 / 频道线 | `Channeling` 或 `ActorVfx` | 常见路径在 `vfx/channeling/eff/` |
| 头顶点名图标 | `LockOn` | 自动拼 `vfx/lockon/eff/` |
| Boss 专属技能特效 | `ActorVfx` | 可能在 `vfx/monster/mXXXX/eff/` 或 `vfx/common/eff/` |
| 圆形点名 AOE | `PictoACT Omen: Circle` | 比 ActorVfx 更稳定，可控半径/持续时间 |
| 钢铁 / 踩踏圆形范围 | `PictoACT Omen: Circle` | 以 boss/source 坐标为中心 |
| 月环 / 甜甜圈 | `PictoACT Omen: Donut` | 可控内外径 |
| 矩形直线 / 激光 | `PictoACT Omen: Rect` | 可控半宽/长度/朝向 |
| 扇形 | `PictoACT Omen: Fan90/Fan120/...` | 可控半径/角度/朝向 |

结论：

- 要“复现游戏特效本身”：用 `ActorVfx`。
- 要“稳定提示危险范围”：用 `PictoACT Omen`。
- 要“连线/分摊/大圈频道效果”：优先试 `Channeling`。

## PictoACT Omen 示例

钢铁/踩踏圆：

```text
CallbackName: PictoACT
CallbackParam:
Omen: Circle
Tag: TestCircle_${sid}
Pos: ${_entity[${sid}].x}, ${_entity[${sid}].y}, ${_entity[${sid}].z}
Scale: 12, 12, 1
Color: 1, 0.2, 0.2, 0.55
t: 5
```

月环：

```text
CallbackName: PictoACT
CallbackParam:
Omen: Donut
Tag: TestDonut_${sid}
Pos: ${_entity[${sid}].x}, ${_entity[${sid}].y}, ${_entity[${sid}].z}
Scale: 5, 30, 1
Color: 1, 0.2, 0.2, 0.55
t: 5
```

点名圆：

```text
CallbackName: PictoACT
CallbackParam:
Omen: Circle
Tag: Spread_${tid}
Pos: ${_entity[${tid}].x}, ${_entity[${tid}].y}, ${_entity[${tid}].z}
Scale: 6, 6, 1
Color: 1, 0.2, 0.2, 0.55
t: 5
```

## 注意

- `ActorVfx` 需要实体 Address，不是 EntityId。
- 快速测试用 `${_me.Address}` 最稳。
- 给目标测试时用 `${_entity[${tid}].Address}`，并加实体存在条件。
- 部分 `.avfx` 是播放片段，不一定包含完整危险范围。
- 有些 Boss 技能由 TMB/动画触发多个 VFX，单个 `.avfx` 可能只是一部分。
