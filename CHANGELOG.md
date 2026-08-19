# Changelog

## .meta 파일 추가 (UPM git 설치 대응)

git URL로 설치하면 패키지가 immutable 폴더(Library/PackageCache)에 놓이는데, Unity는 여기에
.meta를 생성하지 못한다. 이 저장소에는 .meta가 하나도 없어서 모든 자산이 무시됐고
(`has no meta file, but it's in an immutable folder. The asset will be ignored`),
asmdef도 임포트되지 않아 `StatSystem` 어셈블리 자체가 만들어지지 않았다.
드롭인(Assets/ 복사) 설치에서는 Unity가 meta를 생성해 주기 때문에 드러나지 않던 문제다.

폴더와 자산 전체에 .meta를 추가했다. 코드 변경은 없다.

---

## Stat 슬롯 저장소 재설계 (Reflection 제거 · 변경 통지 도입)

### 배경

앞선 리팩토링에서 `StatValue`를 struct로 바꿔 산술 경로의 boxing은 없앴지만, 정작 `Stat`의 StatId 기반 일괄 처리 경로가 그 성과를 되돌리고 있었다. `Stat.GetValue/SetValue/AddValue`는 `Dictionary<StatId, FieldInfo>`에서 필드를 찾아 `FieldInfo.GetValue(this)`로 슬롯을 꺼냈는데, 이 호출은 struct인 `StatSlot`을 **매번 object로 boxing**한다. 버프 합산·전투력 재계산처럼 README가 직접 "고빈도 경로"라고 지목한 곳이 호출마다 힙을 두드리고 있었던 셈이다.

측정하면 이렇다. `SetValue` + `AddValue` + `GetValue` + 명명 접근 1회를 10만 번 반복(총 40만 연산)했을 때:

| | 할당량 |
|---|---|
| 이전 (FieldInfo 경로) | **19.07 MB** (20,000,000 B) |
| 이후 (배열 인덱싱) | **0 B** |

반복당 200바이트는 boxing 5회(40바이트짜리 `StatSlot` 박스)와 정확히 일치한다. 부수적으로 `Dictionary<StatId, ...>`는 Unity의 Mono/IL2CPP에서 enum 키의 `EqualityComparer<T>.Default`가 boxing 경로로 떨어질 수 있어, 자료구조 자체가 같은 문제를 한 겹 더 얹고 있었다.

### 해결

슬롯 저장소를 `StatSlot[]` 밀집 배열로 바꾸고, `StatId`(희소 uint, 현재 상한 502) → 배열 인덱스 평면 룩업 테이블을 정적 생성자에서 한 번 만든다. Reflection과 Dictionary가 모두 사라지고 조회가 배열 인덱싱 두 번으로 끝난다.

명명 접근자(`stat.AttackPower`)는 `ref readonly StatSlot` 프로퍼티로 노출한다. `ref`라서 24바이트 슬롯의 복사본이 생기지 않고, `readonly`라서 슬롯을 통한 쓰기가 컴파일 단계에서 막힌다. 방어적 복사가 되살아나지 않도록 `StatSlot`의 읽기 멤버에도 `readonly`를 붙였다.

쓰기 경로를 `SetValue`/`AddValue`/`SetMaxValue`로 좁힌 김에 변경 통지(`event Action<StatId, StatValue> Changed`)를 도입했다. 슬롯 직접 대입이 가능한 상태에서는 통지가 조용히 누락될 수 있어 둘은 같이 가야 하는 변경이다. 통지는 **클램프까지 반영된 실제 저장 값**으로 발행되며, 값이 바뀌지 않으면 발행되지 않는다(`Observable<T>`의 변경 감지 정책과 동일).

