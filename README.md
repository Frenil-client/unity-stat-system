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
- **모디파이어 스택** - 기본값 위에 장비·버프를 붙였다 뗄 수 있고, 뗄 때 기본값부터 다시 계산해 값이 어긋나지 않음
- **최종값 캐싱** - 쓰기 시점에 계산해 두므로 읽기는 배열 인덱싱 한 번 (O(1), 할당 0)
- **변경 통지** - 최종값이 실제로 바뀔 때만 `Changed` 발행 (UI 바인딩 연동 지점)

## 구조

```
StatId              스탯 고유 ID enum
StatRegistry        StatId <-> string <-> uint 양방향 매핑
StatValue           고정소수점 값 타입 (readonly struct, partial)
 ├─ StatValue.cs            생성/변환, Equals/CompareTo/ToString
 └─ StatValue.Operators.cs  +, -, *, /, 비교 연산자, ApplyPercent
StatSlot            StatId + 기본값 + 상한 컨테이너 (struct)
StatModifier        붙였다 뗄 수 있는 보정 (struct) + StatModifierType + ModifierHandle
Stat                캐릭터 스탯 집합체
 └─ Stat.cs                 기본값 슬롯 + 모디파이어 목록 + 최종값 캐시, Changed 통지
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

### 1. 값은 기본값과 모디파이어 두 층으로 나뉜다

되돌리지 않는 수치와 붙였다 뗄 수 있는 수치는 성격이 다르므로 따로 보관한다.

| 층 | 무엇 | 예 |
|---|---|---|
| 기본값(base) | 영구 수치 | 레벨업, 강화, 능력치 투자 |
| 모디파이어 | 붙였다 뗄 수 있는 보정 | 장비, 버프, 세트 효과 |

```csharp
stat.SetBaseValue(StatId.AttackPower, 100L);        // 영구
stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L, sword);   // 임시
```

최종값은 이 순서로 접는다.

```
(기본값 + Flat 합) × (1 + PercentAdd 합) × Π(1 + PercentMultiply)   -> 상한으로 클램프
```

**왜 나눴는가.** 예전에는 버프를 `AddValue(+50)`으로 걸고 `AddValue(-50)`으로 풀었는데,
여기에 상한 클램프가 겹치면 값이 어긋났다.

```
방어력 180, 상한 200
  버프 +50  ->  230이지만 상한에 걸려 200 (실제로는 +20만 반영)
  버프 -50  ->  150            (원래 180이었는데 30이 사라짐)
```

버프를 받았다 풀었을 뿐인데 스탯이 줄어드는 버그다. "더한 만큼 뺀다"는 전제가 클램프 앞에서
깨지는 것이라, 호출부를 조심해서 막을 수 있는 종류가 아니다.

모디파이어는 뗄 때 빼는 게 아니라 **목록에서 제거하고 기본값부터 다시 계산**한다.
그래서 클램프가 걸렸든 아니든 원래 값으로 정확히 돌아온다.

```csharp
stat.SetMaxValue(StatId.Defense, 200L);
stat.SetBaseValue(StatId.Defense, 180L);

var armor = new object();                 // 실제로는 장비 인스턴스
stat.AddModifier(StatId.Defense, StatModifierType.Flat, 50L, armor);
// stat.Defense == 200   (상한)

stat.RemoveModifiersFrom(armor);
// stat.Defense == 180   (원래대로)
```

### 1-1. 적용 순서와 제거 방법

| 종류 | 계산 | 예 |
|---|---|---|
| `Flat` | 기본값에 그대로 더함 | 공격력 +50 |
| `PercentAdd` | 서로 합산 후 한 번 곱함 | +30%, +20% -> ×1.5 |
| `PercentMultiply` | 각각 순차로 곱함 | +30%, +20% -> ×1.3×1.2 |

enum 값이 곧 적용 순서(100 / 200 / 300)이며, **추가한 순서와 무관하게 결과가 같다.**
장비를 어떤 순서로 착용하든 스탯이 같아야 하기 때문이고, 이 성질은 테스트로 고정되어 있다.

제거는 두 가지다.

```csharp
var handle = stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);
stat.RemoveModifier(handle);              // 이 하나만

