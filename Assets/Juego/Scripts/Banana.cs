using UnityEngine;

/// <summary>Banana de Bonzi. Si Clippy la toca, suma puntos y desaparece.</summary>
[RequireComponent(typeof(Collider2D))]
public class Banana : MonoBehaviour
{
    [SerializeField] private int puntos = 10;
    [SerializeField] private float amplitud = 0.12f;
    [SerializeField] private float frecuencia = 2.5f;

    private Vector3 posicionBase;
    private float desfase;

    private void Start()
    {
        posicionBase = transform.position;
        desfase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        transform.position = posicionBase + Vector3.up * Mathf.Sin(Time.time * frecuencia + desfase) * amplitud;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (otro.GetComponentInParent<ClippyJugador>() == null || GestorJuego.Instancia == null) return;
        if (!GestorJuego.Instancia.Jugando) return;

        GestorJuego.Instancia.SumarPuntos(puntos);
        Destroy(gameObject);
    }
}
