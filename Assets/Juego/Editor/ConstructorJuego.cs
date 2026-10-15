using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Menú: Herramientas > Clippy vs Bonzi
///  1. Construir juego: corta los sprites de Bonzi, crea animaciones, Animator Controllers,
///     prefabs (banana y cáscara) y la escena Assets/Juego/Juego.unity.
///  2. Compilar para WebGL: genera la página del juego dentro del proyecto web
///     (../frontend-html/game) lista para cargarse en un iframe.
/// Desde terminal: Unity -batchmode -quit -projectPath . -buildTarget WebGL
///                 -executeMethod ConstructorJuego.CompilarDesdeTerminal [-salida ruta]
/// </summary>
public static class ConstructorJuego
{
    private const string Raiz = "Assets/Juego";
    private const string CarpetaSprites = Raiz + "/Sprites";
    private const string CarpetaAnim = Raiz + "/Animaciones";
    private const string CarpetaPrefabs = Raiz + "/Prefabs";
    private const string RutaEscena = Raiz + "/Juego.unity";
    private const string SpritesClippy = "Assets/Enemigo/Sprites";
    private const string MaterialSinLuz = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const string SalidaPorDefecto = "../../frontend-html/game"; // relativa a Assets/

    private static readonly Color FondoEscritorio = new Color(0f, 0.5f, 0.5f); // verde azulado de Windows 98

    [MenuItem("Herramientas/Clippy vs Bonzi/1. Construir juego (sprites, animaciones, prefabs, escena)")]
    public static void Construir()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Directory.CreateDirectory(CarpetaAnim);
        Directory.CreateDirectory(CarpetaPrefabs);

        // ---- Sprites
        var bonziIdle = CortarTira($"{CarpetaSprites}/bonzi_idle.png", 200, 160, 100);
        var bonziLanzar = CortarTira($"{CarpetaSprites}/bonzi_lanzar.png", 200, 160, 100);
        var banana = SpriteSimple($"{CarpetaSprites}/banana.png", 40);
        var cascara = SpriteSimple($"{CarpetaSprites}/cascara.png", 50);
        var clippyIdle = CargarSprites($"{SpritesClippy}/clippy_idle.png");
        var clippyBici = CargarSprites($"{SpritesClippy}/clippy_bicicleta.png");
        if (clippyIdle.Length == 0 || clippyBici.Length < 37)
        {
            Debug.LogError("Faltan los sprites de Clippy. Ejecuta antes: Herramientas > Enemigo Clippy > 1. Construir enemigo.");
            return;
        }

        // ---- Animaciones
        var clipClippyIdle = CrearClip("Clippy_Jugador_Idle", clippyIdle, 24f, true);
        var clipClippyBici = CrearClip("Clippy_Jugador_Bici", clippyBici.Skip(12).Take(25).ToArray(), 24f, true);
        var clipBonziIdle = CrearClip("Bonzi_Idle", bonziIdle, 8f, true);
        var clipBonziLanzar = CrearClip("Bonzi_Lanzar", bonziLanzar, 12f, false);

        var controllerClippy = ControllerClippy(clipClippyIdle, clipClippyBici);
        var controllerBonzi = ControllerBonzi(clipBonziIdle, clipBonziLanzar);

