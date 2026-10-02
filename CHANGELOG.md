# 변경 내역 / Changelog

## 1.0.0 (2026-10-02)

첫 공개 버전입니다.

- 모드·맵·봇(수·난이도·팀)을 고르고 버튼 하나로 CS2를 켜서 연습 서버를 엽니다.
- 서버가 열리면 친구에게 보낼 참가 링크가 자동으로 복사됩니다. 친구는 링크를 누르기만 하면 되고, 따로 설치하거나 개발자 콘솔을 켤 필요가 없습니다.
- 콘솔에 직접 붙여넣는 주소도 따로 복사할 수 있습니다.
- 게임 안에서 F10으로 맵·모드를 바꿉니다. 들어와 있는 친구들은 그대로 남습니다.
- CS2 설치 위치를 자동으로 찾습니다. 못 찾으면 폴더를 직접 고를 수 있습니다.
- 설정은 고르는 즉시 저장됩니다.

알아두실 것

- CS2는 이 앱의 ▶ 버튼으로 켜 주세요. Steam에서 직접 켜면 앱이 서버 주소를 읽지 못합니다.
- 방장이 게임에서 메뉴로 나가면 서버가 닫히고 친구들도 모두 끊깁니다.
- 봇을 한쪽 팀으로만 몰면 실제 인원이 「사람 수 + 2」에서 멈춥니다. 게임의 팀 인원 제한 때문이며, 「양쪽에 섞기」로 두면 고른 수만큼 들어옵니다.
- 이 앱은 게임에 설정 파일(`cs2host_*.cfg`)을 만들고 F10 키를 다시 지정합니다. 기존에 F10을 쓰고 계셨다면 덮어씌워집니다.
- 서명이 없어 Windows가 경고를 띄웁니다. 「추가 정보」 → 「실행」으로 진행하실 수 있습니다.

---

First public release.

- Pick a mode, map, and bots (count, difficulty, team), then open a practice server with one button.
- When the server is up, a join link is copied automatically. Friends only need to click it — no install, no developer console.
- The raw console address can be copied separately if needed.
- Press F10 in game to change map or mode. Connected friends stay in.
- Finds your CS2 install automatically, and lets you pick the folder if it cannot.
- Settings are saved as you choose them.

Notes

- Launch CS2 with this app's ▶ button. If you start CS2 from Steam directly, the app cannot read the server address.
- If the host leaves to the main menu, the server closes and everyone is disconnected.
- Putting all bots on one team caps them at "humans + 2" — a game rule on team size. Use "both teams" to get the number you picked.
- This app writes config files (`cs2host_*.cfg`) into the game and rebinds F10, replacing any existing F10 binding.
- The build is unsigned, so Windows shows a warning. Choose "More info" → "Run anyway".
