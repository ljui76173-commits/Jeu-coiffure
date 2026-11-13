using System.Collections;
using UnityEngine;

namespace SalonCoiffure
{
    public enum GameMode { Menu, Libre }

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
        public CustomerAvatar Client { get; private set; }

        public GameMode Mode { get; private set; }
        public Stage Stage { get; private set; }
        public bool AtBac { get; private set; }
        public bool Busy { get; private set; }

        public bool PoseRunning;
        public float PoseTimer;
        public float PoseDuration = 30f;
        public float PoseSpeed = 1f;
        public float Develop;

        public SessionInfo Session;
        public ScoreReport Report { get; private set; }
        public Texture2D PhotoBefore { get; private set; }
        public Texture2D PhotoAfter { get; private set; }
        public string LastSavedPhoto;

        public float Fade;   // voile noir des transitions
        public float Flash;  // flash de l'appareil photo

        System.Random rng = new System.Random();
        float pasteCheckTimer;
        Transform clientRoot;

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
            MainCam.fieldOfView = 40f;
            MainCam.clearFlags = CameraClearFlags.SolidColor;
            MainCam.backgroundColor = SalonUtil.Hex("#EFE5D8");
            MainCam.allowHDR = true;
            MainCam.allowMSAA = true;
            Cam = MainCam.gameObject.GetComponent<OrbitCamera>();
            if (Cam == null) Cam = MainCam.gameObject.AddComponent<OrbitCamera>();
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
            PoseRunning = false; PoseTimer = 0; Develop = 0;
            if (PhotoBefore != null) Destroy(PhotoBefore);
            if (PhotoAfter != null) Destroy(PhotoAfter);
            PhotoBefore = null; PhotoAfter = null;
            LastSavedPhoto = null;
            Stage = Stage.Accueil;
            Tools.Current = ToolType.Main;
            Tools.ZoneFilter = -1;
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
            Mode = GameMode.Libre;
            NewClient();
            StartCoroutine(ArriveRoutine());
        }

        public void BackToMenu()
        {
            Mode = GameMode.Menu;
            Audio.StopLoop();
            Cam.Set(new Vector3(0.1f, 1.25f, 0.6f), 20f, 6f, 2.1f, false);
        }

        public void NextClient()
        {
            StartCoroutine(NextClientRoutine());
        }

        IEnumerator NextClientRoutine()
        {
            Busy = true;
            yield return FadeTo(1f);
            NewClient();
            yield return ArriveRoutine();
        }

        IEnumerator ArriveRoutine()
        {
            Busy = true;
            Fade = Mathf.Max(Fade, 0.99f);
            ApplyCameraPreset(Stage.Accueil, true);
            yield return null;
            yield return FadeTo(0f);
            yield return new WaitForSeconds(0.35f);
            yield return Photo.Capture(this, 0f, tex => PhotoBefore = tex);
            ApplyCameraPreset(Stage.Accueil, false);
            Busy = false;
        }

        // ------------------------------------------------------------------ Étapes
        public void GoToStage(Stage s)
        {
            if (Busy || Client == null) return;
            StartCoroutine(StageRoutine(s));
        }

        IEnumerator StageRoutine(Stage s)
        {
            Busy = true;
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
            Tools.Current = tools[0];
            Busy = false;

            if (s == Stage.Resultat)
            {
                Busy = true;
                yield return new WaitForSeconds(0.4f);
                yield return Photo.Capture(this, 0f, tex => { if (PhotoAfter != null) Destroy(PhotoAfter); PhotoAfter = tex; });
                ApplyCameraPreset(Stage.Resultat, false);
                if (Report == null)
                {
                    Report = Client.Profile.request.Evaluate(Client.Hair, Session, rng);
                    Save.clientsServed++;
                    Save.totalEarned += Report.price + Report.tip;
                    if (Report.stars == 5) Save.fiveStars++;
                    Save.Store();
                    Audio.OneShot(Audio.Ding, 0.7f);
                }
                Busy = false;
            }
        }

        public void ApplyCameraPreset(Stage s, bool snap)
        {
            Vector3 head = HeadWorld;
            switch (s)
            {
                case Stage.Bac: Cam.Set(head, 180f, 52f, 0.85f, snap); break;
                case Stage.Coupe: Cam.Set(head + new Vector3(0, -0.05f, 0), 25f, 6f, 0.85f, snap); break;
                case Stage.Coloration: Cam.Set(head + new Vector3(0, -0.03f, 0), -30f, 12f, 0.8f, snap); break;
                case Stage.Sechage: Cam.Set(head + new Vector3(0, -0.05f, 0), 15f, 8f, 0.85f, snap); break;
                default: Cam.Set(head + new Vector3(0, -0.06f, 0), 0f, 5f, 0.85f, snap); break;
            }
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

        // ------------------------------------------------------------------ Boucle
        void Update()
        {
            float dt = Time.deltaTime;
            Flash = Mathf.MoveTowards(Flash, 0f, dt * 2.5f);
            UpdateShaderGlobals();

            if (Mode == GameMode.Menu)
            {
                Cam.yaw = 20f + Mathf.Sin(Time.time * 0.12f) * 18f;
                Tools.PointerActive = false;
            }

            if (Client != null)
            {
                Session.maxFoam = Mathf.Max(Session.maxFoam, Client.Hair.AverageFoam());
                Session.maxCare = Mathf.Max(Session.maxCare, Client.Hair.AverageCare());
            }

            if (PoseRunning)
            {
                PoseTimer += dt * PoseSpeed;
                Develop = Mathf.Clamp01(PoseTimer / PoseDuration);
                Session.colorDevelop = Develop;
                if (Develop >= 1f)
                {
                    PoseRunning = false;
                    Tools.Say("Temps de pose terminé ! Va au bac pour rincer.", 4f);
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

            if (Mode == GameMode.Libre) Tools.Tick(this, dt);
            else Audio.StopLoop();
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