        // ---- Prefabs
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialSinLuz);
        var prefabBanana = PrefabBanana(banana, material);
        var prefabCascara = PrefabCascara(cascara, material);

        // ---- Escena
        CrearEscena(material, clippyIdle[0], controllerClippy, bonziIdle[0], controllerBonzi, prefabBanana, prefabCascara);

        AssetDatabase.SaveAssets();
        Debug.Log("Clippy vs Bonzi construido: " + RutaEscena);
    }

    [MenuItem("Herramientas/Clippy vs Bonzi/2. Compilar para WebGL (página del juego)")]
    public static void CompilarDesdeMenu()
    {
        string salida = Path.GetFullPath(Path.Combine(Application.dataPath, SalidaPorDefecto));
        if (!Directory.Exists(Path.GetDirectoryName(salida)))
        {
            salida = EditorUtility.SaveFolderPanel("Carpeta donde se guardará el juego WebGL", "", "game");
            if (string.IsNullOrEmpty(salida)) return;
        }
        if (Compilar(salida)) EditorUtility.RevealInFinder(Path.Combine(salida, "index.html"));
    }

    public static void CompilarDesdeTerminal()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-salida");
        string salida = i >= 0 && i + 1 < args.Length
            ? Path.GetFullPath(args[i + 1])
            : Path.GetFullPath(Path.Combine(Application.dataPath, SalidaPorDefecto));

        Construir();
        if (!Compilar(salida)) EditorApplication.Exit(1);
    }

    private static bool Compilar(string salida)
    {
        if (!File.Exists(RutaEscena)) Construir();

        PlayerSettings.productName = "Clippy vs BonziBuddy";
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;
        PlayerSettings.runInBackground = true;
        // Gzip + "decompression fallback": los archivos pesan ~4 veces menos y el navegador los
        // descomprime con JavaScript, así funciona en cualquier servidor (GitHub Pages, http.server...)
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.stripEngineCode = true;
        ComprimirTexturasClippyParaWeb();
        PlayerSettings.WebGL.template = "PROJECT:ClippyVsBonzi"; // Assets/WebGLTemplates/ClippyVsBonzi
        PlayerSettings.WebGL.dataCaching = false;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };

        var reporte = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { RutaEscena },
            locationPathName = salida,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        bool ok = reporte.summary.result == BuildResult.Succeeded;
        Debug.Log(ok
            ? $"WebGL compilado en {salida} ({reporte.summary.totalSize / 1024 / 1024} MB)"
            : $"Falló la compilación WebGL: {reporte.summary.result} ({reporte.summary.totalErrors} errores)");
        return ok;
    }

    // ------------------------------------------------------------------ Sprites

    /// <summary>
    /// Las hojas de Clippy se importan sin compresión (pesan ~20 MB cada una). Solo para WebGL
    /// se les pone una compresión de GPU, sin tocar la configuración de las demás plataformas.
    /// </summary>
    private static void ComprimirTexturasClippyParaWeb()
    {
        foreach (string archivo in new[] { "clippy_idle.png", "clippy_bicicleta.png", "clippy_avion.png", "clippy_musica.png" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath($"{SpritesClippy}/{archivo}");
            if (importer == null) continue;
            var web = importer.GetPlatformTextureSettings("WebGL");
            if (web.overridden && web.format == TextureImporterFormat.ASTC_6x6) continue;
            web.overridden = true;
            web.maxTextureSize = 4096;
            web.format = TextureImporterFormat.ASTC_6x6;
            web.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(web);
            importer.SaveAndReimport();
        }
    }

    private static Sprite[] CortarTira(string ruta, int ancho, int alto, float ppu)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(ruta);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;

        importer.GetSourceTextureWidthAndHeight(out int anchoTotal, out _);
        int cuadros = anchoTotal / ancho;
        string baseNombre = Path.GetFileNameWithoutExtension(ruta);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var proveedor = factory.GetSpriteEditorDataProviderFromObject(importer);
        proveedor.InitSpriteEditorDataProvider();

        var rects = new SpriteRect[cuadros];
        for (int i = 0; i < cuadros; i++)
        {
            rects[i] = new SpriteRect
            {
                name = $"{baseNombre}_{i}",
                spriteID = GUID.Generate(),
                rect = new Rect(i * ancho, 0, ancho, alto),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
        }
        proveedor.SetSpriteRects(rects);
        proveedor.GetDataProvider<ISpriteNameFileIdDataProvider>()?
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        proveedor.Apply();
        importer.SaveAndReimport();
        return CargarSprites(ruta);
    }

    private static Sprite SpriteSimple(string ruta, float ppu)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(ruta);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    private static Sprite[] CargarSprites(string ruta) =>
        AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();

    // ------------------------------------------------------------------ Animaciones

    private static AnimationClip CrearClip(string nombre, Sprite[] sprites, float fps, bool loop)
    {
        var clip = new AnimationClip { name = nombre, frameRate = fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);

        string ruta = $"{CarpetaAnim}/{nombre}.anim";
        var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        if (existente != null)
        {
            EditorUtility.CopySerialized(clip, existente);
            return existente;
        }
        AssetDatabase.CreateAsset(clip, ruta);
        return clip;
    }

    private static AnimatorController ControllerClippy(AnimationClip idle, AnimationClip bici)
    {
        string ruta = $"{CarpetaAnim}/ClippyJugador.controller";
        AssetDatabase.DeleteAsset(ruta);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        controller.AddParameter(ClippyJugador.ParametroMoviendo, AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;
        var estadoIdle = sm.AddState("Idle", new Vector3(300, 0));
        estadoIdle.motion = idle;
        var estadoBici = sm.AddState("Bicicleta", new Vector3(300, 120));
        estadoBici.motion = bici;
        sm.defaultState = estadoIdle;

        var aBici = estadoIdle.AddTransition(estadoBici);
        aBici.AddCondition(AnimatorConditionMode.If, 0, ClippyJugador.ParametroMoviendo);
        aBici.hasExitTime = false; aBici.duration = 0f;

        var aIdle = estadoBici.AddTransition(estadoIdle);
        aIdle.AddCondition(AnimatorConditionMode.IfNot, 0, ClippyJugador.ParametroMoviendo);
        aIdle.hasExitTime = false; aIdle.duration = 0f;
        return controller;
    }

    private static AnimatorController ControllerBonzi(AnimationClip idle, AnimationClip lanzar)
    {
        string ruta = $"{CarpetaAnim}/Bonzi.controller";
        AssetDatabase.DeleteAsset(ruta);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        controller.AddParameter(BonziEnemigo.ParametroLanzar, AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var estadoIdle = sm.AddState("Idle", new Vector3(300, 0));
        estadoIdle.motion = idle;
        var estadoLanzar = sm.AddState("Lanzar", new Vector3(300, 120));
        estadoLanzar.motion = lanzar;
        sm.defaultState = estadoIdle;

        var aLanzar = estadoIdle.AddTransition(estadoLanzar);
        aLanzar.AddCondition(AnimatorConditionMode.If, 0, BonziEnemigo.ParametroLanzar);
        aLanzar.hasExitTime = false; aLanzar.duration = 0f;

        var aIdle = estadoLanzar.AddTransition(estadoIdle);
        aIdle.hasExitTime = true; aIdle.exitTime = 1f; aIdle.duration = 0f;
        return controller;
    }

    // ------------------------------------------------------------------ Prefabs

    private static SpriteRenderer PonerSprite(GameObject go, Sprite sprite, Material material, int orden)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = orden;
        if (material != null) sr.sharedMaterial = material;
        return sr;
    }

    private static Banana PrefabBanana(Sprite sprite, Material material)
    {
        var go = new GameObject("Banana");
        try
        {
            PonerSprite(go, sprite, material, 1);
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.35f;
            go.AddComponent<Banana>();
            return PrefabUtility.SaveAsPrefabAsset(go, $"{CarpetaPrefabs}/Banana.prefab").GetComponent<Banana>();
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static Cascara PrefabCascara(Sprite sprite, Material material)
    {
        var go = new GameObject("Cascara");
        try
        {
            PonerSprite(go, sprite, material, 4);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.25f;
            go.AddComponent<Cascara>();
            return PrefabUtility.SaveAsPrefabAsset(go, $"{CarpetaPrefabs}/Cascara.prefab").GetComponent<Cascara>();
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // ------------------------------------------------------------------ Escena

    private static void CrearEscena(Material material, Sprite spriteClippy, AnimatorController controllerClippy,
        Sprite spriteBonzi, AnimatorController controllerBonzi, Banana prefabBanana, Cascara prefabCascara)
    {
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // NewScene descarga los assets que no están en uso: se vuelven a cargar desde su ruta
        material = Recargar(material);
        spriteClippy = Recargar(spriteClippy);
        spriteBonzi = Recargar(spriteBonzi);
        controllerClippy = Recargar(controllerClippy);
        controllerBonzi = Recargar(controllerBonzi);
        prefabBanana = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPrefabs}/Banana.prefab").GetComponent<Banana>();
        prefabCascara = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPrefabs}/Cascara.prefab").GetComponent<Cascara>();

        var camara = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
        camara.orthographic = true;
        camara.orthographicSize = 5f;
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = FondoEscritorio;
        camara.transform.position = new Vector3(0f, 0f, -10f);

        // Clippy (jugador)
        var clippyGo = new GameObject("Clippy") { tag = "Player" };
        clippyGo.transform.position = new Vector3(-5f, -1f, 0f);
        PonerSprite(clippyGo, spriteClippy, material, 3);
        clippyGo.AddComponent<Animator>().runtimeAnimatorController = controllerClippy;
        var rbClippy = clippyGo.AddComponent<Rigidbody2D>();
        rbClippy.gravityScale = 0f;
        rbClippy.freezeRotation = true;
        rbClippy.interpolation = RigidbodyInterpolation2D.Interpolate;
        rbClippy.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var colClippy = clippyGo.AddComponent<CapsuleCollider2D>();
        colClippy.size = new Vector2(0.8f, 1.2f);
        var energia = clippyGo.AddComponent<PlayerEnergy>();
        var so = new SerializedObject(energia);
        so.FindProperty("mostrarBarra").boolValue = false; // el HUD del juego dibuja su propia barra
        so.ApplyModifiedPropertiesWithoutUndo();
        var clippy = clippyGo.AddComponent<ClippyJugador>();

        // Bonzi (enemigo)
        var bonziGo = new GameObject("Bonzi");
        bonziGo.transform.position = new Vector3(5f, 1f, 0f);
        bonziGo.transform.localScale = Vector3.one * 1.5f;
        PonerSprite(bonziGo, spriteBonzi, material, 2);
        bonziGo.AddComponent<Animator>().runtimeAnimatorController = controllerBonzi;
        var rbBonzi = bonziGo.AddComponent<Rigidbody2D>();
        rbBonzi.bodyType = RigidbodyType2D.Kinematic;
        rbBonzi.gravityScale = 0f;
        rbBonzi.interpolation = RigidbodyInterpolation2D.Interpolate;
        var colBonzi = bonziGo.AddComponent<BoxCollider2D>();
        colBonzi.isTrigger = true;
        colBonzi.size = new Vector2(0.6f, 0.9f);
        var bonzi = bonziGo.AddComponent<BonziEnemigo>();
        Asignar(bonzi, "clippy", clippy);
        Asignar(bonzi, "prefabCascara", prefabCascara);

        // Gestor del juego + API
        var gestorGo = new GameObject("GestorJuego");
        var api = gestorGo.AddComponent<ApiJuego>();
        var gestor = gestorGo.AddComponent<GestorJuego>();
        Asignar(gestor, "api", api);
        Asignar(gestor, "clippy", clippy);
        Asignar(gestor, "bonzi", bonzi);
        Asignar(gestor, "prefabBanana", prefabBanana);

        EditorSceneManager.SaveScene(escena, RutaEscena);
    }

    private static T Recargar<T>(T asset) where T : UnityEngine.Object
    {
        if (asset == null) return null;
        string ruta = AssetDatabase.GetAssetPath(asset);
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<T>().FirstOrDefault(a => a.name == asset.name) ?? asset;
    }

    private static void Asignar(UnityEngine.Object destino, string campo, UnityEngine.Object valor)
    {
        if (valor == null) throw new InvalidOperationException($"No se pudo asignar '{campo}' en {destino.name}: la referencia está vacía.");
        var so = new SerializedObject(destino);
        so.FindProperty(campo).objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