이 "출력은 읽기 전용 타입으로 노출해 역방향 쓰기를 컴파일 에러로 만든다"는 원칙은 [unity-mvvm](https://github.com/Frenil-client/unity-mvvm)의 `IReadOnlyObservable<T>`와 같은 결정이다.

### Breaking Changes

- `public StatSlot AttackPower` 등 18개 **public 필드 -> `ref readonly StatSlot` 프로퍼티**. 읽기(`stat.AttackPower.Value`)는 그대로지만 쓰기(`stat.AttackPower.Value = 5000`)는 `CS8332` 컴파일 에러다. `stat.SetValue(StatId.AttackPower, 5000L)`로 대체할 것
- `Stat.GetValue(StatId)`의 반환 타입 `double` -> `StatValue`. 이 시스템의 수치 타입은 `StatValue`인데 대표 접근자만 `double`로 내리는 것은 일관성이 없었다. 기존 동작이 필요하면 `.ToDouble()`
- `Stat.SetValue/AddValue`의 인자 `double` -> `StatValue` (`long`/`double` 리터럴은 암시적 변환되므로 호출부 대부분은 그대로 컴파일된다)
- `Stat.GetAllStats()` 제거 -> `Stat.Slots` (`IReadOnlyList<StatSlot>`). 호출마다 Dictionary를 새로 할당하던 API였고, 순서는 `StatId` 선언 순서와 같다
- `StatRegistry.AllIds`의 타입 `IEnumerable<StatId>` -> `IReadOnlyList<StatId>` (호출마다 `Enum.GetValues`로 배열을 새로 만들던 것을 캐싱)

### 추가

- `Stat.Changed` — 값 변경 통지 이벤트. 복사 생성자는 구독자를 복사하지 않는다
- `Stat.SetMaxValue(StatId, StatValue)` — 상한을 StatId 경로로도 설정
- `StatRegistry.GetName(StatId)`가 enum의 `ToString()` 대신 캐시된 문자열을 반환 (기존 구현은 호출마다 문자열 할당 + 내부 리플렉션)
- Runtime asmdef에 `noEngineReferences: true` — "순수 C#, Unity 비의존"이라는 README의 주장을 빌드 단계에서 강제한다

### 검증

`Tests/StatTests.cs`를 5종에서 18종으로 확장했다(레포 전체 32종 -> 45종). 클램프된 값이 통지되는지, 값이 그대로면 통지가 억제되는지, 복사본이 구독자를 물려받지 않는지를 각각 고정했고, 일괄 처리 경로와 명명 접근자 읽기에 `Is.Not.AllocatingGCMemory()` 할당 테스트를 걸었다. **이 할당 테스트 두 개는 이전 FieldInfo 구현에서는 실패한다** — 회귀 방지 장치로 의도한 것이다.

위 표의 수치는 Runtime 소스(순수 C#)를 .NET 8 콘솔에서 `GC.GetAllocatedBytesForCurrentThread()`로 측정한 값이다. `stat.AttackPower.Value = 5000L`이 실제로 `CS8332`로 거부되는지도 컴파일로 확인했다.

---

## StatValue 값 타입 전환 (boxing 제거)

### 배경

면접에서 받은 지적: 기존 `StatValue<T>` 연산자 오버로드가 `(T)(object)`로 피연산자를 매 호출마다 boxing했다. 스탯 연산은 전투/장비 갱신 시 고빈도로 호출되는 핫패스라 GC 압박의 직접 원인이었다. `StatValue<T>` 자체가 class였기 때문에, boxing을 없애도 `+`/`-` 연산자가 `new StatValue<T>(...)`로 매번 힙 할당을 했다.

원래 의도는 float 오차 없는 정확한 수치 연산(decimal 계열)이었는데, 제네릭 `T`(int/long/float/double)를 하나의 클래스로 처리하려다 보니 타입 분기마다 object 캐스트가 필요했던 것이 문제였다.

### 해결

`StatValue`를 long 기반 고정소수점(소수 4자리) non-generic readonly struct로 재작성했다. 컨테이너 역할(Id 보존, MaxValue 클램프)은 struct인 `StatSlot`이 이어받아 `+`/`-` 연산도 alloc-free를 유지한다. 자세한 설계 근거는 [README.md](README.md)의 "StatValue가 struct + long 고정소수점인 이유" 절 참고.

### Breaking Changes

- `StatValue<T>` (제네릭 class) 제거 -> `StatValue`(non-generic struct, 순수 산술) + `StatSlot`(struct, Id/MaxValue 컨테이너)로 분리
- `StatValue<T>.ValueDecimal` 제거 - `StatValue` 자체가 소수 4자리까지 정확해 별도 decimal 보관이 불필요해짐
- `Stat.GetLongStats()` / `Stat.GetDoubleStats()` -> `Stat.GetAllStats()`로 통합 (long/double 모두 `StatValue`로 표현되므로 CLR 타입별로 나눌 이유가 없어짐)
- `long`/`double` 리터럴은 `StatValue`로 암시적 변환되므로 (`StatValue v = 100;`, `StatValue p = 0.3;`) 호출부 코드는 대체로 타입 이름만 `StatValue<long>`/`StatValue<double>` -> `StatSlot`으로 바꾸면 그대로 컴파일된다

### 검증

`0.1 + 0.2 == 0.3` 정밀도 테스트, 오버플로 예외 테스트, `Is.Not.AllocatingGCMemory()` GC 할당 테스트를 `Tests/StatValueTests.cs`에 추가했다. 조사 과정과 설계 결정 근거는 [REFACTOR_SPEC.md](REFACTOR_SPEC.md)에 기록되어 있다.
