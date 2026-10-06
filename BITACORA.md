# Bitácora del laboratorio

Registro cronológico de lo realizado, las decisiones tomadas y su justificación, y los resultados obtenidos. Sirve de base para la sección de **Desarrollo** del informe.

## Estado

- [x] Guía del laboratorio pasada a Markdown
- [x] Servicio de referencia desplegado y probado
- [x] Estructura del repositorio de entrega
- [x] Cliente Unity de la Parte 1 (sincronización por estado)
- [x] Prueba automática de los experimentos A y B con dos instancias
- [ ] Prueba visual de la Parte 1 en el editor con Multiplayer Play Mode
- [ ] Video de la Parte 1
- [ ] Elegir el escenario de la Parte 2
- [x] Servicio de eventos (`events-service/`)
- [ ] Cliente Unity de la Parte 2
- [ ] Experimento C
- [ ] Pre-laboratorio (marco teórico)
- [ ] Video de la Parte 2
- [ ] Informe
- [x] Primer commit

---

## 2026-10-06

### 1. Guía en Markdown

Se transcribió el PDF de la guía a Markdown para consultarla con más facilidad. Los dos archivos se conservan solo localmente, por ser material del curso.

### 2. Entorno de trabajo

| Herramienta | Versión |
|---|---|
| Docker | 29.6.2 |
| Git | 2.53.0 |
| Python | 3.14.3 |
| Node.js | 24.19.0 |
| .NET SDK | 10.0.103 |
| Unity | 6000.0.82f1 |

### 3. Despliegue del servicio de referencia

