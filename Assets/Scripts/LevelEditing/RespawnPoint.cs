using UnityEngine;
using UnityEngine.Events;

/// <summary>A scene-authored position in the RespawnPointManager sequence.</summary>
[DisallowMultipleComponent]
public sealed class RespawnPoint : MonoBehaviour
{
    [SerializeField] private string pointId = "checkpoint_01";
    [SerializeField] private bool activeOnLevelStart;
    [SerializeField] private bool activateOnPlayerEnter = true;
    [SerializeField] private Collider2D activationTrigger;
    [SerializeField] private UnityEvent onActivated;

    public string PointId => pointId;
    public Vector3 RespawnPosition => transform.position;
    public Collider2D ActivationTrigger => activationTrigger;
    public bool ActiveOnLevelStart => activeOnLevelStart;
    public int SequenceIndex { get; private set; } = -1;

    private RespawnPointManager manager;

    private void Reset()
    {
        activationTrigger = GetComponent<Collider2D>();
        if (activationTrigger != null) activationTrigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activateOnPlayerEnter && other.GetComponentInParent<PlayerCharacterController>() != null) Activate();
    }

    [ContextMenu("Set As Active Respawn Point")]
    public void Activate()
    {
        manager ??= RespawnPointManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"Respawn point '{name}' has no RespawnPointManager.", this);
            return;
        }

        manager.Activate(this);
    }

    internal void Bind(RespawnPointManager owner, int sequenceIndex)
    {
        manager = owner;
        SequenceIndex = sequenceIndex;
    }

    internal void NotifyActivated() => onActivated?.Invoke();

    private void OnDrawGizmos()
    {
        Gizmos.color = manager != null && manager.CurrentPoint == this || activeOnLevelStart ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, .45f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * .8f);
    }
}
