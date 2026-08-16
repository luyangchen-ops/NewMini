using UnityEngine;

public sealed class ShieldWarriorAnimationSfx : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float blockVolume = .82f;
    [SerializeField, Range(0f, 1f)] private float attackVolume = .9f;

    public void PlayShieldBlockSfx() =>
        GameAudioManager.PlaySfx(GameSfx.ShieldWarriorBlock, blockVolume);

    public void PlayShieldAttackSfx() =>
        GameAudioManager.PlaySfx(GameSfx.ShieldWarriorAttack, attackVolume);
}
