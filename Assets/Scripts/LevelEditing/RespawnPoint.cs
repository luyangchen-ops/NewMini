using UnityEngine;
using UnityEngine.Events;

/// <summary>Scene-authored checkpoint data for a level. The current active point is available through RespawnPoint.Active.</summary>
[DisallowMultipleComponent]
public sealed class RespawnPoint : MonoBehaviour
{
    [SerializeField] private string pointId = "checkpoint_01";
    [SerializeField] private bool activeOnLevelStart;
    [SerializeField] private bool activateOnPlayerEnter = true;
    [SerializeField] private Collider2D activationTrigger;
    [SerializeField] private UnityEvent onActivated;

    public static RespawnPoint Active { get; private set; }
    public string PointId => pointId;
    public Vector3 RespawnPosition => transform.position;
    public Collider2D ActivationTrigger => activationTrigger;

    private void Reset()
    {
        activationTrigger = GetComponent<Collider2D>();
        if (activationTrigger != null) activationTrigger.isTrigger = true;
    }

    private void Awake()
    {
        if (activeOnLevelStart) Activate();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activateOnPlayerEnter && other.GetComponentInParent<PlayerCharacterController>() != null) Activate();
    }

    [ContextMenu("Set As Active Respawn Point")]
    public void Activate()
    {
        if (Active == this) return;
        Active = this;
        onActivated?.Invoke();
    }

    public static bool TryGetActivePosition(out Vector3 position)
    {
        if (Active == null)
        {
            position = default;
            return false;
        }
        position = Active.RespawnPosition;
        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Active == this || activeOnLevelStart ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, .45f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * .8f);
    }
}
