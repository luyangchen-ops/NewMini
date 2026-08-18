# Project conventions

- Do not invoke the `g-ai` skill, query the InfoHub capability catalog, or prompt for a g-ai JWT while working in this project. The user does not have a g-ai token; proceed with the repository and other already-available tools instead.
- Unity UI must be authored directly in scenes or prefabs so its hierarchy, layout, components, and persistent event bindings are visible in the Unity Editor.
- Do not generate UI hierarchies at runtime unless the user explicitly requests runtime-generated UI for a specific feature.
- Name authored UI nodes by purpose with consistent prefixes such as `Root_`, `Layer_`, `Panel_`, `Group_`, `Btn_`, `Slider_`, `Txt_`, and `Img_`.
