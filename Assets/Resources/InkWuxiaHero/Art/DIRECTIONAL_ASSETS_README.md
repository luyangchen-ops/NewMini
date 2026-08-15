# Ink Wuxia Hero Directional Combat Sprites

本目录包含新版水墨主角的方向性跑步与普通攻击视觉资产，共 52 张独立透明帧。

## 动作与位置

- 横向跑步：`Sprites/Run/Run_00.png` 至 `Run_11.png`，12 帧。`Run_00` 至 `Run_07` 已保留原 GUID 并替换旧画面；向左移动可在后续绑定时水平翻转。
- 向上跑步：`Sprites/RunUp/RunUp_00.png` 至 `RunUp_11.png`，12 帧。
- 向下跑步：`Sprites/RunDown/RunDown_00.png` 至 `RunDown_11.png`，12 帧。
- 向上普通攻击：`Sprites/NormalAttackUp/NormalAttackUp_00.png` 至 `NormalAttackUp_07.png`，8 帧。
- 向下普通攻击：`Sprites/NormalAttackDown/NormalAttackDown_00.png` 至 `NormalAttackDown_07.png`，8 帧。

对应的五张 4K 透明帧表位于 `Sheets/`，完整路径、帧序和建议帧率见 `Hero_DirectionalCombat_Manifest.json`。

## 统一规范

- 独立帧：`1024 x 768`、RGBA PNG。
- 外圈至少 16 px 全透明，避免相邻帧串边。
- Unity 导入：Sprite (2D and UI)、Single、PPU 100、Pivot `(0.5, 0.05)`。
- Mip Maps 关闭、Wrap Mode 为 Clamp、纹理压缩关闭。
- 原始透明帧表：`3840 x 2160`，从左至右、从上至下读取。

## 集成说明

本次只交付并替换视觉素材，没有创建或修改动画 Clip、Animator Controller、角色脚本、Prefab、场景或输入绑定。新增方向动作需由后续角色逻辑改造统一接入。
