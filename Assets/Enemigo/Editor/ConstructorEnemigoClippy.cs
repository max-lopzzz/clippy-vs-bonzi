using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Menú: Herramientas > Enemigo Clippy
///  1. Construir enemigo: corta los sprites, crea las 4 animaciones, el Animator Controller
///     y los prefabs Enemigo_Clippy y Jugador_Prueba.
///  2. Colocar en la escena abierta: instancia ambos prefabs para probar.
/// </summary>
public static class ConstructorEnemigoClippy
{
    private const string Raiz = "Assets/Enemigo";
    private const string CarpetaSprites = Raiz + "/Sprites";
    private const string CarpetaAnim = Raiz + "/Animaciones";
    private const string CarpetaPrefabs = Raiz + "/Prefabs";
    private const string RutaController = CarpetaAnim + "/Clippy.controller";
    private const string RutaPrefabEnemigo = CarpetaPrefabs + "/Enemigo_Clippy.prefab";
    private const string RutaPrefabJugador = CarpetaPrefabs + "/Jugador_Prueba.prefab";
    private const string RutaSpriteJugador = CarpetaSprites + "/jugador_placeholder.png";

    private const int TamanoCuadro = 240; // cada cuadro de las hojas mide 240x240 px
    private const int Columnas = 8;
    private const float Fps = 24f;
    private const int CuadroLanzamientoAvion = 40; // cuadro donde Clippy lanza el avión

    private struct Anim
    {
        public string archivo, clip, estado; public int cuadros; public bool loop; public int accion;
        public Anim(string archivo, string clip, string estado, int cuadros, bool loop, int accion)
        { this.archivo = archivo; this.clip = clip; this.estado = estado; this.cuadros = cuadros; this.loop = loop; this.accion = accion; }
    }

    private static readonly Anim[] Animaciones =
    {
        new Anim("clippy_idle.png",      "Clippy_Idle",      EnemigoClippy.EstadoIdle, 33, true,  0),
        new Anim("clippy_avion.png",     "Clippy_Avion",     "Accion1_Avion",          84, false, 1),
        new Anim("clippy_bicicleta.png", "Clippy_Bicicleta", "Accion2_Bicicleta",      60, false, 2),
        new Anim("clippy_musica.png",    "Clippy_Musica",    "Accion3_Musica",         84, false, 3),
    };

