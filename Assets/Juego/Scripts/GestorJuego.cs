using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla la partida de "Clippy vs BonziBuddy":
///  1. Al iniciar trae los datos del usuario de la API (nombre y puntos guardados).
///  2. Clippy junta las bananas de Bonzi: cada una suma puntos a los del usuario.
///  3. Botón "Guardar puntaje" (o tecla G): guarda los puntos actuales en la BDD con la API.
///  4. Si Bonzi deja a Clippy sin energía, el puntaje se guarda automáticamente.
/// </summary>
public class GestorJuego : MonoBehaviour
{
    public static GestorJuego Instancia { get; private set; }

    private enum Estado { Cargando, Jugando, Perdio }

    [SerializeField] private ApiJuego api;
    [SerializeField] private ClippyJugador clippy;
    [SerializeField] private BonziEnemigo bonzi;
    [SerializeField] private Banana prefabBanana;

    [Header("Bananas")]
    [SerializeField] private int maximoBananas = 4;
    [SerializeField] private float intervaloBananas = 1.5f;
    [SerializeField] private float distanciaMinimaAClippy = 2.5f;

    private Estado estado = Estado.Cargando;
    private string nombre = "Invitado";
    private int puntos;
    private int puntosGuardados;
    private bool guardando;
    private string mensaje = "Cargando datos del usuario...";
    private float finMensaje = float.MaxValue;
    private bool mensajeEsError;
    private float siguienteBanana;
    private string mensajeFinal; // texto de la pantalla "Bonzi te ganó"
    private readonly List<Banana> bananas = new List<Banana>();
    private Vector2 inicioClippy, inicioBonzi;

    private GUIStyle estiloTexto, estiloTitulo, estiloBoton, estiloPanel, estiloCentro, estiloAyuda;

    public bool Jugando => estado == Estado.Jugando;
    public int Puntos => puntos;
    /// <summary>Puntos ganados desde el último guardado (los que se pierden al perder).</summary>
    public int PuntosEnPartida => Mathf.Max(0, puntos - puntosGuardados);
    private bool HayUsuario => api != null && api.IdUsuario > 0;

    private void Awake()
    {
        Instancia = this;
        if (api == null) api = FindAnyObjectByType<ApiJuego>();
        if (clippy == null) clippy = FindAnyObjectByType<ClippyJugador>();
        if (bonzi == null) bonzi = FindAnyObjectByType<BonziEnemigo>();
    }

    private void Start()
    {
        inicioClippy = clippy.transform.position;
        inicioBonzi = bonzi.transform.position;
        clippy.Energia.OnEnergiaCambio += AlCambiarEnergia;

        if (HayUsuario)
        {
            StartCoroutine(api.ObtenerJugador(AlCargarJugador, error =>
            {
                MostrarMensaje(error + " Juegas como invitado: tus puntos no se guardarán.", true, 8f);
                EmpezarPartida();
            }));
        }
        else
        {
            MostrarMensaje("No hay user_id en la URL: inicia sesión en la página para guardar tus puntos.", true, 8f);
            EmpezarPartida();
        }
    }

    private void OnDestroy()
    {
        if (clippy != null) clippy.Energia.OnEnergiaCambio -= AlCambiarEnergia;
        if (Instancia == this) Instancia = null;
    }

    private void AlCargarJugador(ApiJuego.DatosJugador datos)
    {
        nombre = datos.nombre;
        puntos = puntosGuardados = datos.puntos;
        MostrarMensaje($"¡Hola, {nombre}! Junta las bananas de Bonzi.", false, 4f);
        EmpezarPartida();
    }

    private void EmpezarPartida()
    {
        estado = Estado.Jugando;
        siguienteBanana = Time.time + 0.5f;
    }

