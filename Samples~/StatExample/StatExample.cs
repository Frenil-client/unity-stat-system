using UnityEngine;
using StatSystem;

namespace StatSystem.Example
{
    /// <summary>
    /// StatSystem Unity 사용 예시.
    /// MonoBehaviour에 부착해 실행합니다.
    /// </summary>
    public class StatExample : MonoBehaviour
    {
        private void Start()
        {
            Example_Registry();
            Example_StatSlot();
            Example_Operator();
            Example_StatClass();
            Example_BulkAccess();
            Example_Modifiers();
            Example_ChangeNotification();
            Example_Copy();
        }

        //1. StatRegistry - 이름 <-> UID 양방향 조회
        private void Example_Registry()
        {
            string name  = StatRegistry.GetName(StatId.AttackPower); // "AttackPower"
            uint   uid   = StatRegistry.GetUid(StatId.CriticalRate); // 210
            StatId byUid = StatRegistry.GetId(210u);                 // StatId.CriticalRate
            StatId byStr = StatRegistry.GetId("BossDamage");         // StatId.BossDamage

            Debug.Log($"[Registry] 이름: {name}, UID: {uid}, byUid: {byUid}, byStr: {byStr}");
        }

        //2. StatSlot - 값 관리 및 MaxValue 클램프
        private void Example_StatSlot()
        {
            // 독립적으로 만든 슬롯은 값 타입 지역 변수라 직접 수정할 수 있다.
            var atk = new StatSlot(StatId.AttackPower, 3000L);
            atk.Value += 500;
            Debug.Log($"[StatSlot] AttackPower: {atk.Value}"); // 3500

            // 생성자: StatSlot(StatId id, StatValue value, StatValue maxValue)
            var def = new StatSlot(StatId.Defense, 100L, 200L);
            def.Value = 999; // 200으로 자동 클램프
            Debug.Log($"[Clamp] Defense: {def.Value}"); // 200

            // MaxValue를 현재 Value보다 낮게 설정하면 예외
            try
            {
                def.MaxValue = 50;
            }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning($"[Clamp] 예외 정상: {e.Message}");
            }
        }

        //3. 연산자 오버로딩 - 장비 / 버프 스탯 합산 (+, - 만 지원)
        private void Example_Operator()
        {
            var baseAtk    = new StatSlot(StatId.AttackPower, 500L);
            var equipBonus = new StatSlot(StatId.AttackPower, 200L);
            var buffBonus  = new StatSlot(StatId.AttackPower, 100L);

            var total = baseAtk + equipBonus + buffBonus;
            Debug.Log($"[Operator] 총 공격력: {total.Value}"); // 800

            // 버프 해제 시 차감
            var afterDebuff = total - buffBonus;
            Debug.Log($"[Operator] 버프 해제 후: {afterDebuff.Value}"); // 700
        }

        //4. Stat 클래스 - 쓰기는 SetBaseValue, 읽기는 이름으로 최종값 접근
        private void Example_StatClass()
        {
            var stat = new Stat();

            // stat.AttackPower = 5000; <- 컴파일 에러.
            // 명명 접근자는 최종값을 계산해 돌려주는 읽기 전용 프로퍼티다.
            // 쓰기는 기본값(SetBaseValue)이나 모디파이어(AddModifier) 둘 중 하나로만 들어간다.
            stat.SetBaseValue(StatId.AttackPower, 5000L);

            // Multiplicative 스탯: 여기서는 수치를 합산만 함
            // 실제 최종 공격력 = AttackPower * (1 + AttackPowerPercent) 는 외부에서 계산
            stat.SetBaseValue(StatId.AttackPowerPercent, 0.30);
            stat.SetBaseValue(StatId.CriticalRate,       0.85);
            stat.SetBaseValue(StatId.CriticalDamage,     0.60);
            stat.SetBaseValue(StatId.BossDamage,         2.00);
            stat.SetBaseValue(StatId.IgnoreDefense,      0.93);

            // 읽기는 이름으로 바로 (ref readonly - 복사본이 생기지 않는다)
            Debug.Log($"[Stat] 공격력: {stat.AttackPower}");
            Debug.Log($"[Stat] 공격력%: {stat.AttackPowerPercent.ToDouble():P0}");
            Debug.Log($"[Stat] 크리확률: {stat.CriticalRate.ToDouble():P0}");
            Debug.Log($"[Stat] 보스뎀: {stat.BossDamage.ToDouble():P0}");
            Debug.Log($"[Stat] 방무: {stat.IgnoreDefense.ToDouble():P0}");
        }

