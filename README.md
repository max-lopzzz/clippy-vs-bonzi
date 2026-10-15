# Clippy vs BonziBuddy (Unity)

Videojuego 2D hecho en Unity 6 (6000.5.8f1) que se conecta a una REST-API.

- **Clippy** (jugador) junta las bananas de **BonziBuddy** (+10 cada una) y esquiva sus cáscaras.
- Al iniciar, el juego lee `user_id` de la URL y trae el nombre y los puntos con `GET /game/:id`.
- **Guardar puntaje** (botón o tecla G) guarda los puntos con `PUT /game/:id` y body `{puntos}`.
- Si Bonzi deja a Clippy sin energía se pierden los puntos que no se guardaron.

## Proyectos relacionados

- REST-API: https://github.com/max-lopzzz/practica-rest-api
- Página web (incluye el juego compilado en `game/`): https://github.com/max-lopzzz/Actividad---Pr-ctica-con-HTML

## Estructura

- `Assets/Juego/Scripts`: `ApiJuego` (API y URL), `GestorJuego` (puntos, guardado, HUD), `ClippyJugador`, `BonziEnemigo`, `Banana`, `Cascara`.
- `Assets/Juego/Editor/ConstructorJuego.cs`: menú **Herramientas > Clippy vs Bonzi** para construir la escena y compilar WebGL.
- `Assets/WebGLTemplates/ClippyVsBonzi`: plantilla web que ajusta el juego al iframe.
- `Assets/Enemigo`: enemigo Clippy de la práctica anterior (sus sprites se reutilizan para el jugador).

## Compilar

En Unity: **Herramientas > Clippy vs Bonzi > 1. Construir juego** y luego **2. Compilar para WebGL**
(guarda el resultado en `../frontend-html/game`). Desde terminal:

```bash
Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod ConstructorJuego.CompilarDesdeTerminal -salida ruta/al/proyecto-web/game
```
