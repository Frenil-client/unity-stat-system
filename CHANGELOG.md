# Changelog

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
