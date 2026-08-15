# Ink Wuxia Hero Animation Kit

This folder is self-contained. Keep its `.meta` files when copying it into another Unity project.

## Included animations

- `Hero_Idle`: one-frame standing pose, looping
- `Hero_Run`: eight-frame run, looping
- `Hero_NormalAttack`: eight-frame normal attack
- `Hero_DashAttack`: eight-frame dash attack
- `Hero_Roll`: eight-frame evasive roll
- `Hero_Hurt`: eight-frame hit reaction
- `Hero_Death`: eight-frame death sequence

## Animator parameters

- `Speed` (Float): values greater than `0.1` enter Run; lower values return to Idle.
- `NormalAttack` (Trigger)
- `DashAttack` (Trigger)
- `Roll` (Trigger)
- `Hurt` (Trigger)
- `IsDead` (Bool): set to true to enter Death. Death has no automatic exit.

## Quick start

Drag `Prefabs/InkWuxiaHero.prefab` into the scene. Drive its Animator using the parameters above.

All frame textures are single Sprites with a bottom-center custom pivot, 100 pixels per unit, alpha transparency enabled, mipmaps disabled, and uncompressed texture import.
