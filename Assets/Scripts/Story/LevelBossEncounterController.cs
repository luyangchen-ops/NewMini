using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored first-level boss handoff: the final regular wave summons Qiu Jiu,
/// his borrowed lives are presented on the HUD, and his defeat starts the epilogue.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelBossEncounterController : MonoBehaviour
{
    private const string BossDialogueSpeakerName = "\u88D8\u4E5D";

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
    private Coroutine epilogueRoutine;

    private void Awake()
    {
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
        if (arenaCombatZone != null) arenaCombatZone.ZoneReset += ResetEncounterForRetry;
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
        epilogueRoutine = StartCoroutine(PlayPostBossDialogue());
    }

    private IEnumerator PlayPostBossDialogue()
    {
        if (postBossDialogueDelay > 0f) yield return new WaitForSecondsRealtime(postBossDialogueDelay);
        gameStateUi ??= FindAnyObjectByType<GameStateUIController>();
        if (dialogueSystem == null || postBossDialogue == null)
        {
            gameStateUi?.ShowVictory();
            epilogueRoutine = null;
            yield break;
        }

        dialogueSystem.DialogueFinished += ShowVictoryAfterEpilogue;
        Transform bossCorpse = activeBoss != null ? activeBoss.transform : npcSpeakerAnchor;
        bool dialogueStarted = dialogueSystem.StartDialogueWithOffscreenSpeakerAtLowerScreen(
            postBossDialogue,
            playerSpeaker,
            npcSpeakerAnchor,
            BossDialogueSpeakerName,
            bossCorpse);
        epilogueRoutine = null;
        if (dialogueStarted) yield break;

        dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
        Debug.LogError("Boss epilogue dialogue could not start; showing victory as a fallback.", this);
        gameStateUi?.ShowVictory();
    }

    private void ShowVictoryAfterEpilogue()
    {
        dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
        gameStateUi?.ShowVictory();
    }

    private void ResetEncounterForRetry()
    {
        if (epilogueRoutine != null) StopCoroutine(epilogueRoutine);
        epilogueRoutine = null;
        if (dialogueSystem != null) dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;

        if (activeBoss != null)
        {
            activeBoss.Died -= HandleBossDied;
            Destroy(activeBoss.gameObject);
        }
        activeBoss = null;

        foreach (EnemyAgent guardArcher in guardArchers)
            if (guardArcher != null) Destroy(guardArcher.gameObject);
        guardArchers.Clear();

        bossSpawned = false;
        bossDefeated = false;
        arenaCleared = false;
        epilogueStarted = false;
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (activeBoss != null) activeBoss.Died -= HandleBossDied;
        if (dialogueSystem != null) dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
    }

    private void OnDestroy()
    {
        if (arenaCombatZone != null) arenaCombatZone.ZoneReset -= ResetEncounterForRetry;
    }
}
