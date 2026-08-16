using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerSpecialItemInventory : MonoBehaviour
{
    [SerializeField, Min(0)] private int shieldCharges;
    [SerializeField, Min(0)] private int throwingKnifeCount;
    [SerializeField, Min(0f)] private float potionHealing = 20f;
    [SerializeField, Min(0f)] private float knifeSpeed = 14f;
    [SerializeField, Min(0f)] private float knifeLifetime = 2f;

    private PlayerCharacterController player;

    public int ShieldCharges => shieldCharges;
    public int ThrowingKnifeCount => throwingKnifeCount;

    private void Awake() => player = GetComponent<PlayerCharacterController>();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            TryThrowKnife();
    }

    public void Collect(SpecialItemType itemType)
    {
        switch (itemType)
        {
            case SpecialItemType.OneHitShield:
                shieldCharges++;
                break;
            case SpecialItemType.HealingPotion:
                player?.RestoreHealth(potionHealing);
                break;
            case SpecialItemType.ThrowingKnife:
                throwingKnifeCount++;
                break;
        }
    }

    /// <summary>Called by the player damage entry point before health is reduced.</summary>
    public bool TryBlockAttack()
    {
        if (shieldCharges <= 0) return false;
        shieldCharges--;
        return true;
    }

    private void TryThrowKnife()
    {
        if (throwingKnifeCount <= 0 || player == null) return;

        Camera camera = Camera.main;
        Vector2 direction = Vector2.right;
        if (camera != null && Pointer.current != null)
        {
            Vector3 pointer = Pointer.current.position.ReadValue();
            pointer.z = -camera.transform.position.z;
            Vector2 offset = (Vector2)camera.ScreenToWorldPoint(pointer) - (Vector2)transform.position;
            if (offset.sqrMagnitude > .0001f) direction = offset.normalized;
        }

        throwingKnifeCount--;
        GameObject knifeObject = new GameObject("Projectile_ThrowingKnife");
        knifeObject.transform.position = transform.position + (Vector3)(direction * .35f);
        knifeObject.transform.right = direction;
        knifeObject.AddComponent<ThrowingKnifeProjectile>().Launch(direction, knifeSpeed, knifeLifetime, gameObject);
    }
}
