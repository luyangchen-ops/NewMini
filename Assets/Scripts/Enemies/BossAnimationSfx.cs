using UnityEngine;

/// <summary>Receives Boss animation events and plays the corresponding sound effects.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class BossAnimationSfx : MonoBehaviour
{
    [SerializeField] private AudioClip attackSfx;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    /// <summary>Called by the Boss_Attack animation at the swing impact frame.</summary>
    public void PlayAttackSfx()
    {
        if (attackSfx != null) audioSource.PlayOneShot(attackSfx);
    }
}