    private void Update()
    {
        var teclado = Keyboard.current;
        if (teclado != null)
        {
            if (teclado.gKey.wasPressedThisFrame) GuardarPuntaje();
            if (estado == Estado.Perdio && !guardando && (teclado.rKey.wasPressedThisFrame || teclado.enterKey.wasPressedThisFrame)) Reiniciar();
        }

        if (estado == Estado.Jugando && Time.time >= siguienteBanana)
        {
            siguienteBanana = Time.time + intervaloBananas;
            bananas.RemoveAll(b => b == null);
            if (bananas.Count < maximoBananas) CrearBanana();
        }
    }

    private void CrearBanana()
    {
        for (int intento = 0; intento < 10; intento++)
        {
            var cam = Camera.main;
            float alto = cam.orthographicSize, ancho = alto * cam.aspect;
            var posicion = ClippyJugador.LimitarAPantalla(
                new Vector2(Random.Range(-ancho, ancho), Random.Range(-alto, alto)) + (Vector2)cam.transform.position, 0.8f);
            if (Vector2.Distance(posicion, clippy.transform.position) < distanciaMinimaAClippy) continue;

            bananas.Add(Instantiate(prefabBanana, posicion, Quaternion.identity));
            return;
        }
    }

    public void SumarPuntos(int cantidad)
    {
        puntos += cantidad;
    }

    private void AlCambiarEnergia(float actual, float maxima)
    {
        if (actual > 0f || estado != Estado.Jugando) return;
        estado = Estado.Perdio;
        mensaje = null;
        Debug.Log($"[GestorJuego] Bonzi ganó con {puntos} puntos. Guardando automáticamente...");

        // Al perder, el puntaje se guarda solo en la base de datos
        if (!HayUsuario)
        {
            mensajeFinal = $"Hiciste {puntos} puntos. Inicia sesión en la página web para guardarlos.";
        }
        else if (PuntosEnPartida == 0)
        {
            mensajeFinal = $"Tus puntos guardados: {puntosGuardados}";
        }
        else
        {
            mensajeFinal = "Guardando tu puntaje...";
            Guardar(
                datos => mensajeFinal = $"Tu puntaje se guardó automáticamente: {datos.puntos} puntos",
                error => mensajeFinal = "No se pudo guardar tu puntaje: " + error);
        }
    }

    public void GuardarPuntaje()
    {
        if (estado != Estado.Jugando || guardando) return;
        if (!HayUsuario)
        {
            MostrarMensaje("Inicia sesión en la página web para guardar tus puntos.", true, 4f);
            return;
        }

        MostrarMensaje("Guardando...", false, float.MaxValue);
        Guardar(
            datos => MostrarMensaje($"Puntaje guardado en la base de datos: {datos.puntos}", false, 3f),
            error => MostrarMensaje("No se pudo guardar: " + error, true, 5f));
    }

    /// <summary>Envía los puntos actuales a la API (PUT /game/:id).</summary>
    private void Guardar(System.Action<ApiJuego.DatosJugador> alTerminar, System.Action<string> alFallar)
    {
        guardando = true;
        StartCoroutine(api.GuardarPuntos(puntos,
            datos =>
            {
                guardando = false;
                puntosGuardados = datos.puntos;
                alTerminar(datos);
            },
            error =>
            {
                guardando = false;
                alFallar(error);
            }));
    }

    private void Reiniciar()
    {
        foreach (var b in bananas) if (b != null) Destroy(b.gameObject);
        bananas.Clear();
        foreach (var c in FindObjectsByType<Cascara>()) Destroy(c.gameObject);

        clippy.Reiniciar(inicioClippy);
        bonzi.Reiniciar(inicioBonzi);
        clippy.Energia.RecuperarEnergia(clippy.Energia.EnergiaMaxima);
        EmpezarPartida();
    }

    private void MostrarMensaje(string texto, bool esError, float segundos)
    {
        mensaje = texto;
        mensajeEsError = esError;
        finMensaje = segundos == float.MaxValue ? float.MaxValue : Time.unscaledTime + segundos;
    }

    // ------------------------------------------------------------------ HUD