stat.RemoveModifiersFrom(armor);          // 이 주체가 붙인 것 전부 (장비 해제)
```

`source`는 참조 동일성으로 비교하므로 장비나 버프 인스턴스를 그대로 넘기면 된다.

### 1-2. 최종값은 쓰기 시점에 계산한다

모디파이어가 있으면 최종값은 계산 결과다. 읽을 때마다 접으면 UI가 매 프레임 스탯을 읽는
상황에서 비용이 그대로 드러난다. 그래서 **쓰기 시점에 계산해 캐시에 넣고, 읽기는 배열
인덱싱 한 번**으로 끝낸다.

읽기가 압도적으로 잦으므로 비용을 드문 쪽(쓰기)에 몰아준 선택이다. 재계산은 모디파이어
목록을 훑지만 `List<T>`의 struct 열거자를 쓰므로 **재계산 경로에도 할당이 없다.**
읽기와 재계산 양쪽에 할당 테스트가 걸려 있다.

비용은 모디파이어 전체 개수에 비례한다. 수십 개 규모를 전제한 선택이며, 수천 개가 되면
StatId별 버킷으로 나눠야 한다.

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

### 4. 슬롯 저장소가 배열이고, 쓰기 경로가 좁은 이유

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

이 수치는 `Benchmarks~/`에 들어 있는 코드가 만든 것이고, 누구나 다시 잴 수 있습니다.

```bash
dotnet run --project "Benchmarks~/StatAllocation.csproj" -c Release
```

벤치마크는 리팩토링 직전 구현(`LegacyReflectionStat`, 커밋 `b220d2e^`의 `Runtime/Stat.cs`를
그대로 가져온 것)과 현재 구현을 나란히 돌려 비교합니다. 과거 수치를 문서에 박아두는 대신
매번 다시 재므로 표가 낡지 않습니다.

**측정 조건**: .NET 8 / Release / `GC.GetAllocatedBytesForCurrentThread()` / 경로당 1,000회 워밍업 후 측정.
**Unity 런타임이 아니라 .NET 8에서 잰 값**이라 Mono·IL2CPP에서는 절대값이 다를 수 있습니다.
다만 여기서 보는 것은 최적화 결과가 아니라 boxing의 유무이고, 그건 런타임이 바뀌어도 방향이 같습니다.
Unity 환경에서의 검증은 `Tests/StatTests.cs`의 `Is.Not.AllocatingGCMemory()` 테스트가 맡습니다.
자세한 내용은 [`Benchmarks~/README.md`](Benchmarks~/README.md)에 있습니다.

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

이름으로 읽는 경로는 유지하되, 최종값을 돌려주는 **읽기 전용 프로퍼티**로 노출한다.
최종값은 계산 결과라 슬롯으로 내줄 수 없다.

```csharp
public StatValue AttackPower => GetValue(StatId.AttackPower);

stat.AttackPower                            // 최종값 (캐시된 배열 읽기)
stat.AttackPower = 5000L;                   // 컴파일 에러 - setter가 없다
stat.SetBaseValue(StatId.AttackPower, 5000L);                          // 기본값
stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 500L, buff);  // 보정
```

쓰기 경로를 좁힌 건 성능이 아니라 무결성 때문이다. 값을 직접 대입할 수 있으면 상한 클램프와
아래의 `Changed` 통지를 조용히 건너뛸 수 있고, 그렇게 생긴 불일치는 "UI에 표시된 스탯과 실제 스탯이
다르다"는 형태로 한참 뒤에 발견된다. 출력을 읽기 전용으로 노출해 역방향 쓰기를 막는 방식은
[unity-mvvm](https://github.com/Frenil-client/unity-mvvm)의 `IReadOnlyObservable<T>`와 같은 결정이다.

내부적으로 슬롯 배열은 여전히 `ref`로 다룬다. 24바이트 `StatSlot`이 복사되지 않도록
`StatSlot`의 읽기 멤버에는 `readonly`를 붙여 뒀다
(`public StatValue Value { readonly get => _value; set => ... }`).
이게 없으면 컴파일러가 멤버 호출마다 슬롯 복사본을 만든다.

### 5. 변경 통지는 "실제로 저장된 값"으로 발행한다

```csharp
stat.Changed += (id, value) => Debug.Log($"{StatRegistry.GetName(id)} -> {value}");

stat.SetBaseValue(StatId.AttackPower, 100L);  // 발행
stat.SetBaseValue(StatId.AttackPower, 100L);  // 값이 같아 발행되지 않음

