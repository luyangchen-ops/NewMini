# 抱刀墨息待机动画

本组资产包含一次性收刀过渡，以及可循环播放的抱刀墨息待机动画。

## 本次修复

- 原洋红底图中的人物跨过了固定的 576 像素行边界，统一网格裁切会截断第 1、5、6、8 张的脚部。
- 现改为按原图真实空白分界逐行独立提取：`0–609`、`609–1157`、`1157–1728`。
- 每帧重新抠成 512×576 RGBA PNG，人物脚底统一落在画布顶部坐标 `Y=532`。
- 双脚中心统一在画布顶部坐标 `X=204`；Unity Pivot 为底部坐标 `(0.3984375, 0.0763888889)`，即像素 `(204, 44)`。
- 透明图集由 Unity MCP 配置为 Multiple Sprite，按 4×3、单格 512×576 自动切分。
- `Hero_IdleHoldInkEcho.anim` 已原位更新为 8 FPS 循环 Clip，仅驱动 `SpriteRenderer.m_Sprite`，不包含 Transform 位移曲线。

## 动作内容

- `Sheathe`：8 帧收刀过渡，建议 12 FPS 单次播放。
- `IdleHoldInkEcho`：12 帧抱刀呼吸循环，第 5–8 帧出现贴身黑墨人物残影。

## 资产位置

- 独立帧：`Sprites/IdleHoldInkEcho/IdleHoldInkEcho_00.png`–`IdleHoldInkEcho_11.png`
- 透明图集：`Sheets/Hero_IdleHoldInkEcho_12Frames_Transparent.png`
- 动画 Clip：`../Animations/Clips/Hero_IdleHoldInkEcho.anim`

纹理规格为 100 PPU、关闭 Mip Map、关闭压缩。Animator 中原有的 Clip 引用不需要重新绑定。
