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
    Eventos en memoria, separados por partida. El seq es la posición en la lista más uno,
    así que los eventos posteriores a since son events[since:].

    FastAPI atiende las rutas síncronas en varios hilos: asignar el seq y agregar el evento
    ocurre bajo un mismo lock para que dos POST simultáneos nunca reciban el mismo seq.
    """

    def __init__(self) -> None:
        self._games: dict[str, _Game] = {}
        self._lock = threading.Lock()

    def append(self, game_id: str, incoming: EventIn) -> tuple[Event, bool]:
        """El booleano indica si era un reenvío de un event_id ya registrado."""
        with self._lock:
            game = self._games.setdefault(game_id, _Game())

            if incoming.event_id is not None and incoming.event_id in game.seq_by_event_id:
                return game.events[game.seq_by_event_id[incoming.event_id] - 1], True

            event = Event(
                seq=len(game.events) + 1,
                player_id=incoming.player_id,
                type=incoming.type,
                payload=incoming.payload,
                timestamp=datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z"),
                event_id=incoming.event_id,
            )
            game.events.append(event)
            if incoming.event_id is not None:
                game.seq_by_event_id[incoming.event_id] = event.seq
            return event, False

    def since(self, game_id: str, since: int) -> tuple[list[Event], int]:
        with self._lock:
            game = self._games.get(game_id)
            if game is None:
                return [], 0
            return game.events[since:], len(game.events)
