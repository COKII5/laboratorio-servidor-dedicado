from fastapi import FastAPI, Query, Response
from fastapi.responses import RedirectResponse

from .models import EventIn, EventsPage, PostResult
from .store import EventStore

app = FastAPI(
    title="Servicio de eventos",
    version="1.0.0",
    description="Parte 2 del laboratorio Servidor Dedicado: guarda todos los eventos de cada partida "
    "en orden y permite pedir solo los que aún no se han recibido.",
)
store = EventStore()


@app.post(
    "/games/{game_id}/events",
    status_code=201,
    response_model=PostResult,
    responses={200: {"model": PostResult, "description": "El event_id ya estaba registrado; se devuelve su seq original"}},
)
def post_event(game_id: str, incoming: EventIn, response: Response) -> PostResult:
    event, duplicate = store.append(game_id, incoming)
    if duplicate:
        response.status_code = 200
    return PostResult(seq=event.seq, timestamp=event.timestamp, duplicate=duplicate)


@app.get("/games/{game_id}/events", response_model=EventsPage, response_model_exclude_none=True)
def get_events(game_id: str, since: int = Query(0, ge=0, description="Último seq que el cliente ya recibió")) -> EventsPage:
    """Una partida sin eventos no es un error: responde una lista vacía y last_seq = 0."""
    events, last_seq = store.since(game_id, since)
    return EventsPage(events=events, last_seq=last_seq)


@app.get("/", include_in_schema=False)
def root() -> RedirectResponse:
    return RedirectResponse("/docs")
