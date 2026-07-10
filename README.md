# Unity Generic Stat System

캐릭터 스탯 시스템입니다.  
RPG에서 범용적으로 사용되는 전투 능력치 구조를 기반으로 설계했습니다.

## 특징

- **고정소수점 값 타입** - `StatValue`(long 기반, 소수 4자리)로 boxing/heap 할당 없이 결정적인 수치 연산
- **StatId enum** - 가독성 있는 이름으로 스탯 식별, `StatRegistry`로 이름<->UID 양방향 조회
- **MaxValue 무결성 검사** - Value 초과 시 자동 클램프, MaxValue 역전 시 예외
- **합산 연산자** - 여러 소스의 스탯을 `+`, `-`로 직관적으로 합산 (퍼센트 곱연산은 `StatValue.ApplyPercent`로 처리)
- **이중 접근 방식** - 필드 이름 직접 접근 + StatId 기반 Dictionary 일괄 처리
- **Reflection 캐싱** - static 생성자에서 한 번만 수집해 반복 비용 제거

## 구조

```
StatId              스탯 고유 ID enum
StatRegistry        StatId <-> string <-> uint 양방향 매핑
StatValue           고정소수점 값 타입 (readonly struct, partial)
 ├─ StatValue.cs            생성/변환, Equals/CompareTo/ToString
 └─ StatValue.Operators.cs  +, -, *, /, 비교 연산자, ApplyPercent
StatSlot            StatId + StatValue Value + StatValue MaxValue 컨테이너 (struct)
Stat                캐릭터 스탯 집합체
 └─ Stat.cs                 public 필드, Dictionary 매핑, Reflection 캐싱
```

## StatId 구간 규칙

```
1xx  공격력 / 마력    AttackPower / MagicAttack / AttackPowerPercent / Defense ...
2xx  데미지 / 크리    Damage / FinalDamage / CriticalRate / CriticalDamage
3xx  방어 / 생존      StatusResistance
4xx  보스 / 방어율    BossDamage / IgnoreDefense / NormalMonsterDamage / IgnoreElemental
5xx  이동 / 기타      MoveSpeed / JumpPower / AttackSpeed
```

## 핵심 설계

### 1. Stat 레이어는 합산만 처리

```csharp
// Stat은 항상 + / - 합산만 수행
var total = baseStat + equipBonus + buffBonus;
stat.AddValue(StatId.AttackPower, 500);

```

### 2. StatId + StatRegistry

```csharp
string name  = StatRegistry.GetName(StatId.AttackPower); // "AttackPower"
uint   uid   = StatRegistry.GetUid(StatId.AttackPower);  // 100
StatId byUid = StatRegistry.GetId(100u);                 // StatId.AttackPower
StatId byStr = StatRegistry.GetId("AttackPower");        // StatId.AttackPower
```

### 3. MaxValue 무결성 검사

```csharp
var def = new StatSlot(StatId.Defense, 100L, 200L);
def.Value = 999;    // -> 200으로 자동 클램프
def.MaxValue = 50;  // -> ArgumentException
```

### 4. Reflection 필드 캐싱

```csharp
static Stat()
{
    // 최초 한 번만 실행 - StatSlot 필드를 수집해 캐싱
    _fieldCache = typeof(Stat)
        .GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Where(f => f.FieldType == typeof(StatSlot))
        .ToDictionary(f => f.Name, f => f);
}
```

### 5. 박싱 제거 - StatValue 고정소수점 값 타입

**문제**: 기존 `StatValue<T>` 연산자 오버로드가 `(T)(object)`로 피연산자를 매 호출마다 boxing했다. 스탯 연산은 전투/장비 갱신 시 고빈도로 호출되는 핫패스라 GC 압박의 직접 원인이었다. 게다가 `StatValue<T>` 자체가 class여서, boxing을 없애도 `+`/`-` 연산자가 `new StatValue<T>(...)`로 매번 힙 할당을 했다.

**원인**: float 오차 없는 정확한 수치 연산(decimal 계열)이 목표였는데, 제네릭 `T`(int/long/float/double)를 하나의 클래스로 처리하려다 보니 타입 분기마다 object 캐스트가 필요했다.

**해결**: `StatValue`를 long 기반 고정소수점(소수 4자리, Scale=10000) non-generic readonly struct로 재작성했다. 8바이트 값 타입 하나로 모든 스탯을 표현하므로 object 경유가 없고, 정수 연산이라 결과가 결정적이다. 컨테이너 역할(Id 보존, MaxValue 클램프)은 struct인 `StatSlot`이 이어받아 `+`/`-` 연산도 alloc-free를 유지한다. 곱셈/나눗셈은 `raw * raw`가 long 범위를 넘을 수 있어 decimal을 중간값으로 쓰고, 범위를 넘으면 조용히 자르지 않고 `OverflowException`을 던지도록 했다.

**검증**: `0.1 + 0.2 == 0.3`이 정확히 성립하는 정밀도 테스트, 오버플로 예외 테스트, `Is.Not.AllocatingGCMemory()`로 `a + b * c` 체인 연산이 할당 없이 도는지 확인하는 GC 테스트를 추가했다 (`Tests/StatValueTests.cs`).

### Breaking Changes

- `StatValue<T>` (제네릭 class) 제거 -> `StatValue`(non-generic struct, 순수 산술) + `StatSlot`(struct, Id/MaxValue 컨테이너)로 분리
- `StatValue<T>.ValueDecimal` 제거 - `StatValue` 자체가 소수 4자리까지 정확해 별도 decimal 보관이 불필요해짐
- `Stat.GetLongStats()` / `Stat.GetDoubleStats()` -> `Stat.GetAllStats()`로 통합 (long/double 모두 `StatValue`로 표현되므로 CLR 타입별로 나눌 이유가 없어짐)
- `long`/`double` 리터럴은 `StatValue`로 암시적 변환되므로 (`StatValue v = 100;`, `StatValue p = 0.3;`) 호출부 코드는 대체로 타입 이름만 `StatValue<long>`/`StatValue<double>` -> `StatSlot`으로 바꾸면 그대로 컴파일된다

## 파일 구성

```
Runtime/
├─ StatId.cs               스탯 고유 ID enum
├─ StatRegistry.cs         StatId <-> string <-> uint 양방향 매핑
├─ StatValue.cs            고정소수점 값 타입 (struct)
├─ StatValue.Operators.cs  +, -, *, /, 비교 연산자, ApplyPercent
├─ StatSlot.cs             StatId + Value + MaxValue 컨테이너 (struct)
└─ Stat.cs                 캐릭터 스탯 집합체
Samples~/StatExample/
└─ StatExample.cs          Unity MonoBehaviour 사용 예시
```

## 설치

### UPM (Package Manager) — 권장
`Window ▸ Package Manager ▸ + ▸ Add package from git URL` 에 입력:

```
https://github.com/Frenil-client/unity-stat-system.git
```

또는 `Packages/manifest.json` 에 직접 추가:

```json
"com.frenil.stat-system": "https://github.com/Frenil-client/unity-stat-system.git"
```

### 드롭인
`Runtime/` 폴더를 프로젝트 `Assets/` 아래에 복사합니다. (순수 C# — 외부 의존 없음)

### 샘플
Package Manager에서 이 패키지를 선택 → **Samples ▸ Import** (원본: `Samples~/StatExample`).

## 테스트

`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`
(`com.unity.test-framework` 필요 · EditMode 테스트 32종 — StatValue / StatSlot / StatRegistry / Stat)

## 요구 사항

- Unity 2021.2+ (C# 9.0)
