# Laboratorio: Servidor Dedicado

Sincronización de un juego en Unity a través de un servicio REST, primero replicando **estado** (posiciones) y luego propagando **eventos**. La guía la entrega el docente y no se incluye en el repositorio. El registro del trabajo realizado está en [BITACORA.md](BITACORA.md).

| Carpeta | Contenido |
|---|---|
| `unity-client/` | Proyecto Unity 6 (6000.0.82f1): cliente de la Parte 1 y, más adelante, de la Parte 2 |
| `events-service/` | Servicio de eventos de la Parte 2 (FastAPI + Docker) |

## Requisitos

- Docker Desktop
- Unity 6000.0.82f1 con soporte para compilar en Windows
- Python 3.12 o superior, solo para correr las pruebas del servicio de eventos fuera de Docker

## 1. Levantar el servicio de referencia

Se clona fuera de este repositorio (está en `.gitignore`):

```bash
git clone https://github.com/memin2522/DedicatedServer-Api.git
cd DedicatedServer-Api
docker compose up --build -d
```

Queda en `http://localhost:5005`, con documentación en `http://localhost:5005/swagger-ui`. Se detiene con `docker compose down`. Las posiciones se guardan solo en memoria y se pierden al reiniciar el contenedor.

## 2. Cliente Unity (Parte 1)

Abrir `unity-client/` desde Unity Hub y luego la escena `Assets/Scenes/Parte1_Estado.unity`.

La escena tiene un objeto `Red` con estos componentes:

| Componente | Función |
|---|---|
| `PositionSyncClient` | Publica la posición local y consulta la remota cada Δt, con dos corrutinas independientes. Cada una espera a que termine su petición antes de lanzar la siguiente, así que nunca hay dos peticiones del mismo tipo en curso. El 404 inicial y los errores de red se registran sin detener el sondeo. |
| `LatencyExperiment` | Experimento A |
| `SamplingExperiment` | Experimento B |
| `Part1Controls` | Atajos de teclado y panel en pantalla |

### Organización de los scripts

```
Assets/Scripts/
  Common/    Código compartido por las dos partes
    Network/   PollingClient: configuración, argumentos de línea de comandos, bucle cada Δt, GET y POST
    UI/        SyncClientControls (teclas y panel comunes), InfoPanel, RequestRateMeter
    Gameplay/  LocalPlayerMover, PlayerColors
    Utils/     CommandLineArgs, ResultsWriter, RealtimeClock
  Part1/     Network/ (PositionSyncClient, PositionDto) · Experiments/ (A y B) · UI/ (Part1Controls)
  Part2/     Network/ (EventSyncClient, DTO) · Race/ (carrera y eventos) · Experiments/ (C) · UI/ (Part2Controls)
Assets/Editor/LabSetup.cs   Menú Laboratorio: genera las escenas y compila
```

`PositionSyncClient` y `EventSyncClient` heredan de `PollingClient`; `Part1Controls` y `Part2Controls`, de `SyncClientControls`. El JSON se lee y se escribe con Newtonsoft Json (paquete `com.unity.nuget.newtonsoft-json`), que permite nombres de C# en PascalCase y nombres JSON fijados por el servicio (`posX`, `player_id`).

Los identificadores de partida y de jugador, la URL del servicio y Δt se configuran en el Inspector de `PositionSyncClient`. El proyecto ya trae activadas *Allow downloads over HTTP* y *Run In Background*. Si hiciera falta, el menú **Laboratorio → Configurar proyecto y regenerar escena Parte 1** las vuelve a aplicar y genera la escena de nuevo.

### Ejecutar dos instancias

Las dos instancias arrancan con la misma escena, así que ambas empiezan como `p1`. **En una de ellas se pulsa `Tab`** para que pase a ser `p2`. Cada jugador mantiene el mismo color en las dos ventanas: `p1` azul y `p2` naranja.

- **Multiplayer Play Mode** (ya instalado): *Window → Multiplayer → Multiplayer Play Mode*, activar un *Player 2* y dar Play.
- **Compilación + editor**: menú **Laboratorio → Compilar para Windows** (genera `unity-client/Builds/Windows/ServidorDedicado.exe`). La compilación acepta argumentos, así que no hace falta pulsar `Tab`:

  ```bash
  ServidorDedicado.exe -playerId p2 -remoteId p1 -gameId partida1 -dt 200
  ```

  Otros argumentos: `-url http://...` y `-experiment latency|burst` (inicia un experimento 2 s después de arrancar). Si la URL no es válida, la partida está vacía o `-dt` no es un entero positivo, se avisa en la consola y se usa el valor por defecto.

