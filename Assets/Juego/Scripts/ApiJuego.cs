using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Comunicación del videojuego con la REST-API.
///  - Lee el ID del usuario de la URL de la página (…/game/index.html?user_id=5).
///  - GET  {api}/game/{id}            -> { id, nombre, puntos }
///  - PUT  {api}/game/{id}  {puntos}  -> { id, nombre, puntos }
/// </summary>
public class ApiJuego : MonoBehaviour
{
    [Serializable]
    public class DatosJugador
    {
        public int id;
        public string nombre;
        public int puntos;
    }

    [Serializable] private class CuerpoPuntos { public int puntos; }
    [Serializable] private class RespuestaError { public string message; }

    [Tooltip("URL de la REST-API. Se puede cambiar desde la página con ?api=http://...")]
    [SerializeField] private string urlApi = "http://localhost:4000";

#pragma warning disable CS0414 // solo se usa dentro del editor de Unity
    [Tooltip("ID que se usa al probar dentro del editor de Unity (en la web se lee de la URL).")]
    [SerializeField] private int idPruebaEditor = 1;
#pragma warning restore CS0414

    /// <summary>ID del usuario que inició sesión, o -1 si la URL no trae user_id.</summary>
    public int IdUsuario { get; private set; } = -1;
    public string UrlApi => urlApi;

    private void Awake()
    {
#if UNITY_EDITOR
        IdUsuario = idPruebaEditor;
#else
        string url = Application.absoluteURL;
        if (int.TryParse(LeerParametro(url, "user_id"), out int id) && id > 0) IdUsuario = id;
        string api = LeerParametro(url, "api");
        if (!string.IsNullOrEmpty(api)) urlApi = api;
#endif
        urlApi = urlApi.TrimEnd('/');
        Debug.Log($"[ApiJuego] user_id = {IdUsuario}, API = {urlApi}");
    }

    /// <summary>Busca un parámetro en la query string de una URL (?a=1&b=2).</summary>
    public static string LeerParametro(string url, string nombre)
    {
        int inicio = url.IndexOf('?');
        if (inicio < 0) return null;
        string query = url.Substring(inicio + 1);
        int hash = query.IndexOf('#');
        if (hash >= 0) query = query.Substring(0, hash);

        foreach (string par in query.Split('&'))
        {
            string[] partes = par.Split(new[] { '=' }, 2);
            if (partes[0] == nombre) return partes.Length > 1 ? UnityWebRequest.UnEscapeURL(partes[1]) : "";
        }
        return null;
    }

    public IEnumerator ObtenerJugador(Action<DatosJugador> alTerminar, Action<string> alFallar)
    {
        using var peticion = UnityWebRequest.Get($"{urlApi}/game/{IdUsuario}");
        yield return peticion.SendWebRequest();
        Procesar(peticion, alTerminar, alFallar);
    }

    public IEnumerator GuardarPuntos(int puntos, Action<DatosJugador> alTerminar, Action<string> alFallar)
    {
        string cuerpo = JsonUtility.ToJson(new CuerpoPuntos { puntos = puntos });
        using var peticion = UnityWebRequest.Put($"{urlApi}/game/{IdUsuario}", cuerpo);
        peticion.SetRequestHeader("Content-Type", "application/json");
        yield return peticion.SendWebRequest();
        Procesar(peticion, alTerminar, alFallar);
    }

    private static void Procesar(UnityWebRequest peticion, Action<DatosJugador> alTerminar, Action<string> alFallar)
    {
        if (peticion.result == UnityWebRequest.Result.Success)
        {
            alTerminar(JsonUtility.FromJson<DatosJugador>(peticion.downloadHandler.text));
            return;
        }

        if (peticion.result == UnityWebRequest.Result.ConnectionError)
        {
            alFallar("No se pudo conectar con la API. ¿Está corriendo en " + peticion.uri.GetLeftPart(UriPartial.Authority) + "?");
            return;
        }

        string mensaje = null;
        try { mensaje = JsonUtility.FromJson<RespuestaError>(peticion.downloadHandler.text)?.message; }
        catch (ArgumentException) { }
        alFallar(string.IsNullOrEmpty(mensaje) ? $"Error {peticion.responseCode}" : mensaje);
    }
}
