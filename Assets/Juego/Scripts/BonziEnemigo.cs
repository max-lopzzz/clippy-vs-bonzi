using System.Collections;
using UnityEngine;

/// <summary>
/// BonziBuddy, el villano. Persigue a Clippy y cada cierto tiempo se detiene
/// para lanzarle una cáscara de plátano. Si lo toca, también le quita energía.
/// Se vuelve más rápido conforme Clippy junta más bananas en la partida.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
public class BonziEnemigo : MonoBehaviour
{
    public const string ParametroLanzar = "Lanzar";
    private static readonly int HashLanzar = Animator.StringToHash(ParametroLanzar);

    [SerializeField] private ClippyJugador clippy;
    [SerializeField] private Cascara prefabCascara;

    [Header("Persecución")]
    [SerializeField] private float velocidadBase = 1.4f;
    [SerializeField] private float velocidadMaxima = 3.4f;
    [Tooltip("Velocidad extra por cada punto ganado en la partida actual.")]
    [SerializeField] private float aumentoPorPunto = 0.008f;

    [Header("Lanzamiento de cáscaras")]
    [SerializeField] private float esperaMinima = 2f;
    [SerializeField] private float esperaMaxima = 4f;
    [Tooltip("Segundos desde que empieza la animación hasta que suelta la cáscara.")]
    [SerializeField] private float momentoLanzamiento = 0.2f;
    [SerializeField] private float duracionLanzamiento = 0.65f;

    [Header("Daño por contacto")]
    [SerializeField] private float danoContacto = 10f;
    [SerializeField] private float esperaEntreContactos = 1f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool lanzando;
    private float siguienteLanzamiento;
    private float siguienteContacto;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (clippy == null) clippy = FindAnyObjectByType<ClippyJugador>();
    }

    private void OnEnable() => ProgramarLanzamiento();

    private float Dificultad =>
        GestorJuego.Instancia != null ? GestorJuego.Instancia.PuntosEnPartida * aumentoPorPunto : 0f;

    private void Update()
    {
        if (!PuedeActuar() || lanzando) return;
        if (Time.time >= siguienteLanzamiento) StartCoroutine(LanzarCascara());
    }

    private void FixedUpdate()
    {
        if (!PuedeActuar() || lanzando) return;

        float velocidad = Mathf.Min(velocidadMaxima, velocidadBase + Dificultad);
        Vector2 siguiente = Vector2.MoveTowards(rb.position, clippy.transform.position, velocidad * Time.fixedDeltaTime);
        rb.MovePosition(ClippyJugador.LimitarAPantalla(siguiente, 0.9f));
    }

    private bool PuedeActuar() =>
        clippy != null && (GestorJuego.Instancia == null || GestorJuego.Instancia.Jugando);

    private IEnumerator LanzarCascara()
    {
        lanzando = true;
        animator.SetTrigger(HashLanzar);
        yield return new WaitForSeconds(momentoLanzamiento);

        if (PuedeActuar() && prefabCascara != null)
        {
            Vector2 origen = (Vector2)transform.position + Vector2.up * 0.6f;
            var cascara = Instantiate(prefabCascara, origen, Quaternion.identity);
            cascara.Lanzar((Vector2)clippy.transform.position - origen, Dificultad * 2f);
        }

        yield return new WaitForSeconds(duracionLanzamiento - momentoLanzamiento);
        lanzando = false;
        ProgramarLanzamiento();
    }

    private void ProgramarLanzamiento() =>
        siguienteLanzamiento = Time.time + Random.Range(esperaMinima, esperaMaxima);

    private void OnTriggerStay2D(Collider2D otro)
    {
        if (!PuedeActuar() || Time.time < siguienteContacto) return;
        var jugador = otro.GetComponentInParent<ClippyJugador>();
        if (jugador == null) return;

        siguienteContacto = Time.time + esperaEntreContactos;
        jugador.Energia.RecibirDano(danoContacto);
    }

    public void Reiniciar(Vector2 posicion)
    {
        StopAllCoroutines();
        lanzando = false;
        rb.position = posicion;
        transform.position = posicion;
        ProgramarLanzamiento();
    }
}
