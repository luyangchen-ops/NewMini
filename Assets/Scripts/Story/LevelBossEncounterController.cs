using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored first-level boss handoff: the final regular wave summons Qiu Jiu,
/// his 99 borrowed lives are presented on the HUD, and his defeat starts the epilogue.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelBossEncounterController : MonoBehaviour
{
    [Header("Authored Boss Spawn")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Transform spawnedBossParent;

    [Header("Authored Boss HUD")]
    [SerializeField] private GameObject bossHudRoot;
    [SerializeField] private Text contractCountText;

    [Header("Existing Dialogue Presentation")]
    [SerializeField] private ClickDialogueSystem dialogueSystem;
    [SerializeField] private TextAsset postBossDialogue;
    [SerializeField] private Transform playerSpeaker;
    [SerializeField] private Transform npcSpeakerAnchor;
    [SerializeField, Min(0f)] private float postBossDialogueDelay = 1f;

    private EnemyAgent activeBoss;
    private bool bossSpawned;
    private bool epilogueStarted;

    private void Awake()
    {
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
    }

    /// <summary>Persistent event target for Arena 05's final regular wave.</summary>
    public void SpawnBoss()
    {
        if (bossSpawned || bossPrefab == null || bossSpawnPoint == null) return;
        bossSpawned = true;

        GameObject instance = Instantiate(
            bossPrefab,
            bossSpawnPoint.position,
            bossSpawnPoint.rotation,
            spawnedBossParent);
        instance.name = "Boss_借命阎罗_裘九";
        activeBoss = instance.GetComponent<EnemyAgent>();

        BorrowedLifeBossController borrowedLife = instance.GetComponent<BorrowedLifeBossController>();
        borrowedLife?.ConfigurePresentation(contractCountText);
        if (bossHudRoot != null) bossHudRoot.SetActive(true);

        if (activeBoss != null) activeBoss.Died += HandleBossDied;
        else Debug.LogError("Borrowed-life boss prefab needs an EnemyAgent component.", instance);
    }

    private void HandleBossDied(EnemyAgent boss)
    {
        if (epilogueStarted) return;
        epilogueStarted = true;
        if (activeBoss != null) activeBoss.Died -= HandleBossDied;
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
        StartCoroutine(PlayPostBossDialogue());
    }

    private IEnumerator PlayPostBossDialogue()
    {
        if (postBossDialogueDelay > 0f) yield return new WaitForSeconds(postBossDialogueDelay);
        dialogueSystem?.StartDialogue(postBossDialogue, playerSpeaker, npcSpeakerAnchor);
    }

    private void OnDisable()
    {
        if (activeBoss != null) activeBoss.Died -= HandleBossDied;
    }
}
