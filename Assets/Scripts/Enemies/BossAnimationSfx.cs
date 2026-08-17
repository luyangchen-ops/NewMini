using UnityEngine;

/// <summary>Receives Boss animation events and plays the corresponding sound effects.</summary>
public sealed class BossAnimationSfx : MonoBehaviour
{
    [SerializeField] private AudioClip attackSfx;

    /// <summary>Called by the Boss_Attack animation at the swing impact frame.</summary>
    public void PlayAttackSfx() => GameAudioManager.PlaySfx(attackSfx);
}
