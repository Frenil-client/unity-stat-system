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

        //4. Stat 클래스 - 쓰기는 SetValue, 읽기는 이름으로 직접 접근
        private void Example_StatClass()
        {
            var stat = new Stat();

            // stat.AttackPower.Value = 5000; <- 컴파일 에러.
            // 명명 접근자는 ref readonly라 쓰기가 막혀 있다. 쓰기 경로를 SetValue 하나로
            // 좁혀야 MaxValue 클램프와 Changed 통지를 우회할 수 없다.
            stat.SetValue(StatId.AttackPower, 5000L);

            // Multiplicative 스탯: 여기서는 수치를 합산만 함
            // 실제 최종 공격력 = AttackPower * (1 + AttackPowerPercent) 는 외부에서 계산
            stat.SetValue(StatId.AttackPowerPercent, 0.30);
            stat.SetValue(StatId.CriticalRate,       0.85);
            stat.SetValue(StatId.CriticalDamage,     0.60);
            stat.SetValue(StatId.BossDamage,         2.00);
            stat.SetValue(StatId.IgnoreDefense,      0.93);

            // 읽기는 이름으로 바로 (ref readonly - 복사본이 생기지 않는다)
            Debug.Log($"[Stat] 공격력: {stat.AttackPower.Value}");
            Debug.Log($"[Stat] 공격력%: {stat.AttackPowerPercent.Value.ToDouble():P0}");
            Debug.Log($"[Stat] 크리확률: {stat.CriticalRate.Value.ToDouble():P0}");
            Debug.Log($"[Stat] 보스뎀: {stat.BossDamage.Value.ToDouble():P0}");
            Debug.Log($"[Stat] 방무: {stat.IgnoreDefense.Value.ToDouble():P0}");
        }

        //5. StatId 기반 일괄 처리 - 버프 시스템 연동
        private void Example_BulkAccess()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 5000L);

            // StatId로 직접 접근
            stat.AddValue(StatId.Damage,      0.50);
            stat.AddValue(StatId.FinalDamage, 0.20);
            Debug.Log($"[Bulk] 데미지: {stat.Damage.Value.ToDouble():P0}");
            Debug.Log($"[Bulk] 최종뎀: {stat.FinalDamage.Value.ToDouble():P0}");

            // UID(uint)로 접근 - 데이터 테이블에서 UID만 넘어올 때
            StatId attackId = StatRegistry.GetId(StatRegistry.GetUid(StatId.AttackPower));
            stat.AddValue(attackId, 1000L);
            Debug.Log($"[Bulk] UID 경유 후 공격력: {stat.AttackPower.Value}"); // 6000

            // 이름(string)으로 접근 - 직렬화 / 데이터 파싱 연동 시
            StatId critId = StatRegistry.GetId("CriticalRate");
            stat.AddValue(critId, 0.10);
            Debug.Log($"[Bulk] 크리확률: {stat.CriticalRate.Value.ToDouble():P0}");
        }

        //6. 변경 통지 - UI 바인딩 연동 지점
        private void Example_ChangeNotification()
        {
            var stat = new Stat();

            // 값이 실제로 바뀔 때만, 클램프까지 반영된 최종 값으로 발행된다.
            stat.Changed += (id, value) =>
                Debug.Log($"[Changed] {StatRegistry.GetName(id)} -> {value}");

            stat.SetValue(StatId.AttackPower, 100L); // 발행됨
            stat.SetValue(StatId.AttackPower, 100L); // 값이 같아 발행되지 않음

            stat.SetMaxValue(StatId.Defense, 200L);
            stat.SetValue(StatId.Defense, 999L);     // 200으로 클램프된 값이 발행됨
        }

        //7. 복사 생성자
        private void Example_Copy()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 5000L);
            stat.SetValue(StatId.BossDamage,  2.00);

            var copy = new Stat(stat);
            Debug.Log($"[Copy] 원본과 동일: {stat.IsEqual(copy)}"); // True

            copy.SetValue(StatId.AttackPower, 1L);
            Debug.Log($"[Copy] 수정 후 동일: {stat.IsEqual(copy)}"); // False
        }
    }
}
