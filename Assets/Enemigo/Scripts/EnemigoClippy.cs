using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo "Clippy".
///  - Idle por defecto + 3 acciones aleatorias (Random.Range solo entre las acciones).
///  - Daña al jugador por contacto y con ataques propios de cada acción.
///  - Si la energía del jugador es igual o menor al 50% se detiene y se queda en Idle.
///    Si el jugador recupera energía por encima del umbral, vuelve a actuar.
/// </summary>
[RequireComponent(typeof(Animator), typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemigoClippy : MonoBehaviour
{
    public enum Accion { Idle = 0, Avion = 1, Bicicleta = 2, Musica = 3 }

    // Nombres usados en el Animator Controller (los crea el menú Herramientas > Enemigo Clippy).
    public const string ParametroAccion = "Accion";
    public const string EstadoIdle = "Idle";
    private static readonly int HashAccion = Animator.StringToHash(ParametroAccion);
    private static readonly string[] NombresClips = { "Clippy_Idle", "Clippy_Avion", "Clippy_Bicicleta", "Clippy_Musica" };

    [Header("Referencias (se buscan solas si están vacías)")]
    [SerializeField] private PlayerEnergy jugador;

    [Header("Comportamiento")]
    [Tooltip("Porcentaje de energía del jugador (0-1) a partir del cual el enemigo se queda en Idle.")]
    [Range(0f, 1f)] [SerializeField] private float umbralEnergia = 0.5f;
    [SerializeField] private float idleMinimo = 1f;
    [SerializeField] private float idleMaximo = 2f;
    [Tooltip("Evita repetir la misma acción dos veces seguidas para que se distingan mejor.")]
    [SerializeField] private bool evitarRepetir = true;

    [Header("Daño por contacto")]
    [SerializeField] private float danoContacto = 5f;
    [SerializeField] private float esperaEntreContactos = 1f;

    [Header("Acción 1: Avión de papel (evento de ataque en la animación)")]
    [SerializeField] private float danoAvion = 15f;
    [SerializeField] private float alcanceAvion = 5f;

    [Header("Acción 2: Bicicleta (embiste hacia el jugador)")]
    [SerializeField] private float velocidadBicicleta = 3f;
    [Tooltip("Distancia máxima que puede alejarse de su posición inicial.")]
    [SerializeField] private float rangoMovimiento = 4f;

    [Header("Acción 3: Música (onda sonora en área)")]
    [SerializeField] private float danoMusica = 2f;
    [SerializeField] private float radioMusica = 2.5f;
    [SerializeField] private float intervaloMusica = 0.5f;

    public Accion AccionActual { get; private set; } = Accion.Idle;
    public bool Calmado { get; private set; }

    private Animator animator;
    private Rigidbody2D rb;
    private Coroutine ciclo;
    private readonly float[] duraciones = { 1f, 2f, 2f, 2f };
    private Vector2 posicionInicial;
    private float siguienteContacto;
    private float siguientePulsoMusica;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        LeerDuracionesDeClips();
    }

    private void Start()
    {
        posicionInicial = rb.position;
        if (jugador == null) BuscarJugador();
        ActualizarModo(forzar: true);
    }

    private void OnDisable()
    {
        ciclo = null; // Unity detiene las corrutinas al desactivar el objeto.
    }

    private void OnEnable()
    {
        if (animator != null && !Calmado && ciclo == null && jugador != null) ciclo = StartCoroutine(CicloAcciones());
    }

    private void Update()
    {
        if (jugador == null) { BuscarJugador(); if (jugador == null) return; }

        ActualizarModo(forzar: false);

        if (!Calmado && AccionActual == Accion.Musica && Time.time >= siguientePulsoMusica)
        {
            siguientePulsoMusica = Time.time + intervaloMusica;
            if (DistanciaAlJugador() <= radioMusica) Danar(danoMusica, "onda de música");
        }
    }

    private void FixedUpdate()
    {
        if (Calmado || AccionActual != Accion.Bicicleta || jugador == null) return;

        float dx = jugador.transform.position.x - rb.position.x;
        if (Mathf.Abs(dx) < 0.05f) return;

        float x = rb.position.x + Mathf.Sign(dx) * velocidadBicicleta * Time.fixedDeltaTime;
        x = Mathf.Clamp(x, posicionInicial.x - rangoMovimiento, posicionInicial.x + rangoMovimiento);
        rb.MovePosition(new Vector2(x, rb.position.y));
    }

    // ---------------------------------------------------------------- Modo según energía

    /// <summary>Cambia entre modo activo y calmado en tiempo real según la energía del jugador.</summary>
    private void ActualizarModo(bool forzar)
    {
        bool debeCalmarse = jugador == null || jugador.Porcentaje <= umbralEnergia;
        if (!forzar && debeCalmarse == Calmado) return;

        Calmado = debeCalmarse;
        if (Calmado)
        {
            if (ciclo != null) { StopCoroutine(ciclo); ciclo = null; }
            CambiarAccion(Accion.Idle);
            animator.Play(EstadoIdle, 0, 0f); // corta cualquier acción al instante
            rb.linearVelocity = Vector2.zero;
            if (jugador != null) Debug.Log($"[{name}] Energía del jugador ≤ {umbralEnergia:P0}: se detiene y queda en Idle.");
        }
        else
        {
            if (!forzar) Debug.Log($"[{name}] Energía del jugador > {umbralEnergia:P0}: reanuda sus acciones.");
            if (ciclo == null) ciclo = StartCoroutine(CicloAcciones());
        }
    }

    // ---------------------------------------------------------------- Acciones aleatorias

    private IEnumerator CicloAcciones()
    {
        var anterior = Accion.Idle;
        while (true)
        {
            CambiarAccion(Accion.Idle);
            yield return new WaitForSeconds(Random.Range(idleMinimo, idleMaximo));

            // Random.Range con enteros excluye el máximo: devuelve 1, 2 o 3 (nunca 0 = Idle).
            var siguiente = (Accion)Random.Range(1, 4);
            if (evitarRepetir && siguiente == anterior) siguiente = (Accion)Random.Range(1, 4);
            anterior = siguiente;

            CambiarAccion(siguiente);
            yield return new WaitForSeconds(duraciones[(int)siguiente]);
        }
    }

    private void CambiarAccion(Accion accion)
    {
        AccionActual = accion;
        animator.SetInteger(HashAccion, (int)accion);
        if (accion != Accion.Idle) Debug.Log($"[{name}] Acción: {accion}");
    }

    private void LeerDuracionesDeClips()
    {
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"[{name}] El Animator no tiene Animator Controller asignado.", this);
            return;
        }
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            for (int i = 0; i < NombresClips.Length; i++)
                if (clip.name == NombresClips[i]) duraciones[i] = clip.length;
    }

    // ---------------------------------------------------------------- Daño

    /// <summary>Llamado por un Animation Event en el clip Clippy_Avion (cuando lanza el avión).</summary>
    public void EventoAtaqueAvion()
    {
        if (Calmado || jugador == null) return;
        if (DistanciaAlJugador() <= alcanceAvion) Danar(danoAvion, "avión de papel");
    }

    private void OnCollisionStay2D(Collision2D otro) => IntentarDanoContacto(otro.collider);
    private void OnTriggerStay2D(Collider2D otro) => IntentarDanoContacto(otro);

    private void IntentarDanoContacto(Collider2D otro)
    {
        if (Calmado || Time.time < siguienteContacto) return;
        var energia = otro.GetComponentInParent<PlayerEnergy>();
        if (energia == null) return;

        jugador = energia;
        siguienteContacto = Time.time + esperaEntreContactos;
        Danar(danoContacto, "contacto");
    }

    private void Danar(float cantidad, string origen)
    {
        Debug.Log($"[{name}] Daña al jugador por {origen}: -{cantidad}");
        jugador.RecibirDano(cantidad);
    }

    // ---------------------------------------------------------------- Utilidades

    private void BuscarJugador()
    {
        var go = GameObject.FindWithTag("Player");
        jugador = go != null ? go.GetComponentInParent<PlayerEnergy>() : null;
        if (jugador == null) jugador = FindAnyObjectByType<PlayerEnergy>();
    }

    private float DistanciaAlJugador() =>
        Vector2.Distance(transform.position, jugador.transform.position);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceAvion);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, radioMusica);
        Gizmos.color = Color.cyan;
        Vector3 origen = Application.isPlaying ? (Vector3)posicionInicial : transform.position;
        Gizmos.DrawLine(origen + Vector3.left * rangoMovimiento, origen + Vector3.right * rangoMovimiento);
    }
}
