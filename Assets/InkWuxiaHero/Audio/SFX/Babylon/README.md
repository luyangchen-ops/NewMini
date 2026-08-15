# Babylon 战斗音效交接说明

本目录保存由 Babylon `kling-text-to-audio` 生成的原始音频和已经裁切、去直流、淡入淡出、响度整理后的 Unity 运行时音频。未包含 `BABYLON_API_KEY`、JWT 或其他认证信息。

## 目录

- `Source/`：Babylon 返回的原始 MP3，用于以后重新挑选片段或再次处理。
- `Runtime/`：建议在游戏中绑定的最终 WAV，均为 44.1 kHz、16-bit PCM、单声道。

## Runtime 资产

| 文件 | 时长 | 建议触发点 |
| --- | ---: | --- |
| `Hero_PerfectDodge_SFX.wav` | 0.950 秒 | 完美闪避确认；锐利弹反瞬态后带短促空气抽离感 |
| `Hero_BulletTime_Loop.wav` | 4.650 秒 | 子弹时间期间循环；低通环境层和低沉心跳，不应完全静音 |
| `Hero_Dash_WindCut_SFX.wav` | 0.550 秒 | 普通闪避或攻击冲刺开始时播放 |
| `Hero_Hit_BladeFlesh_SFX.wav` | 0.700 秒 | 确认造成伤害时播放；刀刃瞬态叠加肉感低频 |
| `Hero_Kill_Confirm_SFX.wav` | 0.620 秒 | 击杀确认时使用独立 AudioSource 播放并按连杀数升调 |
| `Hero_KillChainEnd_Sheathe_SFX.wav` | 0.750 秒 | 连杀真正结束时播放，同时结束子弹时间环境层 |

## 后续绑定规则

1. 完美闪避确认时，先播放 `Hero_PerfectDodge_SFX.wav`，环境声应立即抽低；约 55 ms 后淡入 `Hero_BulletTime_Loop.wav`。
2. 子弹时间层使用单独循环 AudioSource，推荐音量约为普通 SFX 的 42%。不要通过总音量把所有声音完全静音。
3. 冲刺风切声与攻击动作声可叠加，但命中声只在伤害真正确认后播放。
4. 击杀确认声使用独立 AudioSource，避免修改 pitch 时影响同时播放的命中声。第 1 杀保持原调，此后每杀升约 0.45 个半音，并将连杀数限制在 1–5：`pitch = 2 ^ (((clamp(killCount, 1, 5) - 1) * 0.45) / 12)`。
5. 连杀结束时播放收刀声；子弹时间循环和环境压低量都应在 150 ms 内恢复。攻击冲刺暂时离开选敌状态时不要误判为连杀结束。
6. 推荐 Unity 导入设置：短音效使用 `Decompress On Load + PCM`；子弹时间循环使用 `Compressed In Memory + Vorbis`（Quality 约 0.82）并开启 Loop。

## Babylon 生成记录

- 会话：`minigame_wuxia_sfx_v2`
- 完美闪避：`kling_0affa24c4391`
- 子弹时间：`kling_8147a22f3f67`
- 冲刺：`kling_aef378dbffa9`
- 命中：`kling_6980525e499a`
- 击杀：`kling_8552b8ed3cae`
- 连杀结束：`kling_6afef9b62253`

按本次交接要求，当前未修改角色逻辑、场景或现有音频绑定，后续由玩法负责人基于最新角色代码接入。
