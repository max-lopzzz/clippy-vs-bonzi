using System;
using UnityEngine;

/// <summary>
/// Energía (vida) del jugador. El enemigo la lee para decidir su comportamiento
/// y la reduce con RecibirDano().
/// Si tu jugador ya tiene su propia variable de energía, puedes llamar a
/// RecibirDano() desde aquí o adaptar el enemigo a tu script.
/// </summary>
public class PlayerEnergy : MonoBehaviour
{
    [Header("Energía")]
    [SerializeField] private float energiaMaxima = 100f;
    [SerializeField] private float energiaActual = 100f;

    [Header("Feedback de daño")]
    [SerializeField] private float tiempoParpadeo = 0.15f;

    [Header("Debug")]
    [Tooltip("Muestra una barra de energía en pantalla (OnGUI).")]
    [SerializeField] private bool mostrarBarra = true;

    /// <summary>Se dispara cada vez que cambia la energía: (actual, máxima).</summary>
    public event Action<float, float> OnEnergiaCambio;

    public float EnergiaActual => energiaActual;
    public float EnergiaMaxima => energiaMaxima;
    /// <summary>Energía entre 0 y 1.</summary>
    public float Porcentaje => energiaMaxima > 0f ? energiaActual / energiaMaxima : 0f;
    public bool EstaMuerto => energiaActual <= 0f;

    private SpriteRenderer spriteRenderer;
    private Color colorOriginal;
    private float finParpadeo;

    private void Awake()
    {
        energiaActual = Mathf.Clamp(energiaActual, 0f, energiaMaxima);
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) colorOriginal = spriteRenderer.color;
    }

    private void Update()
    {
        if (spriteRenderer != null && finParpadeo > 0f && Time.time >= finParpadeo)
        {
            spriteRenderer.color = colorOriginal;
            finParpadeo = 0f;
        }
    }

    public void RecibirDano(float cantidad)
    {
        if (cantidad <= 0f || EstaMuerto) return;

        energiaActual = Mathf.Max(0f, energiaActual - cantidad);
        Debug.Log($"[Jugador] Daño recibido: {cantidad}. Energía: {energiaActual}/{energiaMaxima} ({Porcentaje:P0})");

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            finParpadeo = Time.time + tiempoParpadeo;
        }

        OnEnergiaCambio?.Invoke(energiaActual, energiaMaxima);
    }

    public void RecuperarEnergia(float cantidad)
    {
        if (cantidad <= 0f) return;
        energiaActual = Mathf.Min(energiaMaxima, energiaActual + cantidad);
        OnEnergiaCambio?.Invoke(energiaActual, energiaMaxima);
    }

    private void OnValidate()
    {
        energiaMaxima = Mathf.Max(1f, energiaMaxima);
        energiaActual = Mathf.Clamp(energiaActual, 0f, energiaMaxima);
    }

    private void OnGUI()
    {
        if (!mostrarBarra) return;

        const float ancho = 220f, alto = 22f;
        var fondo = new Rect(20f, 20f, ancho, alto);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(fondo, Texture2D.whiteTexture);
        GUI.color = Porcentaje > 0.5f ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.95f, 0.35f, 0.25f);
        GUI.DrawTexture(new Rect(fondo.x + 2f, fondo.y + 2f, (ancho - 4f) * Porcentaje, alto - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(fondo.x + 6f, fondo.y + 2f, ancho, alto), $"Energía: {energiaActual:0}/{energiaMaxima:0}");
    }
}
