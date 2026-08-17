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
    [SerializeField] private GameObject guardArcherPrefab;
    [SerializeField] private Transform[] guardArcherSpawnPoints;

    [Header("Encounter Completion")]
    [SerializeField] private ArenaCombatZone arenaCombatZone;

    [Header("Authored Boss HUD")]
    [SerializeField] private GameObject bossHudRoot;
    [SerializeField] private Text contractCountText;

    [Header("Existing Dialogue Presentation")]
    [SerializeField] private ClickDialogueSystem dialogueSystem;
    [SerializeField] private TextAsset postBossDialogue;
    [SerializeField] private Transform playerSpeaker;
    [SerializeField] private Transform npcSpeakerAnchor;
    [SerializeField, Min(0f)] private float postBossDialogueDelay = 1f;
    [SerializeField] private GameStateUIController gameStateUi;

    private EnemyAgent activeBoss;
    public Transform ActiveBossTransform => activeBoss != null ? activeBoss.transform : null;
    public Vector3 ActiveBossFocusPosition
    {
        get
        {
            if (activeBoss == null) return transform.position;
            SpriteRenderer renderer = activeBoss.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.bounds.center : activeBoss.transform.position;
        }
    }
    private readonly System.Collections.Generic.List<EnemyAgent> guardArchers = new();
    private bool bossSpawned;
    private bool bossDefeated;
    private bool arenaCleared;
    private bool epilogueStarted;

    private void Awake()
    {
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
    }

    /// <summary>Persistent event target for Arena 05's first wave.</summary>
    public void SpawnBossWithFirstWave(int waveIndex)
    {
        if (waveIndex == 0) SpawnBoss();
    }

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
        if (activeBoss != null) activeBoss.Died += HandleBossDied;
        else Debug.LogError("Borrowed-life boss prefab needs an EnemyAgent component.", instance);

        SpawnGuardArchers();
    }

    /// <summary>Persistent event target for Arena 05's authored clear event.</summary>
    public void NotifyArenaCleared()
    {
        arenaCleared = true;
        TryStartEpilogue();
    }

    private void SpawnGuardArchers()
    {
        if (guardArcherPrefab == null || guardArcherSpawnPoints == null) return;
        foreach (Transform spawnPoint in guardArcherSpawnPoints)
        {
            if (spawnPoint == null) continue;
            EnemyAgent archer = Instantiate(guardArcherPrefab, spawnPoint.position, spawnPoint.rotation, spawnedBossParent)
                .GetComponent<EnemyAgent>();
            if (archer != null) { archer.enabled = false; guardArchers.Add(archer); }
        }
    }

    public void PlayGuardArcherPresentation(PlayerCharacterController target)
    {
        foreach (EnemyAgent archer in guardArchers)
            if (archer != null) archer.PlayCinematicRangedShot(target != null ? target.transform : null);
    }

    public void ActivateGuardArchers()
    {
        foreach (EnemyAgent archer in guardArchers)
            if (archer != null) archer.enabled = true;
    }

    public void ShowBossHud()
    {
        if (bossHudRoot != null) bossHudRoot.SetActive(true);
    }

    private void HandleBossDied(EnemyAgent boss)
    {
        bossDefeated = true;
        if (activeBoss != null) activeBoss.Died -= HandleBossDied;
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
        TryStartEpilogue();
    }

    private void TryStartEpilogue()
    {
        if (epilogueStarted || !bossDefeated || !arenaCleared) return;
        epilogueStarted = true;
        StartCoroutine(PlayPostBossDialogue());
    }

    private IEnumerator PlayPostBossDialogue()
    {
        if (postBossDialogueDelay > 0f) yield return new WaitForSeconds(postBossDialogueDelay);
        gameStateUi ??= FindAnyObjectByType<GameStateUIController>();
        if (dialogueSystem == null)
        {
            gameStateUi?.ShowVictory();
            yield break;
        }

        dialogueSystem.DialogueFinished += ShowVictoryAfterEpilogue;
        dialogueSystem.StartDialogue(postBossDialogue, playerSpeaker, npcSpeakerAnchor);
    }

    private void ShowVictoryAfterEpilogue()
    {
        dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
        gameStateUi?.ShowVictory();
    }

    private void OnDisable()
    {
        if (activeBoss != null) activeBoss.Died -= HandleBossDied;
        if (dialogueSystem != null) dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
    }
}
