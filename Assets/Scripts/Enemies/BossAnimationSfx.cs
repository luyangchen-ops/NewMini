using UnityEngine;

/// <summary>Receives Boss animation events and plays the corresponding sound effects.</summary>
public sealed class BossAnimationSfx : MonoBehaviour
{
    [SerializeField] private AudioClip attackSfx;
    [SerializeField] private AudioClip tripleAttackSfx;
    [SerializeField] private AudioClip dashAttackSfx;
    [SerializeField] private AudioClip guardImpactSfx;
    [SerializeField] private AudioClip guardRepelSfx;
    [SerializeField] private AudioClip hurtSfx;
    [SerializeField] private AudioClip deathSfx;

    /// <summary>Called by the Boss_Attack animation at the swing impact frame.</summary>
    public void PlayAttackSfx() => Play(attackSfx);

    public void PlayTripleAttackSfx() => Play(tripleAttackSfx);
    public void PlayDashAttackSfx() => Play(dashAttackSfx);
    public void PlayGuardImpactSfx() => Play(guardImpactSfx);
    public void PlayGuardRepelSfx() => Play(guardRepelSfx);
    public void PlayHurtSfx() => Play(hurtSfx);
    public void PlayDeathSfx() => Play(deathSfx);

    private static void Play(AudioClip clip) => GameAudioManager.PlaySfx(clip);
}
