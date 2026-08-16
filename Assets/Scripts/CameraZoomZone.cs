using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Level/Camera Zoom Zone")]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CameraZoomZone : MonoBehaviour
{
    [Header("Camera Zoom")]
    [Tooltip("Target orthographic size while the player is inside. Smaller values zoom in; larger values zoom out.")]
    [SerializeField, Min(.1f)] private float targetOrthographicSize = 4f;
    [Tooltip("How quickly the camera reaches the target size after entering the zone.")]
    [SerializeField, Min(.01f)] private float enterBlendSpeed = 6f;
    [Tooltip("How quickly the camera returns after leaving the zone.")]
    [SerializeField, Min(.01f)] private float exitBlendSpeed = 6f;
    [Tooltip("When zones overlap, the higher priority zone controls the camera. The latest zone wins when priorities match.")]
    [SerializeField] private int priority;

    private readonly HashSet<Collider2D> playerContacts = new HashSet<Collider2D>();
    private PlayerCharacterController activePlayer;

    private void Reset()
    {
        BoxCollider2D zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
        zoneCollider.size = new Vector2(8f, 6f);
    }

    private void OnValidate()
    {
        targetOrthographicSize = Mathf.Max(.1f, targetOrthographicSize);
        enterBlendSpeed = Mathf.Max(.01f, enterBlendSpeed);
        exitBlendSpeed = Mathf.Max(.01f, exitBlendSpeed);

        BoxCollider2D zoneCollider = GetComponent<BoxCollider2D>();
        if (zoneCollider != null) zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerCharacterController player = other.GetComponentInParent<PlayerCharacterController>();
        if (player == null || activePlayer != null && activePlayer != player) return;

        bool wasEmpty = playerContacts.Count == 0;
        playerContacts.Add(other);
        if (!wasEmpty) return;

        activePlayer = player;
        activePlayer.EnterCameraZoomZone(this, targetOrthographicSize, enterBlendSpeed, priority);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerContacts.Remove(other) || playerContacts.Count > 0) return;
        RestoreActivePlayer();
    }

    private void OnDisable()
    {
        RestoreActivePlayer();
    }

    private void RestoreActivePlayer()
    {
        if (activePlayer != null) activePlayer.ExitCameraZoomZone(this, exitBlendSpeed);
        activePlayer = null;
        playerContacts.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D zoneCollider = GetComponent<BoxCollider2D>();
        if (zoneCollider == null) return;

        Gizmos.color = new Color(.25f, .75f, 1f, .28f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(zoneCollider.offset, zoneCollider.size);
        Gizmos.color = new Color(.25f, .75f, 1f, .9f);
        Gizmos.DrawWireCube(zoneCollider.offset, zoneCollider.size);
        Gizmos.matrix = previousMatrix;
    }
}
