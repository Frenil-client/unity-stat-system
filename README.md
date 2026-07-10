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

### 5. StatValue가 struct + long 고정소수점인 이유

스탯 연산(`+`, `-`, 퍼센트 적용)은 전투/장비 갱신 시 고빈도로 호출되는 핫패스라, 호출마다 boxing이나 힙 할당이 생기면 GC 압박으로 바로 이어진다. 그래서 `StatValue`는 object를 경유하지 않는 8바이트 값 타입(struct)으로 설계했고, 소수 4자리(Scale=10000) long 고정소수점으로 값을 표현한다.

- **struct + non-generic**: 값 타입이라 boxing이 없고, `+`/`-`가 항상 struct를 반환하므로 연산자 체인(`a + b * c`)에서도 heap 할당이 없다
- **long 고정소수점 vs decimal vs double**: decimal도 boxing은 없지만 16바이트 + 소프트웨어 연산이라 핫패스엔 무겁고, double은 가볍지만 누적 오차(`0.1 + 0.2 != 0.3`)가 남는다. long 정수 연산은 8바이트로 가볍고 결과가 플랫폼/실행 간 결정적이며, 스탯 도메인(기본값 + 가산 + 퍼센트)엔 소수 4자리 정밀도로 충분하다
- **오버플로**: 곱셈/나눗셈은 `raw * raw`가 long 범위를 넘을 수 있어 decimal을 중간값으로 쓰고, 범위를 벗어나면 조용히 자르는 대신 `OverflowException`을 던진다 (값이 잘리면 전투 수치 버그가 은폐될 위험이 크기 때문)
- **컨테이너 분리**: StatId 보존과 MaxValue 클램프는 `StatValue`가 아니라 `StatSlot`(struct)의 역할이다. 산술 primitive와 컨테이너를 분리해 각각의 책임을 명확히 했다

검증: `0.1 + 0.2 == 0.3` 정밀도 테스트, 오버플로 예외 테스트, `Is.Not.AllocatingGCMemory()`로 `a + b * c` 체인 연산이 할당 없이 도는지 확인하는 GC 테스트가 `Tests/StatValueTests.cs`에 있다.

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
