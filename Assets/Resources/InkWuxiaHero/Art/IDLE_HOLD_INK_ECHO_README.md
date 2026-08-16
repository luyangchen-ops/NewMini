# 抱刀墨息待机动画

本组资产包含一个单次收刀过渡，以及一个可循环的抱刀待机动画。角色画布、导入尺寸和锚点与原始 `Idle_00.png` 保持一致。

## 动作内容

- `Sheathe`：8 帧。垂刀、抬腕、横刀对鞘、滑入剑鞘、剑格卡鞘，最后进入扶刀站姿。
- `IdleHoldInkEcho`：12 帧。抱刀呼吸、披风与围巾次级摆动，第 5–8 帧出现贴身黑墨人物残影，随后破碎消散并回到循环起点。

## 规格

- 独立帧：512×576 RGBA PNG。
- Pixels Per Unit：100。
- Pivot：Bottom Center `(0.5, 0.05)`。
- 纹理压缩：关闭。
- Mip Map：关闭。
- 建议 `Sheathe` 以 12 FPS 单次播放，完成后切入 `IdleHoldInkEcho`。
- 建议 `IdleHoldInkEcho` 以 8 FPS 循环播放。

## 目录

- `Sprites/Sheathe/Sheathe_00.png`–`Sheathe_07.png`
- `Sprites/IdleHoldInkEcho/IdleHoldInkEcho_00.png`–`IdleHoldInkEcho_11.png`
- `Sheets/Hero_Sheathe_8Frames_Transparent.png`
- `Sheets/Hero_IdleHoldInkEcho_12Frames_Transparent.png`

本次只交付图片、Unity `.meta`、说明和清单，没有修改 Animator、Animation Clip、Prefab、场景或角色逻辑。