        //5. StatId 기반 일괄 처리 - 버프 시스템 연동
        private void Example_BulkAccess()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 5000L);

            // StatId로 직접 접근
            stat.AddBaseValue(StatId.Damage,      0.50);
            stat.AddBaseValue(StatId.FinalDamage, 0.20);
            Debug.Log($"[Bulk] 데미지: {stat.Damage.ToDouble():P0}");
            Debug.Log($"[Bulk] 최종뎀: {stat.FinalDamage.ToDouble():P0}");

            // UID(uint)로 접근 - 데이터 테이블에서 UID만 넘어올 때
            StatId attackId = StatRegistry.GetId(StatRegistry.GetUid(StatId.AttackPower));
            stat.AddBaseValue(attackId, 1000L);
            Debug.Log($"[Bulk] UID 경유 후 공격력: {stat.AttackPower}"); // 6000

            // 이름(string)으로 접근 - 직렬화 / 데이터 파싱 연동 시
            StatId critId = StatRegistry.GetId("CriticalRate");
            stat.AddBaseValue(critId, 0.10);
            Debug.Log($"[Bulk] 크리확률: {stat.CriticalRate.ToDouble():P0}");
        }

        //6. 모디파이어 - 붙였다 뗄 수 있는 보정 (장비 / 버프)
        private void Example_Modifiers()
        {
            var stat = new Stat();
            stat.SetMaxValue(StatId.Defense, 200L);
            stat.SetBaseValue(StatId.Defense, 180L);

            // 장비 하나가 여러 스탯을 올리는 상황
            var armor = new object();   // 실제로는 장비 인스턴스를 넘긴다
            stat.AddModifier(StatId.Defense, StatModifierType.Flat, 50L, armor);
            Debug.Log($"[Modifier] 착용 후 방어력: {stat.Defense}");   // 230이 아니라 상한 200

            // 여기가 핵심이다. 기본값에 +50 하고 나중에 -50 하는 방식이었다면,
            // 상한에 걸려 +20만 반영됐는데 뺄 때는 50을 빼서 150이 됐을 것이다.
            // 모디파이어는 목록에서 제거하고 기본값부터 다시 계산하므로 그런 어긋남이 없다.
            stat.RemoveModifiersFrom(armor);
            Debug.Log($"[Modifier] 해제 후 방어력: {stat.Defense}");   // 180 - 원래대로

            // 적용 순서: (기본 + Flat) × (1 + PercentAdd 합) × Π(1 + PercentMultiply)
            var atk = new Stat();
            atk.SetBaseValue(StatId.AttackPower, 100L);
            atk.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);
            atk.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.30);
            atk.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.20);
            Debug.Log($"[Modifier] (100+50) × 1.5 = {atk.AttackPower}");   // 225

            // 핸들로 하나만 정확히 제거할 수도 있다
            var handle = atk.AddModifier(StatId.AttackPower, StatModifierType.PercentMultiply, 0.10);
            Debug.Log($"[Modifier] × 1.1 = {atk.AttackPower}");            // 247.5
            atk.RemoveModifier(handle);
            Debug.Log($"[Modifier] 핸들 제거 후: {atk.AttackPower}");       // 225

            // 기본값은 모디파이어와 무관하게 그대로다
            Debug.Log($"[Modifier] 기본값: {atk.GetBaseValue(StatId.AttackPower)}");  // 100
        }

        //7. 변경 통지 - UI 바인딩 연동 지점
        private void Example_ChangeNotification()
        {
            var stat = new Stat();

            // 값이 실제로 바뀔 때만, 클램프까지 반영된 최종 값으로 발행된다.
            stat.Changed += (id, value) =>
                Debug.Log($"[Changed] {StatRegistry.GetName(id)} -> {value}");

            stat.SetBaseValue(StatId.AttackPower, 100L); // 발행됨
            stat.SetBaseValue(StatId.AttackPower, 100L); // 값이 같아 발행되지 않음

            stat.SetMaxValue(StatId.Defense, 200L);
            stat.SetBaseValue(StatId.Defense, 999L);     // 200으로 클램프된 값이 발행됨
        }

        //8. 복사 생성자
        private void Example_Copy()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 5000L);
            stat.SetBaseValue(StatId.BossDamage,  2.00);

            var copy = new Stat(stat);
            Debug.Log($"[Copy] 원본과 동일: {stat.IsEqual(copy)}"); // True

            copy.SetBaseValue(StatId.AttackPower, 1L);
            Debug.Log($"[Copy] 수정 후 동일: {stat.IsEqual(copy)}"); // False
        }
    }
}