    [MenuItem("Herramientas/Enemigo Clippy/1. Construir enemigo (sprites, animaciones, prefabs)")]
    public static void Construir()
    {
        Directory.CreateDirectory(CarpetaAnim);
        Directory.CreateDirectory(CarpetaPrefabs);

        var clips = new AnimationClip[Animaciones.Length];
        Sprite primerSprite = null;
        for (int i = 0; i < Animaciones.Length; i++)
        {
            var a = Animaciones[i];
            string ruta = $"{CarpetaSprites}/{a.archivo}";
            CortarHoja(ruta, a.cuadros);
            var sprites = CargarSprites(ruta);
            if (sprites.Length == 0) { Debug.LogError($"No se encontraron sprites en {ruta}"); return; }
            if (i == 0) primerSprite = sprites[0];
            clips[i] = CrearClip(a, sprites);
        }

        var controller = CrearController(clips);
        ConfigurarSpriteJugador();
        CrearPrefabEnemigo(controller, primerSprite);
        CrearPrefabJugador();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabEnemigo));
        Debug.Log("Enemigo Clippy construido: " + RutaPrefabEnemigo);
    }

    [MenuItem("Herramientas/Enemigo Clippy/2. Colocar enemigo y jugador en la escena abierta")]
    public static void ColocarEnEscena()
    {
        var enemigo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabEnemigo);
        var jugador = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabJugador);
        if (enemigo == null || jugador == null) { Construir(); enemigo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabEnemigo); jugador = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabJugador); }

        if (Object.FindAnyObjectByType<PlayerEnergy>() == null)
        {
            var j = (GameObject)PrefabUtility.InstantiatePrefab(jugador);
            j.transform.position = new Vector3(-4f, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(j, "Colocar jugador");
        }
        var e = (GameObject)PrefabUtility.InstantiatePrefab(enemigo);
        e.transform.position = new Vector3(2f, 0f, 0f);
        Undo.RegisterCreatedObjectUndo(e, "Colocar enemigo");
        Selection.activeGameObject = e;
        EditorSceneManager.MarkSceneDirty(e.scene);
    }

    [MenuItem("Herramientas/Enemigo Clippy/3. Exportar paquete de entrega (.unitypackage)")]
    public static void ExportarPaquete()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabEnemigo) == null)
        {
            EditorUtility.DisplayDialog("Exportar", "Primero ejecuta \"1. Construir enemigo\".", "OK");
            return;
        }

        string ruta = EditorUtility.SaveFilePanel("Exportar enemigo", "", "Matricula_EnemigoCondicional", "unitypackage");
        if (string.IsNullOrEmpty(ruta)) return;

        // Se exporta la carpeta completa: prefab + scripts (incluido PlayerEnergy, del que depende
        // el enemigo) + Animator Controller + animaciones + sprites. Así compila al importarlo.
        AssetDatabase.ExportPackage(Raiz, ruta, ExportPackageOptions.Recurse);
        EditorUtility.RevealInFinder(ruta);
        Debug.Log("Paquete exportado: " + ruta);
    }

    // ------------------------------------------------------------------ Sprites

    private static void CortarHoja(string ruta, int cuadros)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(ruta);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;

        int filas = Mathf.CeilToInt(cuadros / (float)Columnas);
        int alto = filas * TamanoCuadro;
        string baseNombre = Path.GetFileNameWithoutExtension(ruta);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var proveedor = factory.GetSpriteEditorDataProviderFromObject(importer);
        proveedor.InitSpriteEditorDataProvider();

        var rects = new SpriteRect[cuadros];
        for (int i = 0; i < cuadros; i++)
        {
            int col = i % Columnas, fila = i / Columnas;
            rects[i] = new SpriteRect
            {
                name = $"{baseNombre}_{i}",
                spriteID = GUID.Generate(),
                rect = new Rect(col * TamanoCuadro, alto - (fila + 1) * TamanoCuadro, TamanoCuadro, TamanoCuadro),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
        }
        proveedor.SetSpriteRects(rects);
        var nombres = proveedor.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nombres?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        proveedor.Apply();
        importer.SaveAndReimport();
    }

    private static Sprite[] CargarSprites(string ruta) =>
        AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();

    private static void ConfigurarSpriteJugador()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(RutaSpriteJugador);
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------ Animaciones

    private static AnimationClip CrearClip(Anim a, Sprite[] sprites)
    {
        var clip = new AnimationClip { name = a.clip, frameRate = Fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        // último cuadro repetido para que el clip dure exactamente cuadros / fps
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / Fps, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = a.loop;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);

        if (a.accion == (int)EnemigoClippy.Accion.Avion)
        {
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time = CuadroLanzamientoAvion / Fps, functionName = nameof(EnemigoClippy.EventoAtaqueAvion) }
            });
        }

        string ruta = $"{CarpetaAnim}/{a.clip}.anim";
        var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        if (existente != null)
        {
            EditorUtility.CopySerialized(clip, existente); // conserva el GUID y las referencias
            return existente;
        }
        AssetDatabase.CreateAsset(clip, ruta);
        return clip;
    }

    private static AnimatorController CrearController(AnimationClip[] clips)
    {
        AssetDatabase.DeleteAsset(RutaController);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(RutaController);
        controller.AddParameter(EnemigoClippy.ParametroAccion, AnimatorControllerParameterType.Int);

        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState(Animaciones[0].estado, new Vector3(300, 0));
        idle.motion = clips[0];
        sm.defaultState = idle;

        for (int i = 1; i < Animaciones.Length; i++)
        {
            var estado = sm.AddState(Animaciones[i].estado, new Vector3(600, (i - 2) * 90));
            estado.motion = clips[i];

            var entrada = sm.AddAnyStateTransition(estado);
            entrada.AddCondition(AnimatorConditionMode.Equals, Animaciones[i].accion, EnemigoClippy.ParametroAccion);
            entrada.hasExitTime = false;
            entrada.duration = 0f;
            entrada.canTransitionToSelf = false;

            var salida = estado.AddTransition(idle);
            salida.AddCondition(AnimatorConditionMode.Equals, 0, EnemigoClippy.ParametroAccion);
            salida.hasExitTime = false;
            salida.duration = 0f;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ------------------------------------------------------------------ Prefabs

    private static void CrearPrefabEnemigo(AnimatorController controller, Sprite sprite)
    {
        var go = new GameObject("Enemigo_Clippy");
        try
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1;

            go.AddComponent<Animator>().runtimeAnimatorController = controller;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 1.3f);

            go.AddComponent<EnemigoClippy>();
            PrefabUtility.SaveAsPrefabAsset(go, RutaPrefabEnemigo);
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void CrearPrefabJugador()
    {
        var go = new GameObject("Jugador_Prueba") { tag = "Player" };
        try
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RutaSpriteJugador);
            sr.color = new Color(0.25f, 0.55f, 1f);
            sr.sortingOrder = 2;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            go.AddComponent<BoxCollider2D>().size = Vector2.one;
            go.AddComponent<PlayerEnergy>();
            go.AddComponent<JugadorPrueba>();
            PrefabUtility.SaveAsPrefabAsset(go, RutaPrefabJugador);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
