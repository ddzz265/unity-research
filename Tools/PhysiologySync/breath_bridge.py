"""Forward real belt samples from a local vendor adapter to the Quest recorder.

No simulator or belt driver is included. Input: one JSON object per UDP packet
on 127.0.0.1:5005; see the accompanying Chinese instructions.
"""
import argparse
import json
import math
from pathlib import Path
import re
import select
import socket
import time
import uuid


def finite_number(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError("Expected a numeric sample")
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("Non-finite sample")
    return value


def parse_sample(data, received):
    sample = json.loads(data)
    if not isinstance(sample, dict) or sample.get("kind") != "respiration":
        raise ValueError("kind must be respiration; heart-rate data is not respiration")
    value = finite_number(sample["value"])
    unit = sample.get("unit", "raw")
    if not isinstance(unit, str) or not re.fullmatch(r"[A-Za-z0-9_.:/-]{1,32}", unit):
        raise ValueError("Invalid unit")
    device = sample.get("device_seconds")
    if device is not None:
        device = finite_number(device)
    # Without a proven acquisition-clock mapping, timestamp at PC receipt.
    pc_time, basis = received, "pc_receive"
    if "pc_sample_seconds" in sample:
        pc_time = finite_number(sample["pc_sample_seconds"])
        if pc_time < 0 or pc_time > received + 0.1:
            raise ValueError("pc_sample_seconds must use this PC's time.perf_counter clock")
        if sample.get("time_basis") != "pc_acquisition":
            raise ValueError("Explicit pc_acquisition basis required for an acquisition timestamp")
        basis = "pc_acquisition"
    return value, unit, device, pc_time, basis


class Bridge:
    def __init__(self, quest, port=5006, input_port=5005, output=None):
        self.target = (socket.gethostbyname(quest), port)
        self.network = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.network.bind(("0.0.0.0", 0))
        self.input = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            self.input.bind(("127.0.0.1", input_port))
        except Exception:
            self.input.close()
            self.network.close()
            raise
        self.bridge_id = uuid.uuid4().hex
        self.session = None
        self.sequence = self.ping_sequence = 0
        self.pending_ping = None
        self.last_pong = -math.inf
        self.last_hello = self.last_ping = -math.inf
        self.invalid = self.received = self.sent = 0
        self.log = None
        try:
            folder = Path(output or "D:/Codexput/SereneNightResponses/BreathBridge")
            folder.mkdir(parents=True, exist_ok=True)
            self.log_path = folder / (time.strftime("%Y%m%d_%H%M%S_") + self.bridge_id + ".jsonl")
            self.log = self.log_path.open("x", encoding="utf-8")
            self.write_log({"kind": "bridge_start", "bridge_id": self.bridge_id,
                            "target": self.target, "utc_start": time.time(),
                            "pc_monotonic_start": time.perf_counter(),
                            "source": "external_real_belt_adapter_required"})
        except Exception:
            self.close()
            raise

    def write_log(self, record):
        self.log.write(json.dumps(record, ensure_ascii=False, allow_nan=False) + "\n")

    def send(self, text):
        try:
            self.network.sendto(text.encode("ascii"), self.target)
            return True
        except OSError as exc:
            self.write_log({"kind": "network_error", "message": str(exc), "pc_seconds": time.perf_counter()})
            return False

    def service_network(self, now):
        if now - self.last_hello >= 2:
            self.send("HELLO|1|" + self.bridge_id)
            self.last_hello = now
        if self.session and now - self.last_ping >= 1:
            self.ping_sequence += 1
            # Capture t1 immediately before send; no logging/file I/O in between.
            t1 = time.perf_counter()
            self.pending_ping = (self.ping_sequence, t1)
            self.send(f"PING|1|{self.session}|{self.bridge_id}|{self.ping_sequence}|{t1!r}")
            self.last_ping = now

    def receive_network(self):
        try:
            packet, sender = self.network.recvfrom(2049)
        except OSError as exc:
            # Windows may surface ICMP port-unreachable here before the APK starts.
            self.write_log({"kind": "network_error", "message": str(exc), "pc_seconds": time.perf_counter()})
            self.session = None
            self.pending_ping = None
            return
        t4 = time.perf_counter()
        if sender != self.target or len(packet) > 2048:
            return
        try:
            p = packet.decode("ascii").split("|")
            if len(p) == 3 and p[:2] == ["WELCOME", "1"] and re.fullmatch(r"[A-Za-z0-9_.-]{1,128}", p[2]):
                if p[2] != self.session:
                    self.session = p[2]
                    self.pending_ping = None
                    self.last_pong = -math.inf
                    self.last_ping = -math.inf
                    self.write_log({"kind": "quest_session", "session_id": self.session, "pc_seconds": t4})
                    print("Quest session:", self.session, flush=True)
                return
            if len(p) != 8 or p[:4] != ["PONG", "1", self.session, self.bridge_id]:
                return
            seq, t1, t2, t3 = int(p[4]), float(p[5]), float(p[6]), float(p[7])
            if self.pending_ping != (seq, t1) or not all(map(math.isfinite, (t1, t2, t3))) or t3 < t2:
                return
            rtt = (t4 - t1) - (t3 - t2)
            if not 0 <= rtt <= 2:
                return
            self.send(f"SYNC|1|{self.session}|{self.bridge_id}|{seq}|{t1!r}|{t2!r}|{t3!r}|{t4!r}")
            self.pending_ping = None
            self.last_pong = t4
            self.write_log({"kind": "clock_exchange", "session_id": self.session,
                            "sequence": seq, "pc_t1": t1, "quest_t2": t2, "quest_t3": t3,
                            "pc_t4": t4, "rtt_seconds": rtt})
        except (ValueError, UnicodeError):
            self.invalid += 1

    def receive_sample(self):
        try:
            packet, sender = self.input.recvfrom(2049)
        except OSError as exc:
            self.invalid += 1
            self.write_log({"kind": "adapter_socket_error", "message": str(exc), "pc_seconds": time.perf_counter()})
            return
        received = time.perf_counter()
        try:
            if len(packet) > 2048 or sender[0] != "127.0.0.1":
                raise ValueError("Invalid local adapter packet")
            value, unit, device, pc_time, basis = parse_sample(packet, received)
        except (ValueError, KeyError, TypeError, UnicodeError) as exc:
            self.invalid += 1
            self.write_log({"kind": "rejected_adapter_packet", "reason": str(exc), "pc_seconds": received})
            return
        self.sequence += 1
        self.received += 1
        attempted = bool(self.session and received - self.last_pong < 10)
        sent = False
        if attempted:
            device_text = "" if device is None else repr(device)
            sent = self.send(f"BREATH|1|{self.session}|{self.bridge_id}|{self.sequence}|{pc_time!r}|{value!r}|{unit}|{device_text}|{basis}")
            self.sent += int(sent)
        # Keep PC raw samples even when Quest/Wi-Fi is unavailable. No silent replay.
        self.write_log({"kind": "sample", "session_id": self.session, "sequence": self.sequence,
                        "pc_receive_seconds": received, "pc_sample_seconds": pc_time,
                        "device_seconds": device, "time_basis": basis, "value": value,
                        "unit": unit, "udp_sent": sent})

    def run(self):
        print("Waiting for a real respiration adapter on", self.input.getsockname(), flush=True)
        print("Raw PC backup:", self.log_path, flush=True)
        last_flush = time.perf_counter()
        while True:
            now = time.perf_counter()
            self.service_network(now)
            readable, _, _ = select.select([self.network, self.input], [], [], 0.1)
            for sock in readable:
                if sock is self.network:
                    self.receive_network()
                else:
                    self.receive_sample()
            if now - last_flush >= 1:
                self.log.flush()
                last_flush = now

    def close(self):
        self.input.close()
        self.network.close()
        if self.log:
            self.write_log({"kind": "bridge_stop", "samples_received": self.received,
                            "udp_sends": self.sent, "invalid_packets": self.invalid})
            self.log.close()
            self.log = None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quest", required=True, help="Quest Pro LAN IPv4 address")
    parser.add_argument("--port", type=int, default=5006)
    parser.add_argument("--input-port", type=int, default=5005)
    parser.add_argument("--output", default="D:/Codexput/SereneNightResponses/BreathBridge")
    args = parser.parse_args()
    bridge = Bridge(args.quest, args.port, args.input_port, args.output)
    try:
        bridge.run()
    except KeyboardInterrupt:
        pass
    finally:
        bridge.close()


if __name__ == "__main__":
    main()
