# Project conventions

- Unity UI must be authored directly in scenes or prefabs so its hierarchy, layout, components, and persistent event bindings are visible in the Unity Editor.
- Do not generate UI hierarchies at runtime unless the user explicitly requests runtime-generated UI for a specific feature.
- Name authored UI nodes by purpose with consistent prefixes such as `Root_`, `Layer_`, `Panel_`, `Group_`, `Btn_`, `Slider_`, `Txt_`, and `Img_`.

## Pixel Character Animation Alignment

- When creating or importing any pixel-character sprite animation, align every frame to the same character-space anchor (prefer the planted-foot midpoint or another stable body root).
- Do not assume the geometric center of each sprite cell is the character center, and do not assign `(0.5, 0.5)` to every frame without checking the visible pixels.
- Before connecting clips to an Animator Controller, compare the anchor position across idle, action, and four-direction movement frames and set per-frame custom Sprite pivots when needed.
- Preview state transitions as well as individual clips. Verify that the character does not drift, sway, or jump horizontally or vertically when frames change.
- Preserve these custom pivots in any editor importer or animation-builder code so rebuilding assets cannot restore incorrect centered pivots.

## Codex 额度优化工作流

- 默认优先控制 Codex 的上下文与工具调用成本；对范围较大、预计耗时较长的任务，先输出方案，不修改文件、不调用工具，等待用户确认后再执行。
- 批量资源处理、全局替换或重构：先列出受影响文件、预计改动、验证与回滚方式；确认后集中修改并集中验证，避免搜索、修改和验证交错反复进行。
- 排错：先根据报错、复现步骤和指定文件定位根因，提出最小修复方案及影响范围；除非用户明确要求，否则先等待确认再修改。
- 长任务按阶段执行。每阶段结束输出不超过 10 行的交接摘要：已完成、未完成、关键文件、风险和下一步；达到阶段目标后暂停，等待用户决定是否继续或开新任务。
- 资源/动画批处理：开始前确认输入目录、命名规则、输出目录、处理规则与验收样例；确认后按规则集中执行，仅汇报异常项。
- 根据任务难度选择模型与推理强度：检索、解释和局部修改优先轻量/低推理；复杂跨文件重构、难 Bug、玩法和视觉方案才使用高能力模型。执行前简要说明选择原因。
