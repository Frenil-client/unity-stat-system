using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StatSystem.Benchmarks
{
    /// <summary>
    /// 캐릭터의 모든 스탯을 보관하고 관리하는 클래스.
    ///
    /// 구조:
    /// - public 필드: 이름으로 직접 접근 (stat.AttackPower.Value += 100)
    /// - Dictionary: StatId 기반 일괄 처리 (버프 합산, 전투력 계산 등)
    /// - Reflection 캐싱: 초기화 비용을 static 생성자에서 한 번만 지불
    /// </summary>
    public partial class LegacyReflectionStat
    {
        //공격력 / 마력
        public StatSlot AttackPower;
        public StatSlot MagicAttack;
        public StatSlot AttackPowerPercent;
        public StatSlot MagicAttackPercent;
        public StatSlot Defense;
        public StatSlot DefensePercent;

        //데미지 / 크리티컬
        public StatSlot Damage;            // 일반 데미지 %
        public StatSlot FinalDamage;       // 최종 데미지 %
        public StatSlot CriticalRate;      // 크리티컬 확률 %
        public StatSlot CriticalDamage;    // 크리티컬 데미지 %

        //방어 / 생존
        public StatSlot StatusResistance;

        //보스 / 방어율 무시
        public StatSlot BossDamage;            // 보스 데미지 %
        public StatSlot IgnoreDefense;         // 방어율 무시 %
        public StatSlot NormalMonsterDamage;   // 일반 몬스터 데미지 %
        public StatSlot IgnoreElemental;       // 속성 내성 무시 %

        //이동 / 기타
        public StatSlot MoveSpeed;
        public StatSlot JumpPower;
        public StatSlot AttackSpeed;       // 공격 속도 단계 (1~8)

        //StatId -> FieldInfo 매핑 (일괄 처리용). 값의 복사본이 아니라 필드 위치를 캐싱하므로
        //stat.AttackPower.Value = x 같은 직접 수정에도 항상 최신 값을 반영한다.
        private readonly Dictionary<StatId, FieldInfo> _statFields = new();

        //Reflection 필드 캐싱 (static - 한 번만 초기화)
        private static readonly Dictionary<string, FieldInfo> _fieldCache;

        static LegacyReflectionStat()
        {
            _fieldCache = typeof(LegacyReflectionStat)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(StatSlot))
                .ToDictionary(f => f.Name, f => f);
        }

        /// <summary>기본 생성자. 모든 스탯을 0으로 초기화합니다.</summary>
        public LegacyReflectionStat() => Init();

        /// <summary>복사 생성자.</summary>
        public LegacyReflectionStat(LegacyReflectionStat source)
        {
            Init();
            foreach (var kv in _statFields)
                kv.Value.SetValue(this, kv.Value.GetValue(source));
        }

        private void Init()
        {
            AttackPower        = new StatSlot(StatId.AttackPower,        0L);
            MagicAttack        = new StatSlot(StatId.MagicAttack,        0L);
            AttackPowerPercent = new StatSlot(StatId.AttackPowerPercent, 0.0);
            MagicAttackPercent = new StatSlot(StatId.MagicAttackPercent, 0.0);
            Defense            = new StatSlot(StatId.Defense,            0L);
            DefensePercent     = new StatSlot(StatId.DefensePercent,     0.0);

            Damage         = new StatSlot(StatId.Damage,         0.0);
            FinalDamage    = new StatSlot(StatId.FinalDamage,    0.0);
            CriticalRate   = new StatSlot(StatId.CriticalRate,   0.0);
            CriticalDamage = new StatSlot(StatId.CriticalDamage, 0.0);

            StatusResistance = new StatSlot(StatId.StatusResistance, 0L);

            BossDamage          = new StatSlot(StatId.BossDamage,          0.0);
            IgnoreDefense       = new StatSlot(StatId.IgnoreDefense,       0.0);
            NormalMonsterDamage = new StatSlot(StatId.NormalMonsterDamage, 0.0);
            IgnoreElemental     = new StatSlot(StatId.IgnoreElemental,     0.0);

            MoveSpeed   = new StatSlot(StatId.MoveSpeed,   0L);
            JumpPower   = new StatSlot(StatId.JumpPower,   0L);
            AttackSpeed = new StatSlot(StatId.AttackSpeed, 0L);

            BuildFieldIndex();
        }

        /// <summary>
        /// Reflection 캐시로 StatId -> FieldInfo Dictionary를 구성합니다.
        /// UID 기반 일괄 처리(버프 합산, 전투력 계산 등)에 사용됩니다.
        /// </summary>
        private void BuildFieldIndex()
        {
            _statFields.Clear();
            foreach (var fi in _fieldCache.Values)
            {
                var slot = (StatSlot)fi.GetValue(this);
                _statFields[slot.Id] = fi;
            }
        }

        //StatId 기반 접근 API

        public double GetValue(StatId id)
        {
            if (!_statFields.TryGetValue(id, out var fi)) return 0;
            return ((StatSlot)fi.GetValue(this)).Value.ToDouble();
        }

        public bool SetValue(StatId id, double value)
        {
            if (!_statFields.TryGetValue(id, out var fi)) return false;
            var slot = (StatSlot)fi.GetValue(this);
            slot.Value = StatValue.FromFloat(value);
            fi.SetValue(this, slot);
            return true;
        }

        public bool AddValue(StatId id, double value)
        {
            if (!_statFields.TryGetValue(id, out var fi)) return false;
            var slot = (StatSlot)fi.GetValue(this);
            slot.Value = slot.Value + StatValue.FromFloat(value);
            fi.SetValue(this, slot);
            return true;
        }

        /// <summary>등록된 모든 StatId -> StatSlot 스냅샷.</summary>
        public Dictionary<StatId, StatSlot> GetAllStats()
        {
            var result = new Dictionary<StatId, StatSlot>();
            foreach (var kv in _statFields)
                result[kv.Key] = (StatSlot)kv.Value.GetValue(this);
            return result;
        }

        public bool IsEqual(LegacyReflectionStat other)
        {
            foreach (var kv in _statFields)
            {
                var mine   = (StatSlot)kv.Value.GetValue(this);
                var theirs = (StatSlot)kv.Value.GetValue(other);
                if (mine.Value != theirs.Value) return false;
            }
            return true;
        }
    }
}