    private void CrearEstilos()
    {
        if (estiloTexto != null) return;
        estiloTexto = new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
        estiloTitulo = new GUIStyle(estiloTexto) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        estiloCentro = new GUIStyle(estiloTexto) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        estiloBoton = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
        estiloPanel = new GUIStyle(GUI.skin.box);
        estiloAyuda = new GUIStyle(estiloCentro) { fontSize = 14 };
    }

    private void Rectangulo(Rect r, Color c)
    {
        var anterior = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = anterior;
    }

    private void OnGUI()
    {
        CrearEstilos();
        // El HUD está diseñado para 600 px de alto: se escala según el tamaño real del canvas
        // (en pantallas de alta densidad el canvas tiene el doble de píxeles).
        float escala = Screen.height / 600f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));
        float ancho = Screen.width / escala;
        float altoPantalla = 600f;

        // Barra superior
        Rectangulo(new Rect(0, 0, ancho, 64), new Color(0.1f, 0.08f, 0.2f, 0.85f));
        GUI.Label(new Rect(16, 6, 420, 26), HayUsuario ? $"{nombre}  (ID {api.IdUsuario})" : nombre, estiloTexto);
        string extra = PuntosEnPartida > 0 ? $"   (+{PuntosEnPartida} sin guardar)" : "";
        GUI.Label(new Rect(16, 32, 520, 26), $"Puntos: {puntos}{extra}", estiloTexto);

        // Energía
        var energia = clippy.Energia;
        var fondo = new Rect(ancho / 2f - 110f, 20f, 220f, 24f);
        Rectangulo(fondo, new Color(0f, 0f, 0f, 0.6f));
        Rectangulo(new Rect(fondo.x + 2, fondo.y + 2, (fondo.width - 4) * energia.Porcentaje, fondo.height - 4),
            energia.Porcentaje > 0.3f ? new Color(0.3f, 0.85f, 0.4f) : new Color(0.95f, 0.35f, 0.25f));
        GUI.Label(new Rect(fondo.x + 8, fondo.y, fondo.width, fondo.height), $"Energía {energia.EnergiaActual:0}", estiloTexto);

        // Botón guardar
        GUI.enabled = estado == Estado.Jugando && !guardando;
        if (GUI.Button(new Rect(ancho - 236, 12, 220, 40), guardando ? "Guardando..." : "Guardar puntaje (G)", estiloBoton))
            GuardarPuntaje();
        GUI.enabled = true;

        // Mensajes
        if (!string.IsNullOrEmpty(mensaje) && Time.unscaledTime < finMensaje)
        {
            var r = new Rect(ancho / 2f - 330f, 74f, 660f, 34f);
            Rectangulo(r, mensajeEsError ? new Color(0.6f, 0.12f, 0.12f, 0.85f) : new Color(0.1f, 0.35f, 0.2f, 0.85f));
            GUI.Label(r, mensaje, estiloCentro);
        }

        GUI.Label(new Rect(16, altoPantalla - 44, ancho - 32, 40),
            "Mueve a Clippy con WASD o las flechas · Junta bananas (+10) · Esquiva a Bonzi y sus cáscaras", estiloAyuda);

        if (estado == Estado.Cargando)
        {
            Rectangulo(new Rect(0, 0, ancho, altoPantalla), new Color(0, 0, 0, 0.6f));
            GUI.Label(new Rect(0, altoPantalla / 2f - 30, ancho, 60), "Cargando datos del usuario...", estiloTitulo);
        }
        else if (estado == Estado.Perdio)
        {
            Rectangulo(new Rect(0, 0, ancho, altoPantalla), new Color(0, 0, 0, 0.65f));
            float y = altoPantalla / 2f - 90f;
            GUI.Label(new Rect(0, y, ancho, 50), "¡Bonzi te ganó!", estiloTitulo);
            GUI.Label(new Rect(ancho / 2f - 300, y + 55, 600, 60), mensajeFinal, estiloCentro);
            GUI.enabled = !guardando;
            if (GUI.Button(new Rect(ancho / 2f - 110, y + 125, 220, 44), "Jugar otra vez (R)", estiloBoton)) Reiniciar();
            GUI.enabled = true;
        }
    }
}
