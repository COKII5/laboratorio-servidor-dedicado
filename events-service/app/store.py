import threading
from dataclasses import dataclass, field
from datetime import datetime, timezone

from .models import Event, EventIn


@dataclass
class _Game:
    events: list[Event] = field(default_factory=list)
    seq_by_event_id: dict[str, int] = field(default_factory=dict)


class EventStore:
    """
    Registro de eventos en memoria, separado por partida.

    El seq de un evento es su posición en la lista más uno: empieza en 1, aumenta de a
    uno y nunca se reutiliza. Por eso "los eventos posteriores a since" son simplemente
    events[since:].

    FastAPI ejecuta las rutas síncronas en varios hilos, así que asignar el seq y agregar
    el evento ocurre bajo un mismo lock: dos POST simultáneos nunca reciben el mismo seq.
    """

    def __init__(self) -> None:
        self._games: dict[str, _Game] = {}
        self._lock = threading.Lock()

    def append(self, game_id: str, data: EventIn) -> tuple[Event, bool]:
        """Registra el evento y lo devuelve. El booleano indica si era un reenvío ya registrado."""
        with self._lock:
            game = self._games.setdefault(game_id, _Game())

            if data.event_id is not None and data.event_id in game.seq_by_event_id:
                return game.events[game.seq_by_event_id[data.event_id] - 1], True

            event = Event(
                seq=len(game.events) + 1,
                player_id=data.player_id,
                type=data.type,
                payload=data.payload,
                timestamp=datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z"),
                event_id=data.event_id,
            )
            game.events.append(event)
            if data.event_id is not None:
                game.seq_by_event_id[data.event_id] = event.seq
            return event, False

    def since(self, game_id: str, since: int) -> tuple[list[Event], int]:
        """Eventos con seq > since, en orden, y el último seq registrado."""
        with self._lock:
            game = self._games.get(game_id)
            if game is None:
                return [], 0
            return game.events[since:], len(game.events)
