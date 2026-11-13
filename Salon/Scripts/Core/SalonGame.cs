using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SalonCoiffure
{
    public enum GameMode { Menu, Libre }

    /// <summary>Un objectif d'étape (case à cocher dans l'interface).</summary>
    public class Objective
    {
        public string text;
        public float progress;
        public bool optional;
        public bool Done { get { return progress >= 0.999f; } }
        public Objective(string t, float p, bool opt = false) { text = t; progress = Mathf.Clamp01(p); optional = opt; }
    }

    /// <summary>
    /// Chef d'orchestre du jeu : construit le salon, gère la cliente, les étapes,
    /// la caméra, les outils, les photos et la sauvegarde.
    /// Il suffit d'un GameObject avec ce composant dans une scène vide.
    /// </summary>
    public class SalonGame : MonoBehaviour
    {
        public static SalonGame Instance { get; private set; }

        public SaveData Save { get; private set; }
        public SalonWorld World { get; private set; }
        public Camera MainCam { get; private set; }
        public OrbitCamera Cam { get; private set; }
        public SalonAudio Audio { get; private set; }
        public HairClippings Clippings { get; private set; }
        public FoamSystem Foam { get; private set; }
        public PhotoBooth Photo { get; private set; }
        public SalonUI UI { get; private set; }
        public ToolController Tools { get; private set; }
        public ToolProps Props { get; private set; }
        public CustomerAvatar Client { get; private set; }
        public CutGuides Guides { get; private set; }

        public GameMode Mode { get; private set; }
        public Stage Stage { get; private set; }
        public bool AtBac { get; private set; }
        public bool Busy { get; private set; }
        public bool ShowGuides = true;

        public bool PoseRunning;
        public float PoseTimer;
        public float PoseDuration = 25f;
        public float PoseSpeed = 1f;
        public float Develop;

        public SessionInfo Session;
        public ScoreReport Report { get; private set; }
        public float ReportTime;
        public Texture2D PhotoBefore { get; private set; }
        public Texture2D PhotoAfter { get; private set; }
        public string LastSavedPhoto;
        public bool WelcomeDone;

        public float Fade;
        public float Flash;
        public bool Loading;

        readonly System.Random rng = new System.Random();
        float pasteCheckTimer, objTimer;
        Transform clientRoot;
        List<Objective> objectives = new List<Objective>();
        bool stageWasDone;

        public Vector3 HeadWorld
        {
            get { return Client != null ? Client.Head.position : SalonWorld.HeadWorld(false); }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (Application.isMobilePlatform)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }

            Save = SaveData.Load();
            Mats.Init();

            World = new GameObject("Salon").AddComponent<SalonWorld>();
            World.transform.SetParent(transform, false);
            World.Build(Save.salonName);

            SetupCamera();

            Audio = gameObject.AddComponent<SalonAudio>();
            Audio.Init(Save.musicVolume, Save.sfxVolume);

            Clippings = new GameObject("CheveuxCoupes").AddComponent<HairClippings>();
            Clippings.transform.SetParent(transform, false);
            Foam = new GameObject("Mousse").AddComponent<FoamSystem>();
            Foam.transform.SetParent(transform, false);
            Foam.Init();
            Props = new GameObject("Outils3D").AddComponent<ToolProps>();
            Props.transform.SetParent(transform, false);
            Props.Init();
            Photo = gameObject.AddComponent<PhotoBooth>();
            Tools = new ToolController();
            UI = gameObject.AddComponent<SalonUI>();
            UI.Init(this);

            clientRoot = new GameObject("Clientes").transform;
            clientRoot.SetParent(transform, false);

            Mode = GameMode.Menu;
            NewClient();
            Cam.Set(new Vector3(0.1f, 1.25f, 0.6f), 20f, 6f, 2.1f, true);
        }

        void SetupCamera()
        {
            var existing = Camera.main;
            if (existing != null) MainCam = existing;
            else
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                MainCam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            MainCam.nearClipPlane = 0.02f;
            MainCam.farClipPlane = 40f;
            MainCam.fieldOfView = 36f;
            MainCam.clearFlags = CameraClearFlags.SolidColor;
            MainCam.backgroundColor = SalonMath.Hex("#EFE5D8");
            MainCam.allowHDR = true;
            MainCam.allowMSAA = true;
            Cam = MainCam.gameObject.GetComponent<OrbitCamera>();
            if (Cam == null) Cam = MainCam.gameObject.AddComponent<OrbitCamera>();
            PostFX.Setup(MainCam);
        }

        // ------------------------------------------------------------------ Clientes
        public void NewClient()
        {
            if (Client != null) Destroy(Client.gameObject);
            Foam.ClearBubbles();
            Clippings.Sweep();
            int q = Save.EffectiveQuality;
            var profile = CustomerProfile.Random(rng.Next());
            Client = CustomerAvatar.Create(profile, clientRoot, SaveData.StrandsForQuality[q], SaveData.SegmentsForQuality[q]);
            PlaceClient(false);
            AtBac = false;
            Session = new SessionInfo();
            Report = null;
            PoseRunning = false; PoseTimer = 0; Develop = 0; PoseSpeed = 1f;
            if (PhotoBefore != null) Destroy(PhotoBefore);
            if (PhotoAfter != null) Destroy(PhotoAfter);
            PhotoBefore = null; PhotoAfter = null;
            LastSavedPhoto = null;
            WelcomeDone = false;
            Stage = Stage.Accueil;
            Tools.Current = ToolType.Main;
            Tools.ZoneFilter = -1;
            Guides = new GameObject("Reperes").AddComponent<CutGuides>();
            Guides.Build(Client.Head, profile.request);
            Guides.SetVisible(false);
            Client.SetExpression(0.35f);
        }

        void PlaceClient(bool bac)
        {
            Client.transform.position = SalonWorld.ChestAt(bac);
            Client.transform.rotation = SalonWorld.ChestRot(bac);
            Client.Hair.UpdateGravity();
            Client.Hair.Refresh();
        }

        public void StartFreeMode()
        {
            StartCoroutine(StartRoutine());
        }

        IEnumerator StartRoutine()
        {
            Busy = true;
            yield return FadeTo(1f);
            Mode = GameMode.Libre;
            Loading = true;
            yield return null;
            yield return null;
            NewClient();
            Loading = false;
            yield return ArriveRoutine();
        }

        public void BackToMenu()
        {
            Mode = GameMode.Menu;
            Audio.StopLoop();
            Tools.EndStroke();
            Props.Hide();
            if (Guides != null) Guides.SetVisible(false);
            Cam.Set(new Vector3(0.1f, 1.25f, 0.6f), 20f, 6f, 2.1f, false);
        }

        public void NextClient() { StartCoroutine(NextClientRoutine()); }

        IEnumerator NextClientRoutine()
        {
            Busy = true;
            yield return FadeTo(1f);
            Loading = true;
            yield return null;
            yield return null;
            NewClient();
            Loading = false;
            yield return ArriveRoutine();
        }

        IEnumerator ArriveRoutine()
        {
            Busy = true;
            Fade = Mathf.Max(Fade, 0.99f);
            ApplyCameraPreset(Stage.Accueil, true);
            yield return null;
            yield return FadeTo(0f);
            yield return new WaitForSeconds(0.25f);
            yield return Photo.Capture(this, 0f, tex => PhotoBefore = tex);
            ApplyCameraPreset(Stage.Accueil, false);
            Busy = false;
        }

        // ------------------------------------------------------------------ Étapes
        public bool ColorRequested { get { return Client != null && Client.Profile.request.colorKind != ColorKind.Aucune; } }

        public Stage NextStageAfter(Stage s)
        {
            switch (s)
            {
                case Stage.Accueil: return ColorRequested ? Stage.Coloration : Stage.Bac;
                case Stage.Coloration: return Stage.Bac;
                case Stage.Bac: return Stage.Coupe;
                case Stage.Coupe: return Stage.Sechage;
                default: return Stage.Resultat;
            }
        }

        public void GoNext() { GoToStage(NextStageAfter(Stage)); }

        public void GoToStage(Stage s)
        {
            if (Busy || Client == null) return;
            StartCoroutine(StageRoutine(s));
        }

        IEnumerator StageRoutine(Stage s)
        {
            Busy = true;
            Tools.EndStroke();
            Props.Hide();
            bool wantBac = s == Stage.Bac;
            if (wantBac != AtBac)
            {
                yield return FadeTo(1f);
                AtBac = wantBac;
                PlaceClient(AtBac);
                Foam.ClearBubbles();
                Stage = s;
                ApplyCameraPreset(s, true);
                yield return null;
                yield return FadeTo(0f);
            }
            else
            {
                Stage = s;
                ApplyCameraPreset(s, false);
            }

            var tools = ToolCatalog.ForStage(s);
            Tools.Current = tools.Length > 0 ? tools[0] : ToolType.Main;
            Tools.ZoneFilter = -1;
            if (Guides != null) Guides.SetVisible(s == Stage.Coupe && ShowGuides);
            stageWasDone = false;
            objTimer = 0;
            Busy = false;

            if (s == Stage.Resultat)
            {
                Busy = true;
                if (Report == null)
                {
                    Report = Client.Profile.request.Evaluate(Client.Hair.Core, Session, rng);
                    Save.clientsServed++;
                    Save.totalEarned += Report.price + Report.tip;
                    if (Report.stars == 5) Save.fiveStars++;
                    Save.Store();
                }
                Client.SetExpression(Report.stars >= 4 ? 1f : (Report.stars == 3 ? 0.4f : -0.8f));
                yield return new WaitForSeconds(0.6f);
                yield return Photo.Capture(this, 0f, tex => { if (PhotoAfter != null) Destroy(PhotoAfter); PhotoAfter = tex; });
                ApplyCameraPreset(Stage.Resultat, false);
                ReportTime = Time.time;
                Audio.OneShot(Audio.Ding, 0.7f);
                Busy = false;
            }
        }

        public void ApplyCameraPreset(Stage s, bool snap)
        {
            Vector3 head = HeadWorld;
            switch (s)
            {
                case Stage.Bac: Cam.Set(head, 180f, 50f, 0.78f, snap); break;
                case Stage.Coupe: Cam.Set(head + new Vector3(0, -0.06f, 0), 28f, 4f, 0.78f, snap); break;
                case Stage.Coloration: Cam.Set(head + new Vector3(0, -0.04f, 0), -32f, 10f, 0.72f, snap); break;
                case Stage.Sechage: Cam.Set(head + new Vector3(0, -0.06f, 0), 18f, 6f, 0.8f, snap); break;
                case Stage.Resultat: Cam.Set(head + new Vector3(0, -0.05f, 0), 12f, 4f, 0.8f, snap); break;
                default: Cam.Set(head + new Vector3(0, -0.07f, 0), 0f, 4f, 0.85f, snap); break;
            }
        }

        public void ViewFrom(float yaw)
        {
            Cam.yaw = yaw;
            Cam.pitch = Stage == Stage.Bac ? 50f : 6f;
        }

        public void StartPose()
        {
            if (!Client.Hair.HasPaste()) { Tools.Say("Applique d'abord la coloration au pinceau."); return; }
            PoseRunning = true;
            PoseTimer = 0;
            Develop = 0;
        }

        IEnumerator FadeTo(float target)
        {
            while (!Mathf.Approximately(Fade, target))
            {
                Fade = Mathf.MoveTowards(Fade, target, Time.unscaledDeltaTime * 3.5f);
                yield return null;
            }
        }

        public void SavePhoto()
        {
            if (PhotoAfter == null) return;
            LastSavedPhoto = Photo.SaveBeforeAfter(PhotoBefore, PhotoAfter);
            if (LastSavedPhoto != null)
            {
                Save.photosSaved++;
                Save.Store();
                Tools.Say("Photo enregistrée dans la galerie !", 3f);
                Audio.OneShot(Audio.Ding, 0.5f);
            }
        }

        // ------------------------------------------------------------------ Objectifs
        public List<Objective> Objectives { get { return objectives; } }

        public bool StageComplete
        {
            get
            {
                if (objectives.Count == 0) return Stage == Stage.Accueil ? WelcomeDone : false;
                foreach (var o in objectives) if (!o.optional && !o.Done) return false;
                return true;
            }
        }

        void UpdateObjectives()
        {
            objectives = new List<Objective>();
            if (Client == null) return;
            var hair = Client.Hair;
            var req = Client.Profile.request;
            switch (Stage)
            {
                case Stage.Coloration:
                {
                    float cov = hair.PasteCoverage();
                    float need = req.colorKind == ColorKind.Meches ? 0.2f : 0.75f;
                    objectives.Add(new Objective(req.colorKind == ColorKind.Meches ? "Peindre quelques mèches" : "Couvrir toute la chevelure", cov / need));
                    objectives.Add(new Objective("Temps de pose", Develop));
                    break;
                }
                case Stage.Bac:
                {
                    float wet = hair.AverageWet();
                    objectives.Add(new Objective("Mouiller les cheveux", Session.maxWet / 0.85f));
                    objectives.Add(new Objective("Faire mousser le shampoing", Session.maxFoam / 0.55f));
                    float rinsed = Session.maxFoam > 0.3f ? 1f - hair.AverageFoam() / 0.05f : 0f;
                    if (hair.HasPaste()) rinsed = 0f;
                    objectives.Add(new Objective("Bien rincer", Mathf.Clamp01(rinsed)));
                    objectives.Add(new Objective("Soin (bonus)", Session.maxCare / 0.4f, true));
                    objectives.Add(new Objective("Essorer à la serviette", Session.maxFoam > 0.3f && rinsed >= 1f ? (1f - wet) / 0.4f : 0f, true));
                    break;
                }
                case Stage.Coupe:
                {
                    for (int z = 0; z < 4; z++)
                    {
                        var t = req.zones[z];
                        if (t.kind == TargetKind.Aucune) continue;
                        float p = hair.Core.ZoneProgress(z, t.kind, t.value, 0.025f);
                        objectives.Add(new Objective(HairCore.ZoneNames[z] + " : " + req.ZoneHint(z), p / 0.8f));
                    }
                    break;
                }
                case Stage.Sechage:
                    objectives.Add(new Objective("Sécher toute la chevelure", (1f - hair.AverageWet()) / 0.9f));
                    break;
            }
        }

        // ------------------------------------------------------------------ Boucle
        void Update()
        {
            float dt = Time.deltaTime;
            Flash = Mathf.MoveTowards(Flash, 0f, dt * 2.5f);
            UpdateShaderGlobals();
            PostFX.SetFocus(Cam.CurrentDistance);

            if (Mode == GameMode.Menu)
            {
                Cam.yaw = 20f + Mathf.Sin(Time.time * 0.12f) * 18f;
                Tools.EndStroke();
            }

            if (Client != null)
            {
                Session.maxFoam = Mathf.Max(Session.maxFoam, Client.Hair.AverageFoam());
                Session.maxCare = Mathf.Max(Session.maxCare, Client.Hair.AverageCare());
                Session.maxWet = Mathf.Max(Session.maxWet, Client.Hair.AverageWet());
            }

            if (PoseRunning)
            {
                PoseTimer += dt * PoseSpeed;
                Develop = Mathf.Clamp01(PoseTimer / PoseDuration);
                Session.colorDevelop = Develop;
                if (Develop >= 1f)
                {
                    PoseRunning = false;
                    Tools.Say("Temps de pose terminé ! Direction le bac pour rincer.", 4f);
                    Audio.OneShot(Audio.Ding, 0.6f);
                }
            }
            else if (Develop > 0 && Client != null)
            {
                pasteCheckTimer -= dt;
                if (pasteCheckTimer <= 0)
                {
                    pasteCheckTimer = 0.5f;
                    if (!Client.Hair.HasPaste()) Develop = 0;
                }
            }

            if (Mode == GameMode.Libre)
            {
                Tools.Tick(this, dt);
                objTimer -= dt;
                if (objTimer <= 0)
                {
                    objTimer = 0.25f;
                    UpdateObjectives();
                    bool done = StageComplete;
                    if (done && !stageWasDone && Stage != Stage.Accueil && Stage != Stage.Resultat)
                    {
                        Audio.OneShot(Audio.Ding, 0.45f);
                        Tools.Say("Étape terminée ! Tu peux passer à la suite.", 2.5f);
                    }
                    stageWasDone = done;
                }
            }
            else
            {
                Audio.StopLoop();
                Props.Hide();
            }
        }

        void UpdateShaderGlobals()
        {
            var key = World.KeyLight;
            var fill = World.FillLight;
            Vector3 head = HeadWorld;
            Shader.SetGlobalVector("_SalonLightDir", -key.transform.forward);
            Color kc = key.color.linear * key.intensity;
            Shader.SetGlobalVector("_SalonLightColor", new Vector4(kc.r, kc.g, kc.b, 1));
            Vector3 fd = (fill.transform.position - head).normalized;
            Shader.SetGlobalVector("_SalonFillDir", fd);
            Color fc = fill.color.linear * 0.45f;
            Shader.SetGlobalVector("_SalonFillColor", new Vector4(fc.r, fc.g, fc.b, 1));
            Color amb = RenderSettings.ambientLight.linear * 1.6f;
            Shader.SetGlobalVector("_SalonAmbient", new Vector4(amb.r, amb.g, amb.b, 1));
        }

        public void ApplyOptions()
        {
            Audio.SetVolumes(Save.musicVolume, Save.sfxVolume);
            World.SetSalonName(Save.salonName);
            Save.Store();
        }
    }
}
