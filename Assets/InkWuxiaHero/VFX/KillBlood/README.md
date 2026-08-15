# Ink Wuxia Kill Blood VFX

水墨武侠击杀喷血特效资产，共 4 组，每组 8 帧。正式项目位置：`Assets/InkWuxiaHero/VFX/KillBlood`。

## 目录

- `Source/`：Babylon / Banana Pro 生成的绿幕源帧表，用于留档和再次编辑。
- `Sheets/`：已去除绿幕与绿色溢色的 RGBA 透明帧表，用于总览和批量处理。
- `Sprites/<Effect>/`：游戏内优先使用的独立透明 Sprite，文件名统一为 `VFX_KillBlood_<Effect>_F01` 至 `F08`。
- `VFX_KillBlood_Manifest.json`：尺寸、帧序和相对路径清单。

## 特效命名

| Action | 中文用途 | 建议使用场景 |
| --- | --- | --- |
| `SlashFan` | 横斩扇形喷血 | 刀兵、普通近战斩杀 |
| `HeavyBurst` | 重击爆发喷血 | 冲刺攻击、重击、处决 |
| `MistDissolve` | 血墨雾化消散 | 敌人死亡消散、心流斩杀 |
| `GroundSplash` | 落地喷溅与血泊 | 尸体倒地、地面残留反馈 |

## 帧表规格

- 帧表尺寸：`2752 x 1536`
- 排列：`4 列 x 2 行`
- 单帧尺寸：`688 x 768`
- 顺序：上排从左到右为 `F01-F04`，下排从左到右为 `F05-F08`
- 循环：否
- 建议播放速度：`14-18 FPS`

## Unity 导入配置

- `Sprites/`：`Texture Type = Sprite (2D and UI)`、`Sprite Mode = Single`、中心 Pivot、`Pixels Per Unit = 100`
- `Sheets/` 与 `Source/`：作为完整帧表和源文件留档，保持单张 Texture 导入
- 全部关闭 Mip Maps，`Wrap Mode = Clamp`
- 透明 Sprite 使用 `Alpha Is Transparency = On`，默认平台关闭压缩，避免薄墨边缘出现色块
- 帧表如需重新切片：`Grid By Cell Size = 688 x 768`，顺序为从左到右、从上到下

本目录仅包含视觉素材，没有修改角色逻辑、Animator Controller 或动画状态机。