### Teclas

| Tecla | Acción |
|---|---|
| WASD / flechas | Mover el jugador local |
| `1` `2` `3` | Δt = 50 / 200 / 1000 ms |
| `Tab` | Intercambiar jugador local y remoto |
| `I` | Activar o desactivar la interpolación del jugador remoto |
| `L` | Iniciar o detener el experimento A |
| `B` | Publicar la ráfaga del experimento B |
| `H` | Mostrar u ocultar el panel |

## 3. Experimentos

Los resultados se guardan como CSV (coma como separador, punto decimal) en `%USERPROFILE%\AppData\LocalLow\Laboratorio\ServidorDedicado\Resultados`. El panel en pantalla muestra la ruta exacta. En Excel con configuración en español se abren con *Datos → Desde texto/CSV*.

### Experimento A: latencia y carga

Pulsar `L` **en las dos instancias**. Cada una recorre Δt = 50, 200 y 1000 ms, toma 100 tiempos de ida y vuelta del GET por intervalo y vuelve a su Δt original. Dura unos 2 minutos y medio.

- `expA_resumen_<jugador>.csv`: promedio, desviación, mínimo, mediana, p95 y máximo del ping, más GET/s, POST/s y total por segundo **de esa instancia**. La carga del sistema completo es la suma de las dos instancias (en teoría, 2 instancias × 2 peticiones / Δt).
- `expA_muestras_<jugador>.csv`: las 100 muestras de cada Δt, útiles para graficar.

> El ping se mide desde Unity: la corrutina nota que la respuesta llegó en el frame siguiente, así que la medición incluye hasta un frame de espera. Por eso el cliente desactiva VSync y fija 240 FPS (`targetFrameRate` en el Inspector). Vale la pena mencionarlo en el análisis.

### Experimento B: muestreo

1. En la instancia **observadora**, elegir Δt con `1`, `2` o `3`.
2. En la **otra** instancia, pulsar `B`. Esa instancia publica `posX = 0` como marca de inicio, espera 1,5 s y luego publica `posX = 1…20`, uno cada 20 ms. Después mantiene el último valor 1,5 s y vuelve a publicar su posición normal.
3. La observadora (siempre está atenta) cuenta cuántos valores distintos vio y lo muestra en el panel.

Se repite para cada Δt.

- `expB_observador_<jugador>.csv`: Δt, valores distintos vistos y cuáles fueron.
- `expB_publicador_<jugador>.csv`: separación real entre publicaciones, para comprobar que se cumplieron los 20 ms.

Las posiciones de la ráfaga van a altura y = 2, que el movimiento normal nunca usa; así se distinguen. Durante la ráfaga, el cubo remoto se ve saltar en el aire a lo largo del eje x.

## 4. Servicio de eventos (Parte 2)

FastAPI (Python) en un contenedor:

```bash
cd events-service
docker compose up --build -d
```

Queda en `http://localhost:5006`, con documentación interactiva en `http://localhost:5006/docs`. Los eventos se guardan en memoria.

| Método | Ruta | Respuesta |
|---|---|---|
| POST | `/games/{game_id}/events` | Cuerpo `{player_id, type, payload, event_id?}`. Responde **201** con `{seq, timestamp, duplicate}`, o **422** si falta un campo. |
| GET | `/games/{game_id}/events?since={seq}` | **200** con `{events: [...], last_seq}`: los eventos con `seq > since`, en orden. Una partida sin eventos devuelve `events: []` y `last_seq: 0`, no un 404. |

- `seq` empieza en 1 en cada partida y aumenta de uno en uno. Se asigna bajo un lock, así que dos POST simultáneos nunca reciben el mismo número.
- `event_id` es opcional. Lo genera el cliente (por ejemplo, un GUID). Si un evento se reenvía con el mismo `event_id`, no se registra dos veces: el servidor responde **200** con el `seq` original y `duplicate: true`.
- El cliente guarda el último `seq` recibido y lo envía como `since` en la siguiente consulta.

Pruebas automáticas:

```bash
cd events-service
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements-dev.txt   # en Linux/macOS: .venv/bin/python
.venv/Scripts/python -m pytest
```
