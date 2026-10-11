import threading

from app.models import EventIn
from app.store import EventStore


def run_concurrently(thread_count: int, work) -> None:
    barrier = threading.Barrier(thread_count)

    def worker():
        barrier.wait()  # todos arrancan a la vez para forzar la competencia
        work()

    pool = [threading.Thread(target=worker) for _ in range(thread_count)]
    for thread in pool:
        thread.start()
    for thread in pool:
        thread.join()


def test_concurrent_appends_get_unique_consecutive_seq():
    store = EventStore()
    seqs = []

    def work():
        for _ in range(20):
            event, _ = store.append("g", EventIn(player_id="p", type="t", payload={}))
            seqs.append(event.seq)

    run_concurrently(50, work)

    assert sorted(seqs) == list(range(1, 1001))


def test_concurrent_resends_of_one_event_id_register_it_once():
    store = EventStore()
    results = []

    def work():
        results.append(store.append("g", EventIn(player_id="p", type="t", payload={}, event_id="same")))

    run_concurrently(30, work)

    assert {event.seq for event, _ in results} == {1}
    assert sum(not duplicate for _, duplicate in results) == 1
    assert store.since("g", 0)[1] == 1