Se clonó [DedicatedServer-Api](https://github.com/memin2522/DedicatedServer-Api) y se levantó con `docker compose up --build -d`.

Revisando el código antes de ejecutarlo:

- Es una API en Flask con flask-smorest que guarda las posiciones en un diccionario en memoria.
- Trae configurada una URL de PostgreSQL que nunca se usa, así que no hace falta una base de datos.

Pruebas con curl:

| Prueba | Resultado |
|---|---|
| GET de un jugador que no existe | 404 con cuerpo `{}` |
| POST con `posX`, `posY` y `posZ` | 201 |
| GET después de publicar | 200 con la posición |
| Segundo POST y luego GET | 200, solo con la última posición |
| POST sin `posZ` | 422 |
| POST sin `Content-Type: application/json` | 422 (el servidor no lee el cuerpo) |
| Swagger en `/swagger-ui` | 200 |

Hallazgos:

- **El Content-Type es obligatorio.** Sin él, el servidor responde 422 aunque el JSON esté bien. En Unity, `UnityWebRequest.Post(url, string)` envía el cuerpo como formulario, así que hay que usar `UnityWebRequest.Post(url, json, "application/json")`.
- **Latencia local:** unos 5 ms por petición con curl. Es la referencia para comparar con lo que mide Unity.
- **Sin persistencia:** al reiniciar el contenedor se pierden todas las posiciones. Esto sirve para comparar con la pregunta de análisis 4.

### 4. Estructura del repositorio

Un solo repositorio git (rama `main`, todavía sin commits):

```
laboratorio-servidor-dedicado/
├── README.md              instrucciones de ejecución
├── BITACORA.md            este archivo
├── unity-client/          proyecto Unity 6
└── events-service/        servicio de la Parte 2
```

`DedicatedServer-Api/` está en `.gitignore` porque es un repositorio aparte. También se ignoran `Library/`, `Builds/` y los demás archivos generados por Unity. La guía (el PDF y su transcripción en Markdown) también está en `.gitignore`: se conserva solo localmente, por ser material del curso.

### 5. Cliente Unity de la Parte 1

El proyecto se creó con el editor en modo batch. Las escenas y la configuración se generan desde un script de editor (`Assets/Editor/LabSetup.cs`), así que se pueden regenerar desde el menú **Laboratorio**.

**Componentes** (en el objeto `Red` de la escena `Parte1_Estado`):

| Componente | Responsabilidad |
|---|---|
| `PositionSyncClient` | Publicar y consultar posiciones; representar al jugador remoto |
| `LocalPlayerMover` | Mover el jugador local con WASD o las flechas |
| `LatencyExperiment` | Experimento A |
| `SamplingExperiment` | Experimento B |
| `Part1Controls` | Teclas, argumentos de línea de comandos y panel en pantalla |

**Decisiones y justificación:**

- **Una corrutina por tipo de petición.** Hay un bucle para el POST y otro para el GET, y cada uno espera a que termine su petición antes de lanzar la siguiente. Así se cumple, sin locks, el requisito de tener como máximo una petición en curso por tipo, y el hilo principal nunca se bloquea.
- **Ciclos programados a Δt fijo.** Cada ciclo empieza Δt después del inicio del anterior, no Δt después de recibir la respuesta. Así la carga es 1/Δt sin importar la latencia. Si una petición tarda más que Δt, el siguiente ciclo arranca en cuanto termina y no se acumulan ciclos atrasados.
- **El 404 se decide por el código de estado**, porque no trae un cuerpo útil. El sondeo continúa. Los cambios de estado (esperando, 404, recibiendo, error) se registran en la consola solo cuando ocurren, para no llenarla cada Δt.
- **Medición del ping.** Se usa un `Stopwatch` alrededor de `SendWebRequest`. La corrutina detecta la respuesta en el frame siguiente, así que el ping medido incluye hasta un frame de espera. Para reducir ese error se desactiva VSync y se fijan 240 FPS.
- **Identidad intercambiable.** Las dos instancias arrancan con la misma escena y, por tanto, con los mismos identificadores. Se resolvió con la tecla `Tab` (intercambia local y remoto) y con los argumentos `-playerId`, `-remoteId`, `-gameId`, `-dt` y `-url` para las compilaciones.
- **Colores consistentes.** El identificador menor se pinta azul y el otro naranja, así cada jugador tiene el mismo color en ambas ventanas.
- **Marcas del experimento B.** Las posiciones de la ráfaga van a altura y = 2, que el movimiento normal nunca usa, y un valor previo `posX = 0` marca el inicio. Antes y después de la ráfaga hay una pausa de 1,5 s, mayor que el Δt más grande (1 s), para que el observador alcance a ver el inicio y el último valor.
- **CSV independientes del idioma del sistema.** Se escriben con punto decimal; en la configuración es-CO el decimal sería coma.
- **Player Settings:** *Allow downloads over HTTP* (el servicio usa HTTP sin cifrar), *Run In Background* (la instancia sin foco debe seguir sondeando) y ventana de 960×540 redimensionable (para poner las dos instancias lado a lado).
- **Multiplayer Play Mode 1.6.3**, la versión más reciente compatible con Unity 6.0.

El código compiló sin errores ni advertencias.

### 6. Prueba automática con dos instancias

Se compiló el ejecutable de Windows y se ejecutaron dos instancias sin gráficos (`-batchmode -nographics`) contra el servicio de referencia.

**Experimento B** (la instancia publicadora tenía Δt = 200 ms; se varió el Δt de la observadora):

| Δt del observador | Valores distintos vistos | Cuáles |
|---|---|---|
| 50 ms | 8 de 20 | 2 5 7 10 12 15 17 20 |
| 200 ms | 3 de 20 | 6 16 20 |
| 1000 ms | 1 de 20 | 20 |

La ráfaga se publicó con una separación media de 20,1 ms (20 valores en unos 381 ms), frente a los 20 ms pedidos.

**Experimento A** (ambas instancias a la vez, 100 muestras por Δt):

| Δt | Ping promedio p1 / p2 | p95 | Peticiones/s por instancia | Sistema completo |
|---|---|---|---|---|
| 50 ms | 10,5 / 10,9 ms | ~16,9 ms | 39,9 | ~80 |
| 200 ms | 9,9 / 9,6 ms | ~16,8 ms | 10,0 | ~20 |
| 1000 ms | 10,0 / 9,8 ms | ~17,0 ms | 2,0 | ~4 |

Observaciones:

- La carga coincide con la teoría: 2 instancias × 2 peticiones / Δt.
- **El ping no depende de Δt.** El servidor no está saturado ni siquiera con 80 peticiones/s.
- **El ping medido en Unity es mayor que el de curl** (unos 8 ms de mediana, frente a 5 ms) y tiene dos valores frecuentes, cerca de 8 ms y cerca de 17 ms. Hipótesis por verificar: la granularidad de frames y del temporizador de Windows (unos 15,6 ms) se suma a la medición. Es un buen punto para el análisis.
- El 404 inicial se manejó bien: la instancia que arrancó primero registró el 404 y siguió sondeando hasta recibir la posición.

Los CSV de esta prueba quedaron en `%USERPROFILE%\AppData\LocalLow\Laboratorio\ServidorDedicado\Resultados`. Se pueden conservar como referencia o borrar antes de las mediciones oficiales.

### 7. Servicio de eventos (Parte 2)

Se implementó en `events-service/` con **FastAPI** (Python 3.13) dentro de Docker, publicado en el puerto **5006** para no chocar con el servicio de referencia (5005). Versiones: FastAPI 0.142.2 y Uvicorn 0.54.0.

| Archivo | Contenido |
|---|---|
| `app/models.py` | Modelos de entrada y salida (Pydantic) |
| `app/store.py` | `EventStore`: almacenamiento en memoria, `seq` y detección de reenvíos |
| `app/main.py` | Las dos rutas |
| `tests/` | 15 pruebas con pytest |

**Decisiones y justificación:**

- **FastAPI.** Valida el cuerpo automáticamente (responde 422 si falta un campo, igual que el servicio de referencia), genera la documentación interactiva en `/docs` y necesita poco código.
- **`seq` = posición en la lista + 1.** Empieza en 1, aumenta de a uno y nunca se reutiliza, así que "los eventos posteriores a `since`" son `events[since:]`, sin recorrer la lista.
- **Lock alrededor de asignar el `seq` y guardar el evento.** FastAPI ejecuta las rutas en varios hilos; sin el lock, dos POST simultáneos podrían recibir el mismo `seq`. El GET lee bajo el mismo lock, así que `last_seq` siempre coincide con la lista devuelta.
- **Un solo proceso de Uvicorn.** Con varios procesos, cada uno tendría su propia lista en memoria y su propio contador.
- **`event_id` opcional para los reenvíos** (responde la pregunta de análisis 3). Si un cliente no recibe respuesta y reenvía el evento con el mismo `event_id`, el servidor no lo registra dos veces: responde 200 con el `seq` original y `duplicate: true`. Es opcional para no salirse del contrato mínimo de la guía.
- **Una partida sin eventos responde 200 con `events: []` y `last_seq: 0`,** no 404. Para un cliente que sondea, "todavía no hay eventos" no es un error y así no necesita un caso especial (a diferencia del 404 del servicio de referencia).
- **`since` debe ser mayor o igual a 0** (si no, 422). Si es mayor que `last_seq`, devuelve una lista vacía.
- **`timestamp` en UTC con formato ISO 8601 y milisegundos,** por ejemplo `2026-10-06T21:45:00.145Z`.

**Pruebas automáticas (15, todas pasan):** respuesta 201 con `seq` y `timestamp`; `seq` independiente por partida; 422 por cada campo faltante; filtro `since`; partida inexistente; `since` mayor que `last_seq`; `since` negativo; reenvío con el mismo `event_id`; mismo `event_id` en otra partida; simulación del sondeo con varios eventos entre consultas. Además, dos pruebas de concurrencia: 50 hilos registran 1000 eventos y obtienen exactamente los `seq` 1 a 1000, y 30 hilos reenvían el mismo `event_id` y queda registrado una sola vez.

**Prueba contra el contenedor con curl:** 201 al registrar, 200 con `duplicate: true` al reenviar, 422 sin `type`, filtro `since` correcto y `/docs` disponible.

**Simulación del experimento C con tiempos reales** (script de Python: un hilo publica 20 eventos, uno cada 20 ms, y otro sondea cada Δt con `since`):

| Δt | Eventos recibidos | En orden | Repetidos | Consultas |
|---|---|---|---|---|
| 50 ms | 20 de 20 | sí | 0 | 9 |
| 200 ms | 20 de 20 | sí | 0 | 3 |
| 1000 ms | 20 de 20 | sí | 0 | 2 |

En el experimento B, con los mismos Δt, solo se vieron 8, 3 y 1 valores. Con eventos, aumentar Δt no hace perder información: solo retrasa su llegada y la agrupa en menos consultas, cada una con más eventos. Es el contraste central de la pregunta de análisis 2. Esta simulación solo verifica el servicio; el experimento C oficial debe hacerse con Unity.

**Pendiente para el análisis (pregunta 4):** como los eventos están en memoria, al reiniciar el servicio el `seq` vuelve a 1. Un cliente que ya iba en `since = 15` no vería los eventos nuevos hasta que la partida pasara de 15. Posibles soluciones: persistir los eventos en disco o en una base de datos; incluir un identificador de arranque del servidor para que el cliente detecte el reinicio y vuelva a `since = 0`; y, contra el crecimiento sin límite, conservar solo una ventana de eventos recientes o compactarlos en un estado.

### Próximos pasos

1. Probar la Parte 1 en el editor con Multiplayer Play Mode y grabar el video.
2. Elegir el escenario: Arena, Cocina cooperativa, Sala de palancas o Carreras.
3. Cliente Unity de la Parte 2. Cada evento se enviará con un `event_id` (GUID), y cada evento recibido se ejecutará solo si su `seq` es mayor que el último procesado, para que se ejecute una sola vez.
