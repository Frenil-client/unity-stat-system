# Unity Generic Stat System

제네릭 기반 캐릭터 스탯 시스템입니다.  
RPG에서 범용적으로 사용되는 전투 능력치 구조를 기반으로 설계했습니다.

## 특징

- **제네릭 타입** - `int / long / float / double` 등 수치 타입을 단일 클래스로 통합 처리
- **StatId enum** - 가독성 있는 이름으로 스탯 식별, `StatRegistry`로 이름<->UID 양방향 조회
- **MaxValue 무결성 검사** - Value 초과 시 자동 클램프, MaxValue 역전 시 예외
- **decimal 정밀도 유지** - 부동소수점 오차 방지를 위한 `ValueDecimal` 병행 보관
- **합산 연산자** - 여러 소스의 스탯을 `+`, `-`로 직관적으로 합산 (곱연산은 외부 레이어에서 처리)
- **이중 접근 방식** - 필드 이름 직접 접근 + StatId 기반 Dictionary 일괄 처리
- **Reflection 캐싱** - static 생성자에서 한 번만 수집해 반복 비용 제거

## 구조

```
StatId              스탯 고유 ID enum
StatRegistry        StatId <-> string <-> uint 양방향 매핑
StatValue<T>        제네릭 스탯 값 컨테이너 (partial)
 ├─ StatValue.cs            값 관리, MaxValue 검사, decimal 보관
 └─ StatValue.Operators.cs  +, - 연산자 오버로딩 (합산 전용)
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
var def = new StatValue<long>(StatId.Defense, 100, 200L);
def.Value = 999;    // -> 200으로 자동 클램프
def.MaxValue = 50;  // -> ArgumentException
```

### 4. Reflection 필드 캐싱

```csharp
static Stat()
{
    // 최초 한 번만 실행 - StatValue<> 필드를 수집해 캐싱
    _fieldCache = typeof(Stat)
        .GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Where(f => f.FieldType.IsGenericType &&
                    f.FieldType.GetGenericTypeDefinition() == typeof(StatValue<>))
        .ToDictionary(f => f.Name, f => f);
}
```

## 파일 구성

```
Runtime/
├─ StatId.cs               스탯 고유 ID enum
├─ StatRegistry.cs         StatId <-> string <-> uint 양방향 매핑
├─ StatValue.cs            제네릭 스탯 값 컨테이너
├─ StatValue.Operators.cs  +, - 연산자 오버로딩
└─ Stat.cs                 캐릭터 스탯 집합체
Example/
└─ StatExample.cs          Unity MonoBehaviour 사용 예시
```

## 요구 사항

- Unity 2021.2+ (C# 9.0)
