using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Authors the seamless 《天下第一》 main menu and prologue directly into Level_LD.</summary>
public static class LevelLdPrologueSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level_LD.unity";
    private const string TimelineFolder = "Assets/Timeline";
    private const string TimelinePath = TimelineFolder + "/Level01_OpeningSequence.playable";
    private const string WalkClipPath = TimelineFolder + "/Level01_HeroWalkIn.anim";
    private const string BossEntranceTimelinePath = TimelineFolder + "/BossEntrance.playable";
    private const string BossEntranceCameraClipPath = TimelineFolder + "/BossEntrance_CameraPan.anim";
    private const string OpeningVideoPath = "Assets/Resources/Video/开场动画.mp4";
    private const string OpeningVideoTexturePath = "Assets/Resources/Video/OpeningVideoRenderTexture.renderTexture";
    private const string DialoguePath = "Assets/Resources/Dialogue/Story/Level01_Opening.csv";
    private const string BossDialoguePath = "Assets/Resources/Dialogue/Story/Level01_BossClear.csv";
    private const string HealthUiFontPath = "Assets/Fonts/NotoSerifCJKsc-Regular.otf";
    private const string BossPrefabPath = "Assets/Prefabs/Enemy/Boss.prefab";
    private const string PlayerPrefabGuid = "434d88b041492e74fa49bf21dc3af3e9";
    private const string SwordsmanPrefabGuid = "7513f6c33541e8440a486e9d12ff20c5";
    private const float WalkDuration = 3f;

    private static readonly (string PageName, string ArtworkPath, string ImageName)[] TutorialArtwork =
    {
        ("Page_01_Movement", "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page01_MovementAim.png", "Img_TutorialArtwork_01"),
        ("Page_02_Attack", "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page02_NormalAttack.png", "Img_TutorialArtwork_02"),
        ("Page_03_Dodge", "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page03_DodgePerfectDodge.png", "Img_TutorialArtwork_03"),
        ("Page_04_KillChain", "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page04_KillChain.png", "Img_TutorialArtwork_04"),
        ("Page_05_Ultimate", "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page05_Ultimate.png", "Img_TutorialArtwork_05")
    };

    private static readonly Vector3 HeroStart = new Vector3(-119.4f, -19.6f, 0f);
    private static readonly Vector3 HeroEnd = new Vector3(-113.8f, -19.6f, 0f);
    private static readonly Vector3 BanditPosition = new Vector3(-108.6f, -19.6f, 0f);

    [MenuItem("NewMini/Story/Rebuild Level_LD Seamless Prologue")]
    public static void Rebuild()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureEventSystem(scene);

        DestroySceneObject(scene, "Root_序章演出");
        DestroySceneObject(scene, "Root_MainMenu");
        DestroySceneObject(scene, "Root_OpeningVideo");

        PlayerCharacterController player = FindInScene<PlayerCharacterController>(scene);
        if (player == null)
        {
            string playerPath = AssetDatabase.GUIDToAssetPath(PlayerPrefabGuid);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            GameObject playerObject = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            playerObject.name = "Player";
            playerObject.transform.position = HeroEnd;
            player = playerObject.GetComponent<PlayerCharacterController>();
        }

        GameObject sequenceRoot = new GameObject("Root_序章演出", typeof(PlayableDirector), typeof(SceneFlowController), typeof(LevelPrologueController));
        SceneManager.MoveGameObjectToScene(sequenceRoot, scene);

        Transform markers = NewWorld("Group_OpeningMarkers", sequenceRoot.transform).transform;
        Transform startMarker = NewWorld("Marker_HeroIntroStart", markers).transform;
        Transform endMarker = NewWorld("Marker_HeroIntroEnd", markers).transform;
        Transform banditMarker = NewWorld("Marker_OpeningBandit", markers).transform;
        startMarker.position = HeroStart;
        endMarker.position = HeroEnd;
        banditMarker.position = BanditPosition;

        GameObject actorGroup = NewWorld("Group_OpeningActors", sequenceRoot.transform);
        string swordsmanPath = AssetDatabase.GUIDToAssetPath(SwordsmanPrefabGuid);
        GameObject swordsmanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordsmanPath);
        GameObject banditObject = (GameObject)PrefabUtility.InstantiatePrefab(swordsmanPrefab, actorGroup.transform);
        banditObject.name = "Enemy_OpeningBandit";
        banditObject.transform.position = BanditPosition;
        EnemyAgent openingBandit = banditObject.GetComponent<EnemyAgent>();
        SpriteRenderer banditRenderer = banditObject.GetComponentInChildren<SpriteRenderer>(true);
        if (banditRenderer != null) banditRenderer.flipX = true;
        banditObject.SetActive(false);

        Animator timelineAnimator = player.gameObject.GetComponent<Animator>();
        if (timelineAnimator == null) timelineAnimator = player.gameObject.AddComponent<Animator>();
        timelineAnimator.applyRootMotion = false;

        PlayableDirector director = sequenceRoot.GetComponent<PlayableDirector>();
        TimelineAsset timeline = BuildTimeline(HeroStart, HeroEnd, timelineAnimator, director);
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;

        GameObject menuRoot = BuildMainMenu(scene, out CanvasGroup menuCanvasGroup, out Button startButton, out Button quitButton);
        OpeningVideoController openingVideo = BuildOpeningVideo(scene);
        LevelPrologueController prologue = sequenceRoot.GetComponent<LevelPrologueController>();
        UnityEventTools.AddPersistentListener(startButton.onClick, prologue.StartGame);
        UnityEventTools.AddPersistentListener(quitButton.onClick, prologue.QuitGame);

        GameObject hudRoot = FindGameObject(scene, "Root_角色战斗HUD");
        ClickDialogueSystem dialogue = FindInScene<ClickDialogueSystem>(scene);
        GameStateUIController gameState = FindInScene<GameStateUIController>(scene);
        TextAsset openingCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(DialoguePath);

        if (dialogue != null)
        {
            SerializedObject dialogueSerialized = new SerializedObject(dialogue);
            dialogueSerialized.FindProperty("dialogueCsv").objectReferenceValue = openingCsv;
            dialogueSerialized.FindProperty("character").objectReferenceValue = player.transform;
            dialogueSerialized.FindProperty("soldier").objectReferenceValue = openingBandit.transform;
            dialogueSerialized.FindProperty("soldierObjectName").stringValue = banditObject.name;
            dialogueSerialized.FindProperty("playFirstLineOnStart").boolValue = false;
            dialogueSerialized.FindProperty("gameplayHudRoot").objectReferenceValue = hudRoot;
            dialogueSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        SceneFlowController sceneFlow = sequenceRoot.GetComponent<SceneFlowController>();
        if (gameState != null)
        {
            SerializedObject gameStateSerialized = new SerializedObject(gameState);
            gameStateSerialized.FindProperty("player").objectReferenceValue = player;
            gameStateSerialized.FindProperty("gameplayHudRoot").objectReferenceValue = hudRoot;
            gameStateSerialized.FindProperty("mainMenuSceneName").stringValue = "Level_LD";
            gameStateSerialized.FindProperty("sceneFlow").objectReferenceValue = sceneFlow;
            gameStateSerialized.ApplyModifiedPropertiesWithoutUndo();
            RenameHelpBackLabel(gameState);
        }

        SerializedObject prologueSerialized = new SerializedObject(prologue);
        prologueSerialized.FindProperty("mainMenuRoot").objectReferenceValue = menuRoot;
        prologueSerialized.FindProperty("mainMenuCanvasGroup").objectReferenceValue = menuCanvasGroup;
        prologueSerialized.FindProperty("startButton").objectReferenceValue = startButton;
        prologueSerialized.FindProperty("openingVideo").objectReferenceValue = openingVideo;
        prologueSerialized.FindProperty("player").objectReferenceValue = player;
        prologueSerialized.FindProperty("openingBandit").objectReferenceValue = openingBandit;
        prologueSerialized.FindProperty("heroIntroStart").objectReferenceValue = startMarker;
        prologueSerialized.FindProperty("heroIntroEnd").objectReferenceValue = endMarker;
        prologueSerialized.FindProperty("openingBanditEnd").objectReferenceValue = banditMarker;
        prologueSerialized.FindProperty("gameplayHudRoot").objectReferenceValue = hudRoot;
        prologueSerialized.FindProperty("openingDialogue").objectReferenceValue = dialogue;
        prologueSerialized.FindProperty("gameStateUi").objectReferenceValue = gameState;
        prologueSerialized.FindProperty("openingDirector").objectReferenceValue = director;
        prologueSerialized.ApplyModifiedPropertiesWithoutUndo();

        player.transform.position = HeroEnd;
        player.gameObject.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(false);

        BuildBossEncounter(scene, player, dialogue);
        AssignTutorialArtwork(scene);
        EnsureDialogueContinueHint(scene);
        AssignHealthUiFont(scene);

        ConfigureBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Level_LD seamless 《天下第一》 prologue rebuilt and saved.");
    }

    [MenuItem("NewMini/Story/Validate Level_LD Story Integration")]
    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject menu = FindGameObject(scene, "Root_MainMenu");
        GameObject titleObject = FindGameObject(scene, "Txt_GameTitle");
        Button start = FindGameObject(scene, "Btn_StartGame")?.GetComponent<Button>();
        LevelPrologueController prologue = FindInScene<LevelPrologueController>(scene);
        PlayableDirector director = FindInScene<PlayableDirector>(scene);
        PlayerCharacterController player = FindInScene<PlayerCharacterController>(scene);
        GameObject hud = FindGameObject(scene, "Root_角色战斗HUD");

        Require(menu != null && menu.activeSelf, "Root_MainMenu must be authored and active.");
        Require(titleObject != null && titleObject.GetComponent<Text>()?.text == "天下第一", "Game title must be 《天下第一》.");
        Require(start != null && start.onClick.GetPersistentEventCount() > 0, "Btn_StartGame needs a persistent StartGame binding.");
        Require(prologue != null, "LevelPrologueController is missing.");
        OpeningVideoController openingVideo = FindInScene<OpeningVideoController>(scene);
        Require(openingVideo != null, "OpeningVideoController is missing.");
        Require(director != null && director.playableAsset != null, "Opening PlayableDirector or Timeline is missing.");
        Require(player != null && !player.gameObject.activeSelf, "Player must begin inactive behind the seamless menu.");
        Require(hud != null && !hud.activeSelf, "Gameplay HUD must begin inactive behind the seamless menu.");

        GameObject arena05 = FindGameObject(scene, "Root_Arena_05");
        LevelBossEncounterController bossEncounter = arena05 != null
            ? arena05.GetComponentInChildren<LevelBossEncounterController>(true)
            : null;
        ArenaWaveSpawner finalWaves = arena05 != null ? arena05.GetComponentInChildren<ArenaWaveSpawner>(true) : null;
        Require(bossEncounter != null, "Arena 05 boss encounter is missing.");
        Require(finalWaves != null, "Arena 05 final wave spawner is missing.");
        bool hasBossSpawnBinding = false;
        for (int index = 0; index < finalWaves.AllWavesClearedEvent.GetPersistentEventCount(); index++)
        {
            hasBossSpawnBinding |= finalWaves.AllWavesClearedEvent.GetPersistentTarget(index) == bossEncounter
                && finalWaves.AllWavesClearedEvent.GetPersistentMethodName(index) == nameof(LevelBossEncounterController.SpawnBoss);
        }
        Require(hasBossSpawnBinding, "Arena 05 needs a persistent SpawnBoss binding after its last regular wave.");

        SerializedObject bossSerialized = new SerializedObject(bossEncounter);
        Require(bossSerialized.FindProperty("bossPrefab").objectReferenceValue != null, "Boss prefab reference is missing.");
        Require(bossSerialized.FindProperty("postBossDialogue").objectReferenceValue != null, "Post-boss dialogue reference is missing.");
        Require(bossSerialized.FindProperty("bossHudRoot").objectReferenceValue != null, "Borrowed-life HUD reference is missing.");

        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        Require(bossPrefab != null && bossPrefab.GetComponent<EnemyAgent>() != null, "Boss prefab needs EnemyAgent.");
        Require(bossPrefab.GetComponent<BorrowedLifeBossController>() != null, "Boss prefab needs 99 borrowed lives.");
        Require(EditorBuildSettings.scenes.Length > 0 && EditorBuildSettings.scenes[0].path == ScenePath,
            "Level_LD must be the first build scene.");
        ValidateTutorialArtwork(scene);
        ValidateDialogueContinueHint(scene);
        ValidateHealthUiFont(scene);

        Debug.Log("Validated Level_LD: 《天下第一》 menu, prologue, dialogue continue hint, health UI font, five tutorial artworks, first-kill help, 99-contract boss, and epilogue references are complete.");
    }

    private static TimelineAsset BuildTimeline(Vector3 start, Vector3 end, Animator binding, PlayableDirector director)
    {
        EnsureFolder(TimelineFolder);
        AssetDatabase.DeleteAsset(TimelinePath);
        AssetDatabase.DeleteAsset(WalkClipPath);

        AnimationClip walk = new AnimationClip { name = "Level01_HeroWalkIn", frameRate = 60f };
        walk.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", SmoothCurve(start.x, end.x));
        walk.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.y", ConstantCurve(start.y));
        walk.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.z", ConstantCurve(start.z));
        AssetDatabase.CreateAsset(walk, WalkClipPath);

        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = "Level01_OpeningSequence";
        AssetDatabase.CreateAsset(timeline, TimelinePath);
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, "Track_HeroWalkIn");
        track.trackOffset = TrackOffset.ApplyTransformOffsets;
        TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
        clip.displayName = "Clip_HeroWalkIn";
        clip.duration = WalkDuration;
        AnimationPlayableAsset playable = (AnimationPlayableAsset)clip.asset;
        playable.clip = walk;
        playable.applyFootIK = false;
        director.playableAsset = timeline;
        director.SetGenericBinding(track, binding);
        EditorUtility.SetDirty(timeline);
        return timeline;
    }

    private static AnimationCurve SmoothCurve(float from, float to)
    {
        AnimationCurve curve = AnimationCurve.EaseInOut(0f, from, WalkDuration, to);
        return curve;
    }

    private static AnimationCurve ConstantCurve(float value)
    {
        return new AnimationCurve(new Keyframe(0f, value), new Keyframe(WalkDuration, value));
    }

    private static GameObject BuildMainMenu(Scene scene, out CanvasGroup canvasGroup, out Button start, out Button quit)
    {
        GameObject root = NewUi("Root_MainMenu", null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(root, scene);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        canvasGroup = root.GetComponent<CanvasGroup>();

        Image veil = ImageNode("Layer_MapVeil", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.015f, .018f, .02f, .18f));
        veil.raycastTarget = false;

        GameObject panel = NewUi("Panel_TitleAndActions", root.transform, typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(.44f, 1f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(.015f, .018f, .02f, .84f);
        panelImage.raycastTarget = true;

        TextNode("Txt_WorldMark", panel.transform, new Vector2(94f, -104f), new Vector2(620f, 48f), "九九蚀命 · 雪竹驿", 25, new Color(.72f, .62f, .42f, 1f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
        Text title = TextNode("Txt_GameTitle", panel.transform, new Vector2(92f, -242f), new Vector2(650f, 150f), "天下第一", 102, new Color(.95f, .92f, .83f, 1f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
        title.fontStyle = FontStyle.Bold;
        Image accent = ImageNode("Img_VermilionStroke", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(94f, -335f), new Vector2(420f, 7f), new Color(.62f, .055f, .04f, 1f));
        accent.raycastTarget = false;
        TextNode("Txt_Premise", panel.transform, new Vector2(94f, -425f), new Vector2(630f, 120f), "当天下人只剩一条命，\n你却拥有九十九次犯错机会。", 30, new Color(.82f, .82f, .78f, 1f), TextAnchor.UpperLeft, new Vector2(0f, 1f));

        start = ButtonNode("Btn_StartGame", panel.transform, new Vector2(94f, -625f), "入 江 湖", new Vector2(390f, 82f));
        quit = ButtonNode("Btn_QuitGame", panel.transform, new Vector2(94f, -730f), "退出游戏", new Vector2(390f, 72f));
        TextNode("Txt_ControlHint", panel.transform, new Vector2(94f, 72f), new Vector2(620f, 42f), "WASD 移动  ·  鼠标挥剑  ·  Space 闪避  ·  R 终极", 20, new Color(.62f, .64f, .62f, 1f), TextAnchor.MiddleLeft, new Vector2(0f, 0f));
        TextNode("Txt_Chapter", root.transform, new Vector2(-64f, 58f), new Vector2(620f, 52f), "第一回  ·  一命江湖", 25, new Color(.92f, .86f, .7f, .9f), TextAnchor.MiddleRight, new Vector2(1f, 0f));
        return root;
    }

    private static TimelineAsset BuildBossEntranceTimeline(Camera camera, Vector3 bossPosition, PlayableDirector director)
    {
        EnsureFolder(TimelineFolder);
        AssetDatabase.DeleteAsset(BossEntranceTimelinePath);
        AssetDatabase.DeleteAsset(BossEntranceCameraClipPath);

        Vector3 from = camera.transform.localPosition;
        // Frame the boss in the upper third rather than exactly at screen centre.
        Vector3 to = new Vector3(bossPosition.x, bossPosition.y - camera.orthographicSize * .35f, from.z);
        AnimationClip pan = new AnimationClip { name = "BossEntrance_CameraPan", frameRate = 60f };
        pan.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", SmoothCurve(from.x, to.x));
        pan.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.y", SmoothCurve(from.y, to.y));
        pan.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.z", ConstantCurve(from.z));
        AssetDatabase.CreateAsset(pan, BossEntranceCameraClipPath);

        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = "BossEntrance";
        AssetDatabase.CreateAsset(timeline, BossEntranceTimelinePath);
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, "Track_BossEntranceCameraPan");
        TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
        clip.displayName = "Clip_CameraPanToBoss";
        clip.duration = WalkDuration;
        ((AnimationPlayableAsset)clip.asset).clip = pan;
        director.playableAsset = timeline;
        director.SetGenericBinding(track, camera.transform);
        EditorUtility.SetDirty(timeline);
        return timeline;
    }

    private static OpeningVideoController BuildOpeningVideo(Scene scene)
    {
        VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(OpeningVideoPath);
        if (clip == null) throw new System.InvalidOperationException($"Opening video is missing at {OpeningVideoPath}.");

        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(OpeningVideoTexturePath);
        if (texture == null)
        {
            texture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "OpeningVideoRenderTexture"
            };
            AssetDatabase.CreateAsset(texture, OpeningVideoTexturePath);
        }

        GameObject root = NewUi("Root_OpeningVideo", null, typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(VideoPlayer), typeof(OpeningVideoController));
        SceneManager.MoveGameObjectToScene(root, scene);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        Image background = ImageNode("Layer_OpeningVideoBackdrop", root.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, Color.black);
        background.raycastTarget = true;
        GameObject imageObject = NewUi("Img_OpeningVideo", root.transform, typeof(CanvasRenderer), typeof(RawImage));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;

        VideoPlayer player = root.GetComponent<VideoPlayer>();
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.playOnAwake = false;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;

        OpeningVideoController controller = root.GetComponent<OpeningVideoController>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("videoRoot").objectReferenceValue = root;
        serialized.FindProperty("videoCanvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        serialized.FindProperty("videoImage").objectReferenceValue = image;
        serialized.FindProperty("videoPlayer").objectReferenceValue = player;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return controller;
    }

    private static void BuildBossEncounter(Scene scene, PlayerCharacterController player, ClickDialogueSystem dialogue)
    {
        GameObject arena05 = FindGameObject(scene, "Root_Arena_05");
        if (arena05 == null) throw new System.InvalidOperationException("Level_LD needs Root_Arena_05 for the first boss encounter.");

        GameObject previous = FindGameObject(scene, "Root_BossEncounter");
        if (previous != null) Object.DestroyImmediate(previous);

        ArenaWaveSpawner finalWaves = arena05.GetComponentInChildren<ArenaWaveSpawner>(true);
        ArenaCombatZone combatZone = arena05.GetComponentInChildren<ArenaCombatZone>(true);
        RespawnPoint bossCheckpoint = arena05.GetComponentInChildren<RespawnPoint>(true);
        if (finalWaves == null || combatZone == null || bossCheckpoint == null)
            throw new System.InvalidOperationException("Root_Arena_05 needs its authored wave spawner, combat zone, and respawn point.");

        // Checkpoint death restarts the complete Boss combat state. BossPreludeController
        // skips the already-seen introduction on retry and begins the reset waves directly.
        SerializedObject serializedCombatZone = new SerializedObject(combatZone);
        serializedCombatZone.FindProperty("resetOnCheckpointRetry").boolValue = true;
        serializedCombatZone.ApplyModifiedPropertiesWithoutUndo();

        GameObject root = NewWorld("Root_BossEncounter", arena05.transform);
        LevelBossEncounterController encounter = root.AddComponent<LevelBossEncounterController>();
        BossPreludeController prelude = root.AddComponent<BossPreludeController>();
        PlayableDirector bossEntranceDirector = root.AddComponent<PlayableDirector>();

        GameObject actors = NewWorld("Group_BossActors", root.transform);
        Transform spawn = NewWorld("Marker_BossSpawn", root.transform).transform;
        Transform leftGuard = NewWorld("Marker_BossGuardArcher_Left", root.transform).transform;
        Transform rightGuard = NewWorld("Marker_BossGuardArcher_Right", root.transform).transform;
        Transform speaker = NewWorld("Marker_BossDialogue", root.transform).transform;
        Vector3 encounterCenter = combatZone.ZoneCollider != null
            ? combatZone.ZoneCollider.bounds.center
            : arena05.transform.position;
        encounterCenter.z = 0f;
        // Stage the opening encounter high in the arena, with two stationary archer guards.
        spawn.position = encounterCenter + Vector3.up * 2.5f;
        leftGuard.position = spawn.position + Vector3.left * 1.2f;
        rightGuard.position = spawn.position + Vector3.right * 1.2f;
        speaker.position = encounterCenter;
        Camera entranceCamera = Camera.main != null ? Camera.main : FindInScene<Camera>(scene);
        if (entranceCamera != null) BuildBossEntranceTimeline(entranceCamera, spawn.position, bossEntranceDirector);

        GameObject bossPrefab = BuildBossPrefab(scene);
        GameObject bossHud = BuildBossHud(scene, out Text contractCountText);
        TextAsset bossDialogue = AssetDatabase.LoadAssetAtPath<TextAsset>(BossDialoguePath);

        SerializedObject serialized = new SerializedObject(encounter);
        serialized.FindProperty("bossPrefab").objectReferenceValue = bossPrefab;
        serialized.FindProperty("bossSpawnPoint").objectReferenceValue = spawn;
        serialized.FindProperty("spawnedBossParent").objectReferenceValue = actors.transform;
        serialized.FindProperty("guardArcherPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("ac76861895f1dac48981520d119d1d0e"));
        SerializedProperty guards = serialized.FindProperty("guardArcherSpawnPoints");
        guards.arraySize = 2;
        guards.GetArrayElementAtIndex(0).objectReferenceValue = leftGuard;
        guards.GetArrayElementAtIndex(1).objectReferenceValue = rightGuard;
        serialized.FindProperty("bossWaveSpawner").objectReferenceValue = finalWaves;
        SerializedProperty reinforcementPrefabs = serialized.FindProperty("reinforcementPrefabs");
        string[] reinforcementPrefabGuids =
        {
            "7513f6c33541e8440a486e9d12ff20c5",
            "ac76861895f1dac48981520d119d1d0e",
            "d8ffcc6e514f86047ac9f88b26e9d13b",
            "94645a7045925d448ba94f8f187219fb"
        };
        reinforcementPrefabs.arraySize = reinforcementPrefabGuids.Length;
        for (int i = 0; i < reinforcementPrefabGuids.Length; i++)
            reinforcementPrefabs.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(reinforcementPrefabGuids[i]));

        Transform[] reinforcementPoints = finalWaves.GetComponentsInChildren<Transform>(true)
            .Where(point => point != finalWaves.transform && point.name.StartsWith("Spawn_"))
            .ToArray();
        SerializedProperty reinforcementSpawnPoints = serialized.FindProperty("reinforcementSpawnPoints");
        reinforcementSpawnPoints.arraySize = reinforcementPoints.Length;
        for (int i = 0; i < reinforcementPoints.Length; i++)
            reinforcementSpawnPoints.GetArrayElementAtIndex(i).objectReferenceValue = reinforcementPoints[i];
        serialized.FindProperty("reinforcementCount").intValue = 5;
        serialized.FindProperty("reinforcementSpawnDelay").floatValue = 10f;
        serialized.FindProperty("shieldReinforcementChance").floatValue = .1f;
        serialized.FindProperty("arenaCombatZone").objectReferenceValue = combatZone;
        serialized.FindProperty("bossCheckpoint").objectReferenceValue = bossCheckpoint;
        serialized.FindProperty("bossHudRoot").objectReferenceValue = bossHud;
        serialized.FindProperty("contractCountText").objectReferenceValue = contractCountText;
        serialized.FindProperty("dialogueSystem").objectReferenceValue = dialogue;
        serialized.FindProperty("postBossDialogue").objectReferenceValue = bossDialogue;
        serialized.FindProperty("playerSpeaker").objectReferenceValue = player != null ? player.transform : null;
        serialized.FindProperty("npcSpeakerAnchor").objectReferenceValue = speaker;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject zoneSerialized = new SerializedObject(combatZone);
        zoneSerialized.FindProperty("deferWavesUntilRequested").boolValue = true;
        zoneSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject preludeSerialized = new SerializedObject(prelude);
        preludeSerialized.FindProperty("arena").objectReferenceValue = combatZone;
        preludeSerialized.FindProperty("encounter").objectReferenceValue = encounter;
        preludeSerialized.FindProperty("dialogue").objectReferenceValue = dialogue;
        preludeSerialized.FindProperty("bossBeforeDialogue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Dialogue/Story/Level01_BossBefore.csv");
        preludeSerialized.FindProperty("player").objectReferenceValue = player;
        preludeSerialized.FindProperty("incenseDestination").objectReferenceValue = FindGameObject(scene, "03_IncenseBurner")?.transform;
        preludeSerialized.FindProperty("bossEntranceDirector").objectReferenceValue = bossEntranceDirector;
        preludeSerialized.FindProperty("presentationCamera").objectReferenceValue = entranceCamera;
        preludeSerialized.ApplyModifiedPropertiesWithoutUndo();

        for (int index = finalWaves.WaveStartedEvent.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            Object target = finalWaves.WaveStartedEvent.GetPersistentTarget(index);
            if (target == null || target is LevelBossEncounterController)
                UnityEventTools.RemovePersistentListener(finalWaves.WaveStartedEvent, index);
        }
        UnityEventTools.AddPersistentListener(finalWaves.WaveStartedEvent, encounter.SpawnBossWithFirstWave);

        for (int index = finalWaves.AllWavesClearedEvent.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            Object target = finalWaves.AllWavesClearedEvent.GetPersistentTarget(index);
            if (target == null || target is LevelBossEncounterController)
                UnityEventTools.RemovePersistentListener(finalWaves.AllWavesClearedEvent, index);
        }

        for (int index = combatZone.ZoneClearedEvent.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            Object target = combatZone.ZoneClearedEvent.GetPersistentTarget(index);
            if (target == null || target is LevelBossEncounterController)
                UnityEventTools.RemovePersistentListener(combatZone.ZoneClearedEvent, index);
        }
        UnityEventTools.AddPersistentListener(combatZone.ZoneClearedEvent, encounter.NotifyArenaCleared);
        for (int index = combatZone.ZoneLockedEvent.GetPersistentEventCount() - 1; index >= 0; index--)
        {
            Object target = combatZone.ZoneLockedEvent.GetPersistentTarget(index);
            if (target == null || target is BossPreludeController || target == bossCheckpoint)
                UnityEventTools.RemovePersistentListener(combatZone.ZoneLockedEvent, index);
        }
        UnityEventTools.AddPersistentListener(combatZone.ZoneLockedEvent, bossCheckpoint.Activate);
        UnityEventTools.AddPersistentListener(combatZone.ZoneLockedEvent, prelude.BeginPrelude);
    }

    private static GameObject BuildBossPrefab(Scene scene)
    {
        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        if (bossPrefab == null) throw new System.InvalidOperationException($"Boss prefab is missing at '{BossPrefabPath}'.");
        if (bossPrefab.GetComponent<EnemyAgent>() == null) throw new System.InvalidOperationException("Boss prefab needs EnemyAgent.");
        if (bossPrefab.GetComponent<BossCombatController>() == null) throw new System.InvalidOperationException("Boss prefab needs BossCombatController.");
        if (bossPrefab.GetComponent<BorrowedLifeBossController>() == null) throw new System.InvalidOperationException("Boss prefab needs BorrowedLifeBossController.");
        return bossPrefab;
    }

    private static GameObject BuildBossHud(Scene scene, out Text contractCount)
    {
        GameObject root = NewUi("Root_BossHUD", null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, scene);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject panel = NewUi("Panel_BorrowedLifeBoss", root.transform, typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, 1f);
        panelRect.pivot = new Vector2(.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -34f);
        panelRect.sizeDelta = new Vector2(670f, 112f);
        panel.GetComponent<Image>().color = new Color(.025f, .02f, .018f, .88f);

        Text name = TextNode("Txt_BossName", panel.transform, new Vector2(0f, -14f), new Vector2(620f, 46f), "借命阎罗 · 裘九", 30, new Color(.93f, .82f, .64f, 1f), TextAnchor.MiddleCenter, new Vector2(.5f, 1f));
        name.fontStyle = FontStyle.Bold;
        contractCount = TextNode("Txt_BorrowedLifeCount", panel.transform, new Vector2(0f, -61f), new Vector2(620f, 40f), "借命契：99", 27, new Color(.86f, .16f, .11f, 1f), TextAnchor.MiddleCenter, new Vector2(.5f, 1f));
        contractCount.fontStyle = FontStyle.Bold;
        root.SetActive(false);
        return root;
    }

    private static void AssignTutorialArtwork(Scene scene)
    {
        foreach ((string pageName, string artworkPath, string imageName) in TutorialArtwork)
        {
            GameObject page = FindGameObject(scene, pageName);
            if (page == null) throw new System.InvalidOperationException($"Tutorial page {pageName} is missing.");

            Image artworkImage = page.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(value => value.name == "Img_InstructionPlaceholder"
                    || value.name.StartsWith("Img_TutorialArtwork_", System.StringComparison.Ordinal));
            Sprite artworkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(artworkPath);
            if (artworkImage == null || artworkSprite == null)
                throw new System.InvalidOperationException($"Tutorial artwork could not be assigned for {pageName}.");

            artworkImage.gameObject.name = imageName;
            artworkImage.sprite = artworkSprite;
            artworkImage.type = Image.Type.Simple;
            artworkImage.preserveAspect = true;
            artworkImage.color = Color.white;
            artworkImage.raycastTarget = false;

            RectTransform artworkRect = artworkImage.rectTransform;
            artworkRect.anchorMin = artworkRect.anchorMax = new Vector2(.5f, .5f);
            artworkRect.pivot = new Vector2(.5f, .5f);
            artworkRect.anchoredPosition = Vector2.zero;
            artworkRect.sizeDelta = new Vector2(1120f, 630f);
            artworkRect.SetAsFirstSibling();

            Transform placeholder = artworkImage.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == "Txt_Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            Text pageTitle = page.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(value => value.name == "Txt_PageTitle");
            Text pageDescription = page.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(value => value.name == "Txt_PageDescription");
            if (pageTitle != null) pageTitle.rectTransform.anchoredPosition = new Vector2(0f, 275f);
            if (pageDescription != null) pageDescription.rectTransform.anchoredPosition = new Vector2(0f, -250f);
        }
    }

    private static void ValidateTutorialArtwork(Scene scene)
    {
        foreach ((string pageName, string artworkPath, string imageName) in TutorialArtwork)
        {
            GameObject page = FindGameObject(scene, pageName);
            Image image = page != null
                ? page.GetComponentsInChildren<Image>(true).FirstOrDefault(value => value.name == imageName)
                : null;
            Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(artworkPath);
            Require(image != null && image.sprite == expected, $"{pageName} is not using its authored tutorial artwork.");
            Require(image.preserveAspect && !image.raycastTarget, $"{pageName} tutorial artwork presentation is not configured correctly.");
            Transform placeholder = image.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == "Txt_Placeholder");
            Require(placeholder == null || !placeholder.gameObject.activeSelf, $"{pageName} placeholder label must be hidden.");
        }
    }

    private static void EnsureDialogueContinueHint(Scene scene)
    {
        GameObject continueLayer = FindGameObject(scene, "Btn_ContinueDialogue");
        if (continueLayer == null)
            throw new System.InvalidOperationException("Btn_ContinueDialogue is missing from Level_LD.");

        Transform existing = continueLayer.transform.Find("Txt_DialogueContinueHint");
        Text hint = existing != null ? existing.GetComponent<Text>() : null;
        if (hint == null)
        {
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            hint = TextNode(
                "Txt_DialogueContinueHint",
                continueLayer.transform,
                new Vector2(0f, 42f),
                new Vector2(520f, 46f),
                "点击快进 / 继续",
                24,
                new Color(.9f, .84f, .7f, .96f),
                TextAnchor.MiddleCenter,
                new Vector2(.5f, 0f));
        }

        hint.text = "点击快进 / 继续";
        hint.fontSize = 24;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(.9f, .84f, .7f, .96f);
        hint.raycastTarget = false;

        RectTransform rect = hint.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 42f);
        rect.sizeDelta = new Vector2(520f, 46f);

        Outline outline = hint.GetComponent<Outline>();
        if (outline == null) outline = hint.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .82f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
        hint.transform.SetAsLastSibling();
    }

    private static void ValidateDialogueContinueHint(Scene scene)
    {
        GameObject continueLayer = FindGameObject(scene, "Btn_ContinueDialogue");
        GameObject hintObject = FindGameObject(scene, "Txt_DialogueContinueHint");
        Text hint = hintObject != null ? hintObject.GetComponent<Text>() : null;
        Require(continueLayer != null, "Btn_ContinueDialogue is missing.");
        Require(hint != null && hint.text == "点击快进 / 继续", "Dialogue continue hint is missing or has incorrect text.");
        Require(hint.transform.parent == continueLayer.transform, "Dialogue continue hint must be authored under Btn_ContinueDialogue.");
        Require(!hint.raycastTarget, "Dialogue continue hint must not block the full-screen dialogue input layer.");
    }

    private static void AssignHealthUiFont(Scene scene)
    {
        Font healthFont = AssetDatabase.LoadAssetAtPath<Font>(HealthUiFontPath);
        if (healthFont == null)
            throw new System.InvalidOperationException($"Health UI font is missing at {HealthUiFontPath}.");

        Text[] healthLabels = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Text>(true))
            .Where(label => label.name == "Txt_角色血量")
            .ToArray();
        if (healthLabels.Length == 0)
            throw new System.InvalidOperationException("Level_LD has no Txt_角色血量 labels to style.");

        foreach (Text healthLabel in healthLabels)
        {
            healthLabel.font = healthFont;
            EditorUtility.SetDirty(healthLabel);
        }
    }

    private static void ValidateHealthUiFont(Scene scene)
    {
        Font expected = AssetDatabase.LoadAssetAtPath<Font>(HealthUiFontPath);
        Text[] healthLabels = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Text>(true))
            .Where(label => label.name == "Txt_角色血量")
            .ToArray();
        Require(expected != null, "Health UI font asset is missing.");
        Require(healthLabels.Length > 0, "Level_LD has no health UI labels.");
        Require(healthLabels.All(label => label.font == expected), "Every Txt_角色血量 label must use Noto Serif CJK SC.");
    }

    private static Button ButtonNode(string name, Transform parent, Vector2 position, string label, Vector2 size)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = name.Contains("Start") ? new Color(.5f, .045f, .035f, .96f) : new Color(.12f, .12f, .115f, .94f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.72f, .1f, .065f, 1f);
        colors.pressedColor = new Color(.34f, .025f, .02f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        Text labelText = TextNode("Txt_ButtonLabel", go.transform, Vector2.zero, Vector2.zero, label, name.Contains("Start") ? 34 : 26, Color.white, TextAnchor.MiddleCenter, Vector2.zero, true);
        labelText.fontStyle = FontStyle.Bold;
        return button;
    }

    private static Image ImageNode(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text TextNode(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, TextAnchor alignment, Vector2 anchor, bool stretch = false)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = go.GetComponent<RectTransform>();
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-24f, -12f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject NewUi(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null) go.transform.SetParent(parent, false);
        foreach (System.Type component in components) go.AddComponent(component);
        return go;
    }

    private static GameObject NewWorld(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void EnsureEventSystem(Scene scene)
    {
        if (FindInScene<EventSystem>(scene) != null) return;
        GameObject eventSystem = new GameObject("Root_EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystem, scene);
    }

    private static void RenameHelpBackLabel(GameStateUIController gameState)
    {
        Button button = gameState.GetComponentsInChildren<Button>(true).FirstOrDefault(value => value.name == "Btn_HelpBack");
        Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label != null) label.text = "返回 / 继续";
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettingsScene levelLd = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettingsScene[] remaining = EditorBuildSettings.scenes
            .Where(scene => scene.path != ScenePath)
            .ToArray();
        EditorBuildSettings.scenes = new[] { levelLd }.Concat(remaining).ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindGameObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
        return null;
    }

    private static void DestroySceneObject(Scene scene, string name)
    {
        GameObject existing = FindGameObject(scene, name);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
    }
}
