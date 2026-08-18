using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First-use, scene-authored lesson placed before Arena 01. A melee instructor creates
/// the perfect-dodge opportunity; two passive targets make the player perform a three-hit
/// kill chain before the exit opens.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class PerfectDodgeTutorialController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private RespawnPointManager respawnPointManager;
    [SerializeField] private ArenaBoundaryGate exitGate;
    [SerializeField] private Transform spawnedEnemyParent;
    [SerializeField] private Transform attackerSpawn;
    [SerializeField] private Transform[] followUpSpawns;

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject attackerPrefab;
    [SerializeField] private GameObject followUpPrefab;
    [SerializeField, Min(2)] private int requiredChainKills = 3;

    [Header("Authored UI")]
    [SerializeField] private GameObject tutorialUiRoot;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text progressText;
    [SerializeField, Min(0f)] private float completionDisplayDuration = 2.2f;
    [SerializeField, Min(0f)] private float retryDelay = 1f;

    private readonly List<EnemyAgent> spawnedEnemies = new();
    private EnemyAgent attacker;
    private bool tutorialActive;
    private bool completed;
    private bool restarting;

    private void Awake()
    {
        player ??= FindAnyObjectByType<PlayerCharacterController>(FindObjectsInactive.Include);
        respawnPointManager ??= FindAnyObjectByType<RespawnPointManager>(FindObjectsInactive.Include);
        exitGate?.SetLocked(true);
        tutorialUiRoot?.SetActive(false);
    }

    private void OnEnable()
    {
        BindEvents(true);
    }

    private void OnDisable()
    {
        BindEvents(false);
        player?.SetKillChainTutorialHold(false);
    }

    private void BindEvents(bool bind)
    {
        if (player != null)
        {
            if (bind)
            {
                player.KillChainStarted += HandleKillChainStarted;
                player.KillChainKillConfirmed += HandleKillConfirmed;
                player.KillChainFinished += HandleKillChainFinished;
            }
            else
            {
                player.KillChainStarted -= HandleKillChainStarted;
                player.KillChainKillConfirmed -= HandleKillConfirmed;
                player.KillChainFinished -= HandleKillChainFinished;
            }
        }

        if (respawnPointManager == null) return;
        if (bind) respawnPointManager.PlayerRespawned += HandlePlayerRespawned;
        else respawnPointManager.PlayerRespawned -= HandlePlayerRespawned;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (completed || tutorialActive || other.GetComponentInParent<PlayerCharacterController>() != player) return;
        BeginTutorial();
    }

    private void BeginTutorial()
    {
        tutorialActive = true;
        restarting = false;
        exitGate?.SetLocked(true);
        tutorialUiRoot?.SetActive(true);
        SetPrompt("在敌人攻击命中的瞬间按 空格 完美闪避", "等待完美闪避");
        SpawnAttempt();
    }

    private void SpawnAttempt()
    {
        ClearSpawnedEnemies();
        if (attackerPrefab == null || attackerSpawn == null)
        {
            Debug.LogError($"{name}: tutorial attacker prefab or spawn marker is missing.", this);
            return;
        }

        attacker = SpawnEnemy(attackerPrefab, attackerSpawn, enabledForCombat: true);
        if (attacker != null) attacker.Died += HandleAttackerDied;

        if (followUpPrefab == null || followUpSpawns == null) return;
        foreach (Transform spawn in followUpSpawns)
        {
            if (spawn == null) continue;
            EnemyAgent target = SpawnEnemy(followUpPrefab, spawn, enabledForCombat: false);
            if (target != null) target.gameObject.SetActive(false);
        }
    }

    private EnemyAgent SpawnEnemy(GameObject prefab, Transform marker, bool enabledForCombat)
    {
        GameObject instance = Instantiate(prefab, marker.position, marker.rotation, spawnedEnemyParent);
        instance.name = enabledForCombat ? "Enemy_TutorialAttacker" : "Enemy_TutorialDashTarget";
        EnemyAgent agent = instance.GetComponent<EnemyAgent>();
        if (agent == null)
        {
            Debug.LogError($"{prefab.name} needs an EnemyAgent for the perfect-dodge tutorial.", instance);
            Destroy(instance);
            return null;
        }

        agent.enabled = enabledForCombat;
        spawnedEnemies.Add(agent);
        return agent;
    }

    private void HandleKillChainStarted()
    {
        if (!tutorialActive || completed || restarting) return;
        player.SetKillChainTutorialHold(true);
        foreach (EnemyAgent enemy in spawnedEnemies)
        {
            if (enemy == null || enemy == attacker) continue;
            enemy.gameObject.SetActive(true);
            enemy.enabled = false;
            if (enemy.Body != null) enemy.Body.simulated = true;
        }
        SetPrompt("完美闪避！点击高亮敌人连击，每次击杀都会回血", $"连续冲刺 0 / {requiredChainKills}");
    }

    private void HandleKillConfirmed(int chainKills)
    {
        if (!tutorialActive || completed || restarting) return;
        int shownKills = Mathf.Min(chainKills, requiredChainKills);
        SetPrompt("时间结束前继续连击：击杀敌人可回复生命", $"连续冲刺 {shownKills} / {requiredChainKills}");
        if (chainKills >= requiredChainKills) CompleteTutorial();
    }

    private void HandleKillChainFinished(int chainKills)
    {
        if (!tutorialActive || completed || restarting || chainKills >= requiredChainKills) return;
        StartCoroutine(RestartAttempt());
    }

    private void HandleAttackerDied(EnemyAgent _)
    {
        if (!tutorialActive || completed || restarting || player.IsKillChainActive) return;
        StartCoroutine(RestartAttempt());
    }

    private IEnumerator RestartAttempt()
    {
        restarting = true;
        player?.SetKillChainTutorialHold(false);
        ClearSpawnedEnemies();
        SetPrompt("再试一次：看准攻击命中的瞬间按 空格", "等待完美闪避");
        if (retryDelay > 0f) yield return new WaitForSeconds(retryDelay);
        if (!tutorialActive || completed) yield break;
        restarting = false;
        SpawnAttempt();
    }

    private void CompleteTutorial()
    {
        completed = true;
        tutorialActive = false;
        player?.SetKillChainTutorialHold(false);
        exitGate?.SetLocked(false);
        SetPrompt("很好！完美闪避后的连击可以回血，残血时注意利用", $"连续冲刺 {requiredChainKills} / {requiredChainKills}  完成");
        StartCoroutine(HideCompletionPrompt());
    }

    private IEnumerator HideCompletionPrompt()
    {
        if (completionDisplayDuration > 0f) yield return new WaitForSecondsRealtime(completionDisplayDuration);
        tutorialUiRoot?.SetActive(false);
        GetComponent<Collider2D>().enabled = false;
    }

    private void HandlePlayerRespawned()
    {
        player?.SetKillChainTutorialHold(false);
        if (completed)
        {
            exitGate?.SetLocked(false);
            return;
        }

        StopAllCoroutines();
        tutorialActive = false;
        restarting = false;
        ClearSpawnedEnemies();
        exitGate?.SetLocked(true);
        BeginTutorial();
    }

    private void ClearSpawnedEnemies()
    {
        if (attacker != null) attacker.Died -= HandleAttackerDied;
        attacker = null;
        foreach (EnemyAgent enemy in spawnedEnemies)
            if (enemy != null) Destroy(enemy.gameObject);
        spawnedEnemies.Clear();
    }

    private void SetPrompt(string instruction, string progress)
    {
        if (instructionText != null) instructionText.text = instruction;
        if (progressText != null) progressText.text = progress;
    }
}
