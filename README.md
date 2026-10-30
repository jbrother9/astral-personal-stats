# Astral Personal Stats

아스트랄 파티 개인 전적 통계 모드 (Windows x64).

게임 종료 후 리플레이를 수집·분석하여 게임 안에서 본인 통계를 보여줍니다. 전체·맵·캐릭터별 전적, PvE/PvP 승률, 카드 사용·구매, 칩 선택, 전투 주사위와 경기별 상세 정보를 확인할 수 있습니다.

## 다운로드 및 설치

[Releases](https://github.com/jbrother9/astral-personal-stats/releases)에서 배포 ZIP을 받으세요. GitHub의 자동 생성 Source code ZIP은 설치용이 아닙니다.

1. 게임을 완전히 종료합니다.
2. 배포 ZIP을 모두 압축 해제합니다.
3. Install.cmd를 실행합니다.
4. Steam에서 게임을 실행하고 내 프로필 → 내 전적을 엽니다.

Python·Npcap 별도 설치는 필요 없습니다. 한글패치는 포함하지 않습니다. 현재는 베타이며 다른 PC에서 실제 한 판 수집까지 확인이 필요합니다.

업데이트·제거·데이터 보관 방법은 [설치 안내](INSTALL.md)를 참고하세요.

## 개발 및 데이터

- [빌드 안내](sources/BUILD.md)
- sources/plugin: 게임 모드 C# 소스
- sources/worker: 백그라운드 분석기 Python 소스
- installer: 배포 패키지에 포함되는 설치 도구 (이 저장소 폴더에서 직접 실행하지 마세요)
- [외부 구성요소 고지](THIRD-PARTY-NOTICES.md)

게임 리소스·한글패치·개인 DB·리플레이·로그·계정 설정은 이 저장소에 포함하지 않습니다. 오류 신고 시 개인 기록이나 인증 정보가 담긴 파일을 그대로 첨부하지 마세요.

게임 제작사와 무관한 비공식 개인 통계 모드입니다.
