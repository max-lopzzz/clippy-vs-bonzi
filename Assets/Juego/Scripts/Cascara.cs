using UnityEngine;

/// <summary>Cáscara de plátano que lanza Bonzi. Vuela en línea recta y daña a Clippy.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Cascara : MonoBehaviour
{
    [SerializeField] private float velocidad = 4.5f;
    [SerializeField] private float dano = 15f;
    [SerializeField] private float giro = 540f;
    [SerializeField] private float vida = 5f;

    private Rigidbody2D rb;
    private Vector2 direccion;

    public void Lanzar(Vector2 haciaDonde, float velocidadExtra)
    {
        rb = GetComponent<Rigidbody2D>();
        direccion = haciaDonde.normalized;
        velocidad += velocidadExtra;
        Destroy(gameObject, vida);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        rb.MovePosition(rb.position + direccion * velocidad * Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation + giro * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        var clippy = otro.GetComponentInParent<ClippyJugador>();
        if (clippy == null) return;

        clippy.Energia.RecibirDano(dano);
        Destroy(gameObject);
    }
}
