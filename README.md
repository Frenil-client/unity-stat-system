# Unity Generic Stat System

[![CI](https://github.com/Frenil-client/unity-stat-system/actions/workflows/ci.yml/badge.svg)](https://github.com/Frenil-client/unity-stat-system/actions/workflows/ci.yml)

캐릭터 스탯 시스템입니다.  
RPG에서 범용적으로 사용되는 전투 능력치 구조를 기반으로 설계했습니다.

## 특징

- **고정소수점 값 타입** - `StatValue`(long 기반, 소수 4자리)로 boxing/heap 할당 없이 결정적인 수치 연산
- **StatId enum** - 가독성 있는 이름으로 스탯 식별, `StatRegistry`로 이름<->UID 양방향 조회
- **MaxValue 무결성 검사** - Value 초과 시 자동 클램프, MaxValue 역전 시 예외
- **합산 연산자** - 여러 소스의 스탯을 `+`, `-`로 직관적으로 합산 (퍼센트 곱연산은 `StatValue.ApplyPercent`로 처리)
- **할당 없는 일괄 처리** - `StatSlot[]` 밀집 배열 + StatId 인덱스 룩업. Reflection과 Dictionary를 모두 걷어내 40만 연산 기준 19.07MB -> **0B**
- **읽기/쓰기 경로 분리** - 이름으로 읽기는 `ref readonly`(복사본 없음), 쓰기는 `SetValue` 하나로 좁혀 클램프·통지 우회를 컴파일 단계에서 차단
- **변경 통지** - 값이 실제로 바뀔 때만, 클램프까지 반영된 최종 값으로 `Changed` 발행 (UI 바인딩 연동 지점)

## 구조

```
StatId              스탯 고유 ID enum
StatRegistry        StatId <-> string <-> uint 양방향 매핑
StatValue           고정소수점 값 타입 (readonly struct, partial)
 ├─ StatValue.cs            생성/변환, Equals/CompareTo/ToString
 └─ StatValue.Operators.cs  +, -, *, /, 비교 연산자, ApplyPercent
StatSlot            StatId + StatValue Value + StatValue MaxValue 컨테이너 (struct)
Stat                캐릭터 스탯 집합체
 └─ Stat.cs                 StatSlot[] 저장소, StatId 인덱스 룩업, ref readonly 접근자, Changed 통지
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

곱연산(퍼센트 적용)과 데미지 공식은 이 레이어의 책임이 아니다. `Stat`은 여러 소스의 수치를
더하고 빼서 "현재 스탯"을 유지하는 것까지만 하고, `AttackPower * (1 + AttackPowerPercent)`
같은 최종 계산은 소비하는 쪽(데미지 계산 레이어)에서 수행한다.

```csharp
// 소스별 슬롯을 합산
var total = baseAtk + equipBonus + buffBonus;   // StatSlot 연산자

// 캐릭터 스탯에 누적
stat.AddValue(StatId.AttackPower, 500L);
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

### 4. 슬롯 저장소가 배열이고, 쓰기 경로가 하나인 이유

초기 구현은 18개 `public StatSlot` 필드를 두고 `Dictionary<StatId, FieldInfo>`로 일괄 처리를 했다.
Reflection 수집 비용은 static 생성자에서 한 번만 지불하니 괜찮다고 봤는데, 실제 문제는 수집이 아니라
**조회**였다. `FieldInfo.GetValue(this)`는 struct인 `StatSlot`을 호출마다 object로 boxing한다.
`StatValue`를 struct로 만들어 산술 경로의 boxing을 없애 놓고, 정작 그 값을 꺼내는 경로가
할당을 하고 있었던 것이다.

```
SetValue + AddValue + GetValue + 명명 접근  ×  10만 회 (총 40만 연산)

  이전 (FieldInfo)   19.07 MB   ← 반복당 200B = 40B짜리 StatSlot 박스 5개
  이후 (배열 인덱싱)      0 B
```

그래서 슬롯 실체를 `StatSlot[]` 밀집 배열 하나로 모으고, `StatId`(희소 uint, 현재 상한 502)에서
배열 인덱스를 얻는 평면 룩업 테이블을 정적 생성자에서 만든다. `Dictionary<StatId, ...>`를 쓰지 않은 건
Unity의 Mono/IL2CPP에서 enum 키의 `EqualityComparer<T>.Default`가 boxing 경로로 떨어지는 경우가 있어서다.

```csharp
private static int IndexOf(StatId id)
{
    uint raw = (uint)id;
    return raw <= _maxRawId ? _idToIndex[raw] : -1;   // 배열 인덱싱 두 번, 할당 없음
}
```

이름으로 읽는 경로는 유지하되 `ref readonly`로 노출한다. `ref`라서 24바이트 슬롯이 복사되지 않고,
`readonly`라서 슬롯을 통한 쓰기가 막힌다.

```csharp
public ref readonly StatSlot AttackPower => ref Slot(StatId.AttackPower);

stat.AttackPower.Value             // OK - 복사본 없이 읽기
stat.AttackPower.Value = 5000L;    // 컴파일 에러 CS8332
stat.SetValue(StatId.AttackPower, 5000L);   // 쓰기는 이쪽으로
```

쓰기를 막은 건 성능이 아니라 무결성 때문이다. 슬롯을 직접 대입할 수 있으면 MaxValue 클램프와
아래의 `Changed` 통지를 조용히 건너뛸 수 있고, 그렇게 생긴 불일치는 "UI에 표시된 스탯과 실제 스탯이
다르다"는 형태로 한참 뒤에 발견된다. 출력을 읽기 전용 타입으로 노출해 역방향 쓰기를 컴파일 에러로
만드는 방식은 [unity-mvvm](https://github.com/Frenil-client/unity-mvvm)의 `IReadOnlyObservable<T>`와 같은 결정이다.

`ref readonly`로 넘긴 이득이 방어적 복사로 되돌아가지 않도록 `StatSlot`의 읽기 멤버에는
`readonly`를 붙였다 (`public StatValue Value { readonly get => _value; set => ... }`).
이게 없으면 컴파일러가 멤버 호출마다 슬롯 복사본을 만든다.

### 5. 변경 통지는 "실제로 저장된 값"으로 발행한다

```csharp
stat.Changed += (id, value) => Debug.Log($"{StatRegistry.GetName(id)} -> {value}");

stat.SetValue(StatId.AttackPower, 100L);  // 발행
stat.SetValue(StatId.AttackPower, 100L);  // 값이 같아 발행되지 않음

stat.SetMaxValue(StatId.Defense, 200L);
stat.SetValue(StatId.Defense, 999L);      // 999가 아니라 클램프된 200이 발행됨
```

요청값을 그대로 흘리면 UI가 실제 스탯과 다른 숫자를 표시하게 되므로, 통지는 항상 슬롯에서 다시 읽은
값으로 발행한다. 값이 바뀌지 않으면 발행하지 않는 정책은 `Observable<T>`의 변경 감지와 동일하다.
복사 생성자는 값만 복사하고 구독자는 복사하지 않는다 — 구독은 인스턴스마다 독립이다.

### 6. StatValue가 struct + long 고정소수점인 이유

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
(`com.unity.test-framework` 필요 · EditMode 테스트 45종 — StatValue / StatSlot / StatRegistry / Stat)

할당 회귀를 막는 테스트가 포함되어 있다. `BulkAccess_DoesNotAllocate`와
`NamedAccessorRead_DoesNotAllocate`는 `Is.Not.AllocatingGCMemory()`로 일괄 처리 경로와
명명 접근자 읽기가 할당 없이 도는지 확인하며, 둘 다 이전 Reflection 구현에서는 실패한다.

## CI

`.github/workflows/ci.yml` 이 두 단계로 돌아간다.

| Job | 하는 일 | Unity 라이선스 |
|---|---|---|
| `core-build` | Runtime을 netstandard2.1 / C# 9 로 컴파일 | 불필요 |
| `editmode-tests` | game-ci로 EditMode 테스트 45종 실행 | 필요 |

`core-build`는 컴파일 회귀를 잡는 동시에 "Runtime은 순수 C#이며 Unity에 의존하지 않는다"는
위의 주장을 빌드로 강제한다. Runtime에 UnityEngine 참조가 들어오는 순간 이 job이 깨진다
(에디터 쪽에서는 asmdef의 `noEngineReferences: true`가 같은 제약을 담당한다).

`editmode-tests`는 패키지 저장소에 Unity 프로젝트가 없으므로, 이 패키지만 참조하는 최소
프로젝트를 워크플로에서 만들어 그 안에서 테스트를 돌린다(로컬 패키지의 `Tests/`가 잡히도록
manifest의 `testables`에 등록). 라이선스 시크릿(`UNITY_LICENSE`, `UNITY_EMAIL`,
`UNITY_PASSWORD`)이 없는 저장소에서는 이 job을 건너뛴다 — 포크 PR에서 라이선스가 없다는
이유로 빨간 X가 뜨는 것을 막기 위한 게이트다.

## 요구 사항

- Unity 2021.2+ (C# 9.0)
