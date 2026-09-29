# LongYinRoster — 에이전트 작업 규칙

BepInEx 6 (IL2CPP) 플러그인. 게임 `龙胤立志传` 의 설치 경로는 `Directory.Build.props` 의 `GameDir` 이 단일 출처이며,
Release 빌드가 `$(GameDir)/BepInEx/plugins/LongYinRoster/` 로 자동 배포된다(`DeployToBepInEx`).
진행 상태·릴리스 이력은 `docs/HANDOFF.md`.

## Git 브랜치 전략

`main` 은 **보호 브랜치** — 직접 커밋·푸시·force-push 금지(GitHub 보호 설정 여부와 무관). `develop` 이 통합 브랜치.

1. 작업 브랜치는 항상 `develop` 에서 분기: `feat/*` · `fix/*` · `chore/*` · `docs/*`
2. 작업 브랜치 → `develop` PR 로 머지 (기본 merge commit; 스쿼시·리베이스는 명시 요청 시에만)
3. 릴리스 시점에만 `develop` → `main` PR. `main` 에는 오직 이 경로로만 들어간다.
4. 머지 전 CI 녹색 확인 (현재 워크플로 없음 — 추가 시 이 항목이 유효해짐). 최소한 `dotnet test` 통과.
5. `main`·`develop` 에 직접 push 금지. 모든 변경은 PR.

## 빌드 / 테스트

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Debug     # 배포 없음
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release   # 게임 폴더로 배포 (게임 종료 상태에서)
dotnet test                                                       # 단위 테스트 (xunit)
```

- 경고를 오류로 취급(`TreatWarningsAsErrors`), `Nullable` enable.
- 게임 API 는 Assembly-CSharp 정적 참조 없이 reflection 으로만 접근한다 — 게임 버전이 바뀌면 컴파일은 통과해도 런타임에 조용히 틀릴 수 있으므로, `IL2CppListOps` 경로와 각 캐시의 `BuildFromDb` 시임을 우선 확인한다.
- 소스 파일 줄끝은 CRLF, 문서는 파일마다 다름 — 편집 후 줄끝 혼합 여부를 확인한다.
