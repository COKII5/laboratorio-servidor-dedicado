from typing import Any

from pydantic import BaseModel, Field


class EventIn(BaseModel):
    """Evento que envía un cliente."""

    player_id: str = Field(min_length=1, examples=["p1"])
    type: str = Field(min_length=1, description="Nombre del evento", examples=["ProjectileFired"])
    payload: dict[str, Any] = Field(description="Datos adicionales del evento", examples=[{"x": 1.5, "y": 0.5, "z": -2.0}])
    event_id: str | None = Field(
        default=None,
        min_length=1,
        description="Opcional. Identificador generado por el cliente; si se reenvía el mismo, "
        "el servidor no lo registra dos veces.",
    )


class Event(BaseModel):
    """Evento registrado, con el orden y la hora que asigna el servidor."""

    seq: int
    player_id: str
    type: str
    payload: dict[str, Any]
    timestamp: str = Field(description="Hora de registro en UTC (ISO 8601)")
    event_id: str | None = None


class PostResult(BaseModel):
    seq: int
    timestamp: str
    duplicate: bool = Field(description="true si el event_id ya estaba registrado")


class EventsPage(BaseModel):
    events: list[Event]
    last_seq: int = Field(description="Último número de orden registrado en la partida (0 si no hay eventos)")
