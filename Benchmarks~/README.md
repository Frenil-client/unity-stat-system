# 할당 벤치마크

README와 CHANGELOG에 적힌 할당 수치를 재현하는 코드입니다.

```bash
dotnet run --project "Benchmarks~/StatAllocation.csproj" -c Release
```

## 무엇을 재는가

StatId 기반 일괄 처리 경로(`SetValue` / `AddValue` / `GetValue`)와 명명 접근자 읽기를
10만 번 반복하며 힙 할당을 측정합니다. 버프 합산이나 전투력 재계산처럼 실제로 고빈도로
호출되는 경로입니다.

같은 연산을 두 구현으로 돌려 비교합니다.

- `LegacyReflectionStat` — 리팩토링 직전 구현. 커밋 `b220d2e^`의 `Runtime/Stat.cs`를
  네임스페이스와 클래스명만 바꿔 그대로 가져왔습니다
- `Stat` — 현재 구현 (`Runtime/`을 그대로 컴파일)

과거 수치를 문서에 박아두는 대신 매번 다시 재므로, 표의 숫자는 누구나 재현할 수 있습니다.

## 측정 조건

| 항목 | 값 |
|---|---|
| 런타임 | .NET 8 (실행 시 정확한 버전이 출력됩니다) |
| 구성 | Release |
| 측정 방법 | `GC.GetAllocatedBytesForCurrentThread()` |
| 대상 | `Runtime/` 소스를 그대로 컴파일한 어셈블리 |

**한계를 분명히 해둡니다.** 이 수치는 **Unity 런타임이 아니라 .NET 8에서 잰 값**입니다.
Unity의 Mono나 IL2CPP에서는 절대값이 다를 수 있습니다. 다만 여기서 재는 것은 최적화 결과가
아니라 **boxing이 일어나는가 아닌가**이고, 그건 언어 수준의 성질이라 런타임이 바뀌어도
방향은 같습니다. `FieldInfo.GetValue()`가 struct를 object로 감싸는 것은 어느 런타임에서나 마찬가지입니다.

Unity 안에서의 검증은 `Tests/StatTests.cs`의 `Is.Not.AllocatingGCMemory()` 테스트가 담당합니다.
둘은 서로를 보완하는 관계입니다 — 이쪽은 비교와 재현, 저쪽은 Unity 환경에서의 회귀 방지.

## CI

이 벤치마크는 GitHub Actions에서 매 푸시마다 돌아갑니다. 현재 구현이 **1바이트라도 할당하면
종료 코드 1로 실패**하므로, 할당이 되살아나는 변경은 푸시 시점에 막힙니다.
수치를 문서에 적어두는 것과 달리 이건 문서가 낡을 수 없습니다.
