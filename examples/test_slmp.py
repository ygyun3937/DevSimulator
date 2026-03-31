"""
DevSimulator — SLMP 통신 테스트 스크립트
=========================================
사용법:
  1. DevSimulator 앱 실행 후 ▶ 시작 클릭
  2. 이 스크립트 실행: python test_slmp.py

Python 3.x 표준 라이브러리만 사용 (설치 불필요)
"""

import socket
import struct
import time

HOST = "127.0.0.1"
PORT = 5000


def build_read_request(device_no: int, device_code: str, points: int) -> bytes:
    """SLMP 3E 프레임 읽기 요청 생성"""
    data = struct.pack("<HHH", 0x0010, 0x0401, 0x0000)  # timer, command, subcmd
    data += struct.pack("<I", device_no)[:3]              # device no (3 bytes)
    data += device_code.encode("ascii")                    # device code ('D', 'M' ...)
    data += struct.pack("<H", points)                      # number of points

    data_len = len(data)
    header = struct.pack("<H", 0x0050)           # subheader 50 00
    header += bytes([0x00, 0xFF, 0xFF, 0x03, 0x00])  # network, PC, IO, station
    header += struct.pack("<H", data_len)
    return header + data


def build_write_request(device_no: int, device_code: str, values: list[int]) -> bytes:
    """SLMP 3E 프레임 쓰기 요청 생성"""
    points = len(values)
    data = struct.pack("<HHH", 0x0010, 0x1401, 0x0000)  # timer, command, subcmd
    data += struct.pack("<I", device_no)[:3]
    data += device_code.encode("ascii")
    data += struct.pack("<H", points)
    for v in values:
        data += struct.pack("<h", v)  # signed short (little-endian)

    data_len = len(data)
    header = struct.pack("<H", 0x0050)
    header += bytes([0x00, 0xFF, 0xFF, 0x03, 0x00])
    header += struct.pack("<H", data_len)
    return header + data


def parse_read_response(resp: bytes, points: int) -> list[int]:
    """읽기 응답에서 레지스터 값 추출"""
    # 응답 구조: 헤더(9) + end_code(2) + data(2*points)
    if len(resp) < 11 + points * 2:
        raise ValueError(f"응답 길이 부족: {len(resp)}")
    end_code = struct.unpack_from("<H", resp, 9)[0]
    if end_code != 0:
        raise ValueError(f"SLMP 에러 코드: 0x{end_code:04X}")
    values = []
    for i in range(points):
        v = struct.unpack_from("<h", resp, 11 + i * 2)[0]
        values.append(v)
    return values


def demo():
    print("=" * 50)
    print("DevSimulator SLMP 통신 테스트")
    print(f"접속: {HOST}:{PORT}")
    print("=" * 50)

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.settimeout(5)
        try:
            s.connect((HOST, PORT))
            print("✅ 연결 성공\n")
        except ConnectionRefusedError:
            print("❌ 연결 실패 — DevSimulator가 실행 중인지 확인하세요")
            return

        # ── 테스트 1: D100 읽기 (초기값 = 0) ─────────────────────
        print("[테스트 1] D100 읽기 (초기값)")
        req = build_read_request(100, 'D', 1)
        s.sendall(req)
        resp = s.recv(256)
        val = parse_read_response(resp, 1)
        print(f"  D100 = {val[0]}\n")

        # ── 테스트 2: D100에 1234 쓰기 ───────────────────────────
        print("[테스트 2] D100 = 1234 쓰기")
        req = build_write_request(100, 'D', [1234])
        s.sendall(req)
        resp = s.recv(256)
        end_code = struct.unpack_from("<H", resp, 9)[0]
        print(f"  쓰기 결과: {'✅ 성공' if end_code == 0 else f'❌ 에러 0x{end_code:04X}'}\n")

        # ── 테스트 3: D100 다시 읽기 (1234 확인) ─────────────────
        print("[테스트 3] D100 읽기 (1234 확인)")
        req = build_read_request(100, 'D', 1)
        s.sendall(req)
        resp = s.recv(256)
        val = parse_read_response(resp, 1)
        print(f"  D100 = {val[0]}  {'✅ 정상' if val[0] == 1234 else '❌ 값 불일치'}\n")

        # ── 테스트 4: D0~D4 연속 5개 읽기 ────────────────────────
        print("[테스트 4] D0~D4 연속 5개 읽기")
        req = build_read_request(0, 'D', 5)
        s.sendall(req)
        resp = s.recv(256)
        vals = parse_read_response(resp, 5)
        for i, v in enumerate(vals):
            print(f"  D{i} = {v}")
        print()

        # ── 테스트 5: D0~D4 연속 쓰기 ────────────────────────────
        print("[테스트 5] D0~D4 = [10, 20, 30, 40, 50] 연속 쓰기")
        req = build_write_request(0, 'D', [10, 20, 30, 40, 50])
        s.sendall(req)
        resp = s.recv(256)
        end_code = struct.unpack_from("<H", resp, 9)[0]
        print(f"  쓰기 결과: {'✅ 성공' if end_code == 0 else f'❌ 에러 0x{end_code:04X}'}")

        time.sleep(0.1)

        req = build_read_request(0, 'D', 5)
        s.sendall(req)
        resp = s.recv(256)
        vals = parse_read_response(resp, 5)
        for i, v in enumerate(vals):
            print(f"  D{i} = {v}  {'✅' if v == (i+1)*10 else '❌'}")
        print()

    print("=" * 50)
    print("테스트 완료")
    print("=" * 50)


if __name__ == "__main__":
    demo()
