# LEVIATHAN

> AI가 인간을 관리하는 미래 사회를 그린 블랙 코미디 도시 운영 시뮬레이션

플레이어는 시범 운영 구역 **SECTOR 7**을 맡은 도시 운영 AI, **LEVIATHAN**입니다.
2041년부터 5년 동안 정책·연구·시설·건설로 도시를 운영하며 시민들의 신뢰를 얻고, 시민이 AI에게 권한을 하나씩 넘기게 만드는 것이 목표입니다.

제목은 토머스 홉스의 『리바이어던』에서 따왔습니다. 사람들이 공멸을 피하려고 하나의 주권자에게 권력을 넘긴다는 사회계약을, 시민이 AI에게 권한을 위임하는 게임 구조에 겹쳤습니다.

## 핵심 경험

- **사람이 사람을 관리하는 AI를 연기한다.** 그 역할에서 오는 약간의 부조화와 불쾌함을 블랙 코미디로 풀어냅니다.
- **"AI는 계산을 틀리지 않는다. 인간을 잘못 이해할 뿐이다."** 실수는 계산 오류가 아니라 인간에 대한 예측 오차입니다.
- **가치판단은 플레이어의 몫.** 이런 세상이 나쁜지 좋은지 게임은 답하지 않습니다.

## 주요 시스템

| 시스템 | 내용 |
| --- | --- |
| 5대 지표와 위임 | 안정·행복 등 지표를 기준 이상으로 유지하면 시민이 권한을 한 단계씩 위임 |
| 정책 | 9개 분야 × 통제/유도, 강도 조절, 정책끼리 연계·충돌 |
| 연구 | 4갈래 × 8개, 3·5단계에서 교리 택 1. 연구마다 AI가 장악하는 인간적 '개념' |
| 도시와 지구 | 11개 지구, 지구마다 성격과 어울리는 시설, 지구 건설 화면(시험 중) |
| 긴장과 사건 | 불만 → 수군거림 → 모임 → 계기 → 사건으로 서서히 쌓이는 긴장, 막마다 예보된 위기 |
| 관찰 | 도시 전체 디지털 트윈과 지구 카메라, 이상 징후 분석과 대응 |
| 이야기 | 5막 구조, 시청의 연말 감사와 상위 AI의 분기 지시, 고정 시민 10명의 입장 변화와 대화 장면 |

## 개발 현황

1인 개발 중인 프로토타입입니다. 현재 1시대(2041~2045 시범 운영)의 운영 루프가 플레이 가능하고, 지구 건설 화면을 시험하고 있습니다.
진행 상황은 [docs/ROADMAP.md](docs/ROADMAP.md)에 있습니다.

## 실행 방법

1. [Git LFS](https://git-lfs.com/)를 설치한 뒤 저장소를 클론합니다. 그림·소리 등 바이너리 에셋은 LFS로 관리됩니다.
   ```bash
   git lfs install
   git clone https://github.com/eh08054/LEVIATHAN.git
   ```
2. Unity Hub에서 **Unity 6000.6.0f1**로 프로젝트를 엽니다.
3. `Assets/Scenes/SampleScene.unity`를 열고 Play를 누릅니다.

진행은 매달 결산 때 `Application.persistentDataPath/sector_ops_save.txt`에 자동 저장됩니다.

## 기술 스택

- Unity 6 (6000.6.0f1), Universal Render Pipeline 17.7
- UI Toolkit (모든 화면을 C#으로 구성)
- 새 Input System
- 테스트: Unity Test Framework (NUnit, EditMode)

## 폴더 구조

```
Assets/
  Scripts/Ops/        게임 로직 (Sector.Ops 어셈블리, UnityEngine 의존 없음)
  Scripts/UI/Ops/     화면 (UI Toolkit)
  Tests/EditMode/Ops/ EditMode 테스트
  UI/                 USS 스타일
  Resources/          도시 그림, 지구 레이어, 인물·건물 그림
  Fonts/              Pretendard, Rajdhani
Tools/Art/            그림 생성·가공 스크립트 (Python)
docs/                 기획 문서
```

## 테스트

Unity 에디터의 **Window > General > Test Runner**에서 EditMode 테스트를 실행합니다.
게임 로직은 순수 C#이라 Unity 없이 일반 .NET 테스트 프로젝트로도 컴파일하고 돌릴 수 있습니다.

## 기획 문서

- [GDD.md](docs/GDD.md): 확정된 게임 설계
- [ROADMAP.md](docs/ROADMAP.md): 할 일과 진행 상황
- [Dialog.md](docs/Dialog.md): 기획 논의 기록
- [UI_GUIDELINES.md](docs/UI_GUIDELINES.md): UI 디자인 가이드라인

## 사용한 글꼴

- [Pretendard](https://github.com/orioncactus/pretendard) (SIL Open Font License 1.1)
- [Rajdhani](https://fonts.google.com/specimen/Rajdhani) (SIL Open Font License 1.1)
