using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimiento sencillo para probar el enemigo (WASD / flechas).
/// Tecla R: recupera energía para comprobar que el enemigo vuelve a sus acciones.
/// Sustitúyelo por el jugador real del juego cuando lo integres.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerEnergy))]
public class JugadorPrueba : MonoBehaviour
{
    [SerializeField] private float velocidad = 5f;
    [SerializeField] private float energiaAlRecuperar = 25f;

    private Rigidbody2D rb;
    private PlayerEnergy energia;
    private Vector2 direccion;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        energia = GetComponent<PlayerEnergy>();
    }

    private void Update()
    {
        var teclado = Keyboard.current;
        if (teclado == null) { direccion = Vector2.zero; return; }

        float x = (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed ? 1f : 0f)
                - (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed ? 1f : 0f);
        float y = (teclado.wKey.isPressed || teclado.upArrowKey.isPressed ? 1f : 0f)
                - (teclado.sKey.isPressed || teclado.downArrowKey.isPressed ? 1f : 0f);
        direccion = new Vector2(x, y).normalized;

        if (teclado.rKey.wasPressedThisFrame) energia.RecuperarEnergia(energiaAlRecuperar);
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = energia.EstaMuerto ? Vector2.zero : direccion * velocidad;
    }
}
