using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Clippy, el héroe. Se mueve con WASD / flechas: en bicicleta cuando avanza
/// y con su animación de reposo cuando está quieto. No puede salir de la pantalla.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerEnergy), typeof(Animator))]
public class ClippyJugador : MonoBehaviour
{
    public const string ParametroMoviendo = "Moviendo";
    private static readonly int HashMoviendo = Animator.StringToHash(ParametroMoviendo);

    [SerializeField] private float velocidad = 5.5f;
    [Tooltip("Margen para que Clippy no quede medio fuera de la pantalla.")]
    [SerializeField] private float margen = 0.7f;
    [Tooltip("Las hojas de Clippy en bicicleta miran a la izquierda.")]
    [SerializeField] private bool spriteMiraIzquierda = true;

    private Rigidbody2D rb;
    private PlayerEnergy energia;
    private Animator animator;
    private SpriteRenderer sprite;
    private Vector2 direccion;

    public PlayerEnergy Energia => energia;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        energia = GetComponent<PlayerEnergy>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        var teclado = Keyboard.current;
        bool puedeMoverse = GestorJuego.Instancia == null || GestorJuego.Instancia.Jugando;
        if (teclado == null || !puedeMoverse) { direccion = Vector2.zero; }
        else
        {
            float x = (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed ? 1f : 0f)
                    - (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed ? 1f : 0f);
            float y = (teclado.wKey.isPressed || teclado.upArrowKey.isPressed ? 1f : 0f)
                    - (teclado.sKey.isPressed || teclado.downArrowKey.isPressed ? 1f : 0f);
            direccion = new Vector2(x, y).normalized;
        }

        animator.SetBool(HashMoviendo, direccion != Vector2.zero);
        if (direccion.x != 0f && sprite != null) sprite.flipX = spriteMiraIzquierda ? direccion.x > 0f : direccion.x < 0f;
    }

    private void FixedUpdate()
    {
        Vector2 siguiente = rb.position + direccion * velocidad * Time.fixedDeltaTime;
        rb.MovePosition(LimitarAPantalla(siguiente, margen));
    }

    /// <summary>Recorta una posición para que quede dentro de lo que ve la cámara.</summary>
    public static Vector2 LimitarAPantalla(Vector2 posicion, float margen)
    {
        var cam = Camera.main;
        if (cam == null) return posicion;
        float alto = cam.orthographicSize, ancho = alto * cam.aspect;
        Vector2 centro = cam.transform.position;
        return new Vector2(
            Mathf.Clamp(posicion.x, centro.x - ancho + margen, centro.x + ancho - margen),
            Mathf.Clamp(posicion.y, centro.y - alto + margen, centro.y + alto - margen - 1.4f)); // deja libre la barra del HUD
    }

    public void Reiniciar(Vector2 posicion)
    {
        rb.position = posicion;
        transform.position = posicion;
        direccion = Vector2.zero;
    }
}