stat.SetMaxValue(StatId.Defense, 200L);
stat.SetBaseValue(StatId.Defense, 999L);      // 999가 아니라 클램프된 200이 발행됨
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
├─ StatSlot.cs             StatId + 기본값 + 상한 컨테이너 (struct)
├─ StatModifier.cs         모디파이어 · 종류 · 핸들 (struct)
└─ Stat.cs                 캐릭터 스탯 집합체
Benchmarks~/
├─ StatAllocation.csproj   할당 벤치마크 (CI가 매 푸시마다 실행)
├─ LegacyReflectionStat.cs 리팩토링 직전 구현 - 비교 기준선
└─ Program.cs
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
(`com.unity.test-framework` 필요 · EditMode 테스트 63종 — StatValue / StatSlot / StatRegistry / Stat / StatModifier)

할당 회귀를 막는 테스트가 포함되어 있다. `BulkAccess_DoesNotAllocate`와
`NamedAccessorRead_DoesNotAllocate`는 `Is.Not.AllocatingGCMemory()`로 일괄 처리 경로와
명명 접근자 읽기가 할당 없이 도는지 확인하며, 둘 다 이전 Reflection 구현에서는 실패한다.

## CI

`.github/workflows/ci.yml`이 매 푸시마다 세 가지를 검증합니다. **Unity 라이선스가 필요 없습니다.**

| 단계 | 하는 일 |
|---|---|
| Core 빌드 | Runtime을 netstandard2.1 / C# 9로 컴파일 |
| 헤드리스 테스트 | 63종 중 **57종** 실행 |
| 할당 벤치마크 | 40만 연산 기준 0바이트 아니면 실패 |

**Core 빌드**는 컴파일 회귀를 잡는 동시에 "Runtime은 순수 C#이며 Unity에 의존하지 않는다"는
위의 주장을 빌드로 강제합니다. UnityEngine 참조가 들어오는 순간 깨집니다
(에디터 쪽에서는 asmdef의 `noEngineReferences: true`가 같은 제약을 담당합니다).

**헤드리스 테스트**는 `Tests~/`의 dotnet 프로젝트가 `Tests/`의 소스를 **그대로 컴파일**해 돌립니다.
사본이 아니라 같은 파일이므로 Unity Test Runner에서 도는 것과 정확히 같은 테스트입니다.
빠지는 6종은 `Is.Not.AllocatingGCMemory()`를 쓰는 `StatAllocationTests.cs`뿐이고,
그 성질은 할당 벤치마크가 수치까지 비교해 대신 검증합니다.

### 왜 Unity EditMode 테스트를 CI에서 돌리지 않는가

game-ci로 시도했지만 Unity Personal 라이선스는 `.ulf` 안에 MAC 주소와 머신 ID가 박힌
**하드웨어 바인딩** 방식이라, 실행마다 새로 만들어지는 GitHub 러너에서는 활성화되지 않습니다.
자체 호스팅 러너를 쓰면 라이선스는 맞지만 공개 저장소에서는 포크 PR이 임의 코드를 실행할 수
있어 선택지가 아닙니다.

그래서 "CI에서 Unity를 돌린다"를 포기하는 대신 **테스트를 Unity 없이 돌 수 있게** 만들었습니다.
초록 뱃지가 실제로 컴파일·테스트 57종·할당 회귀를 증명하며, 남은 6종은 로컬 Test Runner의 몫입니다.

## 스레딩

**메인 스레드 전용입니다.** 값 변경과 통지는 호출한 스레드에서 그대로 동기 실행되며,
내부에 락이나 스레드 마샬링이 없습니다. 백그라운드 스레드(네트워크 응답 콜백, `Task`
연속 실행 등)에서 값을 바꾸면 구독자도 그 스레드에서 깨어나고, 구독자가 Unity API를
건드리는 순간 예외가 납니다.

서버 응답처럼 다른 스레드에서 값이 들어오는 경우에는 **호출하는 쪽이 메인 스레드로
넘긴 뒤** 값을 설정해야 합니다. 이 제약을 라이브러리 안으로 들이지 않은 이유는,
마샬링 방식(코루틴 / `SynchronizationContext` / 자체 디스패처)이 프로젝트마다 다르고
그 선택을 패키지가 강제하면 오히려 걸림돌이 되기 때문입니다.

## 요구 사항

- Unity 2021.2+ (C# 9.0)
