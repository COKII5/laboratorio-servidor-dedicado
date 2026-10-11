import pytest
from fastapi.testclient import TestClient

from app import main
from app.store import EventStore


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setattr(main, "store", EventStore())
    return TestClient(main.app)


def post(client, game="g1", **overrides):
    body = {"player_id": "p1", "type": "ProjectileFired", "payload": {"x": 1.0}} | overrides
    return client.post(f"/games/{game}/events", json=body)


def test_post_returns_201_with_seq_and_timestamp(client):
    response = post(client)

    assert response.status_code == 201
    body = response.json()
    assert body["seq"] == 1
    assert body["duplicate"] is False
    assert body["timestamp"].endswith("Z")


def test_seq_increases_by_one_per_game(client):
    assert [post(client).json()["seq"] for _ in range(3)] == [1, 2, 3]
    assert post(client, game="g2").json()["seq"] == 1


@pytest.mark.parametrize("missing", ["player_id", "type", "payload"])
def test_post_with_missing_field_is_422(client, missing):
    body = {"player_id": "p1", "type": "ProjectileFired", "payload": {}}
    del body[missing]

    assert client.post("/games/g1/events", json=body).status_code == 422


def test_get_returns_events_after_since_in_order(client):
    for i in range(5):
        post(client, type=f"E{i}")

    body = client.get("/games/g1/events", params={"since": 2}).json()

    assert [event["seq"] for event in body["events"]] == [3, 4, 5]
    assert [event["type"] for event in body["events"]] == ["E2", "E3", "E4"]
    assert body["last_seq"] == 5


def test_get_without_since_returns_every_event(client):
    post(client)
    post(client)

    assert [event["seq"] for event in client.get("/games/g1/events").json()["events"]] == [1, 2]


def test_get_unknown_game_is_empty_not_404(client):
    response = client.get("/games/nueva/events")

    assert response.status_code == 200
    assert response.json() == {"events": [], "last_seq": 0}


def test_get_since_past_last_seq_is_empty(client):
    post(client)

    assert client.get("/games/g1/events", params={"since": 10}).json() == {"events": [], "last_seq": 1}


def test_get_with_negative_since_is_422(client):
    assert client.get("/games/g1/events", params={"since": -1}).status_code == 422


def test_resent_event_id_is_registered_once(client):
    first = post(client, event_id="abc")
    resent = post(client, event_id="abc")

    assert first.status_code == 201
    assert resent.status_code == 200
    assert resent.json()["seq"] == first.json()["seq"]
    assert resent.json()["duplicate"] is True
    assert client.get("/games/g1/events").json()["last_seq"] == 1


def test_same_event_id_in_another_game_is_a_new_event(client):
    post(client, event_id="abc")

    assert post(client, game="g2", event_id="abc").status_code == 201


def test_polling_with_since_receives_every_event_once_in_order(client):
    received, since = [], 0
    for published_between_polls in [3, 0, 5, 1, 11]:
        for _ in range(published_between_polls):
            post(client)
        body = client.get("/games/g1/events", params={"since": since}).json()
        received += [event["seq"] for event in body["events"]]
        since = body["last_seq"]

    assert received == list(range(1, 21))
