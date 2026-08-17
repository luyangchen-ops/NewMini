using System.Collections;
using System.Collections.Generic;
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
    private const string BossDeathDialogueCue = "boss_death";

    [Header("Authored Boss Spawn")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Transform spawnedBossParent;
    [SerializeField] private GameObject guardArcherPrefab;
    [SerializeField] private Transform[] guardArcherSpawnPoints;

    [Header("Post-Wave Boss Reinforcements")]
    [SerializeField] private ArenaWaveSpawner bossWaveSpawner;
    [SerializeField] private GameObject[] reinforcementPrefabs;
    [SerializeField] private Transform[] reinforcementSpawnPoints;
    [SerializeField, Min(1)] private int reinforcementCount = 5;
    [SerializeField, Min(0f)] private float reinforcementSpawnDelay = 10f;

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
    private BossCombatController activeBossCombat;
    private bool bossPresentationIdleActive;
    private bool bossAgentWasEnabled;
    private bool bossCombatWasEnabled;
    private static readonly int BossIdle = Animator.StringToHash("Idle");
    private static readonly int BossIsMoving = Animator.StringToHash("IsMoving");
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
    private readonly List<EnemyAgent> guardArchers = new();
    private readonly List<EnemyAgent> spawnedReinforcements = new();
    private Coroutine reinforcementRoutine;
    private bool bossSpawned;
    private bool bossDefeated;
    private bool arenaCleared;
    private bool epilogueStarted;
    private Coroutine epilogueRoutine;

    private void Awake()
    {
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
        if (arenaCombatZone != null) arenaCombatZone.ZoneReset += ResetEncounterForRetry;
        if (bossWaveSpawner != null)
            bossWaveSpawner.AllWavesClearedEvent.AddListener(HandleInitialWavesCleared);
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
        activeBossCombat = instance.GetComponent<BossCombatController>();

        BorrowedLifeBossController borrowedLife = instance.GetComponent<BorrowedLifeBossController>();
        borrowedLife?.ConfigurePresentation(contractCountText);
        if (activeBoss != null)
        {
            activeBoss.SetDeathAnimationDeferred(true);
            activeBoss.Died += HandleBossDied;
        }
        else Debug.LogError("Borrowed-life boss prefab needs an EnemyAgent component.", instance);

        SpawnGuardArchers();
    }

    /// <summary>Keeps the authored Boss visible and idling while the pre-fight dialogue plays.</summary>
    public void SetBossPresentationIdle(bool active)
    {
        if (activeBoss == null) return;

        if (active)
        {
            if (bossPresentationIdleActive) return;
            bossPresentationIdleActive = true;
            activeBossCombat ??= activeBoss.GetComponent<BossCombatController>();
            bossAgentWasEnabled = activeBoss.enabled;
            bossCombatWasEnabled = activeBossCombat != null && activeBossCombat.enabled;
            activeBoss.enabled = false;
            if (activeBossCombat != null) activeBossCombat.enabled = false;

            Rigidbody2D bossBody = activeBoss.Body;
            if (bossBody != null) bossBody.linearVelocity = Vector2.zero;
            Animator animator = activeBoss.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Hurt");
                animator.ResetTrigger("Guard");
                animator.ResetTrigger("DashAttack");
                animator.ResetTrigger("TripleAttack");
                animator.SetBool(BossIsMoving, false);
                animator.Play(BossIdle, 0, 0f);
                animator.Update(0f);
            }
            return;
        }

        if (!bossPresentationIdleActive) return;
        bossPresentationIdleActive = false;
        activeBoss.enabled = bossAgentWasEnabled;
        if (activeBossCombat != null) activeBossCombat.enabled = bossCombatWasEnabled;
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
        SetPlayerEpilogueIdle(true);
        StopReinforcementLoop();
        KillAllBossArenaMinions();
        if (activeBoss != null) activeBoss.Died -= HandleBossDied;
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
        TryStartEpilogue();
    }

    private void HandleInitialWavesCleared()
    {
        if (bossDefeated || activeBoss == null || activeBoss.IsDead || reinforcementRoutine != null) return;
        reinforcementRoutine = StartCoroutine(RunReinforcementLoop());
    }

    private IEnumerator RunReinforcementLoop()
    {
        while (!bossDefeated && activeBoss != null && !activeBoss.IsDead)
        {
            yield return new WaitUntil(() => bossDefeated
                || activeBoss == null
                || activeBoss.IsDead
                || !HasAliveBossArenaMinions());
            if (bossDefeated || activeBoss == null || activeBoss.IsDead) break;

            float remainingDelay = reinforcementSpawnDelay;
            while (remainingDelay > 0f)
            {
                if (bossDefeated || activeBoss == null || activeBoss.IsDead) break;
                if (HasAliveBossArenaMinions())
                {
                    remainingDelay = reinforcementSpawnDelay;
                    yield return new WaitUntil(() => bossDefeated
                        || activeBoss == null
                        || activeBoss.IsDead
                        || !HasAliveBossArenaMinions());
                    continue;
                }

                remainingDelay -= Time.deltaTime;
                yield return null;
            }

            if (bossDefeated || activeBoss == null || activeBoss.IsDead) break;
            if (!HasAliveBossArenaMinions()) SpawnRandomReinforcementBatch();
        }

        reinforcementRoutine = null;
    }

    private bool HasAliveBossArenaMinions()
    {
        foreach (EnemyAgent enemy in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
        {
            if (enemy == null || enemy == activeBoss || enemy.IsDead) continue;
            if (arenaCombatZone == null || arenaCombatZone.ContainsPosition(enemy.transform.position)) return true;
        }
        return false;
    }

    private void SpawnRandomReinforcementBatch()
    {
        if (reinforcementPrefabs == null || reinforcementPrefabs.Length == 0
            || reinforcementSpawnPoints == null || reinforcementSpawnPoints.Length == 0) return;

        List<Transform> availablePoints = new(reinforcementSpawnPoints.Length);
        foreach (Transform point in reinforcementSpawnPoints)
            if (point != null) availablePoints.Add(point);
        if (availablePoints.Count == 0) return;

        for (int i = 0; i < reinforcementCount; i++)
        {
            GameObject prefab = GetRandomReinforcementPrefab();
            if (prefab == null) continue;
            if (availablePoints.Count == 0)
                foreach (Transform point in reinforcementSpawnPoints)
                    if (point != null) availablePoints.Add(point);

            int pointIndex = Random.Range(0, availablePoints.Count);
            Transform spawnPoint = availablePoints[pointIndex];
            availablePoints.RemoveAt(pointIndex);
            GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, spawnedBossParent);
            instance.name = $"{prefab.name}_BossReinforcement";
            EnemyAgent enemy = instance.GetComponent<EnemyAgent>();
            if (enemy != null) spawnedReinforcements.Add(enemy);
        }
    }

    private GameObject GetRandomReinforcementPrefab()
    {
        int startIndex = Random.Range(0, reinforcementPrefabs.Length);
        for (int offset = 0; offset < reinforcementPrefabs.Length; offset++)
        {
            GameObject prefab = reinforcementPrefabs[(startIndex + offset) % reinforcementPrefabs.Length];
            if (prefab != null) return prefab;
        }
        return null;
    }

    private void KillAllBossArenaMinions()
    {
        foreach (EnemyAgent enemy in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
        {
            if (enemy == null || enemy == activeBoss || enemy.IsDead) continue;
            if (arenaCombatZone == null || arenaCombatZone.ContainsPosition(enemy.transform.position)) enemy.Die();
        }
    }

    private void StopReinforcementLoop()
    {
        if (reinforcementRoutine != null) StopCoroutine(reinforcementRoutine);
        reinforcementRoutine = null;
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
            PlayBossDeferredDeathAnimation();
            SetPlayerEpilogueIdle(false);
            gameStateUi?.ShowVictory();
            epilogueRoutine = null;
            yield break;
        }

        dialogueSystem.DialogueFinished += ShowVictoryAfterEpilogue;
        dialogueSystem.DialogueLineCompleted += HandlePostBossDialogueLineCompleted;
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
        dialogueSystem.DialogueLineCompleted -= HandlePostBossDialogueLineCompleted;
        PlayBossDeferredDeathAnimation();
        SetPlayerEpilogueIdle(false);
        Debug.LogError("Boss epilogue dialogue could not start; showing victory as a fallback.", this);
        gameStateUi?.ShowVictory();
    }

    private void ShowVictoryAfterEpilogue()
    {
        dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
        dialogueSystem.DialogueLineCompleted -= HandlePostBossDialogueLineCompleted;
        PlayBossDeferredDeathAnimation();
        SetPlayerEpilogueIdle(false);
        gameStateUi?.ShowVictory();
    }

    private void HandlePostBossDialogueLineCompleted(int lineIndex, string speakerName, string completionCue)
    {
        if (!string.Equals(completionCue, BossDeathDialogueCue, System.StringComparison.OrdinalIgnoreCase)) return;
        dialogueSystem.DialogueLineCompleted -= HandlePostBossDialogueLineCompleted;
        PlayBossDeferredDeathAnimation();
    }

    private void PlayBossDeferredDeathAnimation()
    {
        activeBoss?.PlayDeferredDeathAnimation();
    }

    private void SetPlayerEpilogueIdle(bool active)
    {
        PlayerCharacterController player = playerSpeaker != null
            ? playerSpeaker.GetComponentInParent<PlayerCharacterController>()
            : null;
        player ??= FindAnyObjectByType<PlayerCharacterController>();
        player?.SetPresentationIdle(active);
    }

    private void ResetEncounterForRetry()
    {
        SetPlayerEpilogueIdle(false);
        StopReinforcementLoop();
        if (epilogueRoutine != null) StopCoroutine(epilogueRoutine);
        epilogueRoutine = null;
        if (dialogueSystem != null)
        {
            dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
            dialogueSystem.DialogueLineCompleted -= HandlePostBossDialogueLineCompleted;
        }

        if (activeBoss != null)
        {
            activeBoss.Died -= HandleBossDied;
            Destroy(activeBoss.gameObject);
        }
        activeBoss = null;
        activeBossCombat = null;
        bossPresentationIdleActive = false;

        foreach (EnemyAgent guardArcher in guardArchers)
            if (guardArcher != null) Destroy(guardArcher.gameObject);
        guardArchers.Clear();

        foreach (EnemyAgent reinforcement in spawnedReinforcements)
            if (reinforcement != null) Destroy(reinforcement.gameObject);
        spawnedReinforcements.Clear();

        bossSpawned = false;
        bossDefeated = false;
        arenaCleared = false;
        epilogueStarted = false;
        if (bossHudRoot != null) bossHudRoot.SetActive(false);
    }

    private void OnDisable()
    {
        SetPlayerEpilogueIdle(false);
        StopReinforcementLoop();
        if (activeBoss != null) activeBoss.Died -= HandleBossDied;
        if (dialogueSystem != null)
        {
            dialogueSystem.DialogueFinished -= ShowVictoryAfterEpilogue;
            dialogueSystem.DialogueLineCompleted -= HandlePostBossDialogueLineCompleted;
        }
    }

    private void OnDestroy()
    {
        if (arenaCombatZone != null) arenaCombatZone.ZoneReset -= ResetEncounterForRetry;
        if (bossWaveSpawner != null)
            bossWaveSpawner.AllWavesClearedEvent.RemoveListener(HandleInitialWavesCleared);
    }
}
