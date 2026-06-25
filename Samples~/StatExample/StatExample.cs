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
            Example_StatValue();
            Example_Operator();
            Example_StatClass();
            Example_BulkAccess();
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

        //2. StatValue<T> - 값 관리 및 MaxValue 클램프 
        private void Example_StatValue()
        {
            var atk = new StatValue<long>(StatId.AttackPower, 3000);
            atk.Value += 500;
            Debug.Log($"[StatValue] AttackPower: {atk.Value}"); // 3500

            // 생성자: StatValue(StatId id, T value, T maxValue)
            var def = new StatValue<long>(StatId.Defense, 100, 200L);
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
            var baseAtk    = new StatValue<long>(StatId.AttackPower, 500);
            var equipBonus = new StatValue<long>(StatId.AttackPower, 200);
            var buffBonus  = new StatValue<long>(StatId.AttackPower, 100);

            var total = baseAtk + equipBonus + buffBonus;
            Debug.Log($"[Operator] 총 공격력: {total.Value}"); // 800

            // 버프 해제 시 차감
            var afterDebuff = total - buffBonus;
            Debug.Log($"[Operator] 버프 해제 후: {afterDebuff.Value}"); // 700
        }

        //4. Stat 클래스 - 필드 이름으로 직접 접근 
        private void Example_StatClass()
        {
            var stat = new Stat();

            stat.AttackPower.Value        = 5000;
            // Multiplicative 스탯: 여기서는 수치를 합산만 함
            // 실제 최종 공격력 = AttackPower * (1 + AttackPowerPercent) 는 외부에서 계산
            stat.AttackPowerPercent.Value = 0.30;
            stat.CriticalRate.Value       = 0.85;
            stat.CriticalDamage.Value     = 0.60;
            stat.BossDamage.Value         = 2.00;
            stat.IgnoreDefense.Value      = 0.93;

            Debug.Log($"[Stat] 공격력: {stat.AttackPower.Value}");
            Debug.Log($"[Stat] 공격력%: {stat.AttackPowerPercent.Value:P0}");
            Debug.Log($"[Stat] 크리확률: {stat.CriticalRate.Value:P0}");
            Debug.Log($"[Stat] 보스뎀: {stat.BossDamage.Value:P0}");
            Debug.Log($"[Stat] 방무: {stat.IgnoreDefense.Value:P0}");
        }

        //5. StatId 기반 일괄 처리 - 버프 시스템 연동 
        private void Example_BulkAccess()
        {
            var stat = new Stat();
            stat.AttackPower.Value = 5000;

            // StatId로 직접 접근
            stat.AddValue(StatId.Damage,      0.50);
            stat.AddValue(StatId.FinalDamage, 0.20);
            Debug.Log($"[Bulk] 데미지: {stat.Damage.Value:P0}");
            Debug.Log($"[Bulk] 최종뎀: {stat.FinalDamage.Value:P0}");

            // UID(uint)로 접근 - 데이터 테이블에서 UID만 넘어올 때
            StatId attackId = StatRegistry.GetId(StatRegistry.GetUid(StatId.AttackPower));
            stat.AddValue(attackId, 1000);
            Debug.Log($"[Bulk] UID 경유 후 공격력: {stat.AttackPower.Value}"); // 6000

            // 이름(string)으로 접근 - 직렬화 / 데이터 파싱 연동 시
            StatId critId = StatRegistry.GetId("CriticalRate");
            stat.AddValue(critId, 0.10);
            Debug.Log($"[Bulk] 크리확률: {stat.CriticalRate.Value:P0}");
        }

        //6. 복사 생성자 
        private void Example_Copy()
        {
            var stat = new Stat();
            stat.AttackPower.Value = 5000;
            stat.BossDamage.Value  = 2.00;

            var copy = new Stat(stat);
            Debug.Log($"[Copy] 원본과 동일: {stat.IsEqual(copy)}"); // True

            copy.AttackPower.Value = 1;
            Debug.Log($"[Copy] 수정 후 동일: {stat.IsEqual(copy)}"); // False
        }
    }
}
