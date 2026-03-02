using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StatSystem
{
    /// <summary>
    /// 캐릭터의 모든 스탯을 보관하고 관리하는 클래스.
    ///
  /// 구조:
    /// - public 필드: 이름으로 직접 접근 (stat.AttackPower.Value += 100)
    /// - Dictionary: StatId 기반 일괄 처리 (버프 합산, 전투력 계산 등)
    /// - Reflection 캐싱: 초기화 비용을 static 생성자에서 한 번만 지불
    /// </summary>
    public partial class Stat
    {
        //공격력 / 마력 
        public StatValue<long>   AttackPower;
        public StatValue<long>   MagicAttack;
        public StatValue<double> AttackPowerPercent;
        public StatValue<double> MagicAttackPercent;
        public StatValue<long>   Defense;
        public StatValue<double> DefensePercent;

        //데미지 / 크리티컬 
        public StatValue<double> Damage;            // 일반 데미지 %
        public StatValue<double> FinalDamage;       // 최종 데미지 %
        public StatValue<double> CriticalRate;      // 크리티컬 확률 %
        public StatValue<double> CriticalDamage;    // 크리티컬 데미지 %

        //방어 / 생존 
        public StatValue<long>   StatusResistance;

        //보스 / 방어율 무시 
        public StatValue<double> BossDamage;            // 보스 데미지 %
        public StatValue<double> IgnoreDefense;         // 방어율 무시 %
        public StatValue<double> NormalMonsterDamage;   // 일반 몬스터 데미지 %
        public StatValue<double> IgnoreElemental;       // 속성 내성 무시 %

        //이동 / 기타 
        public StatValue<long>   MoveSpeed;
        public StatValue<long>   JumpPower;
        public StatValue<long>   AttackSpeed;       // 공격 속도 단계 (1~8)

        //StatId 기반 Dictionary (일괄 처리용) 
        private Dictionary<StatId, StatValue<long>>   _longStats   = new();
        private Dictionary<StatId, StatValue<double>> _doubleStats = new();

        //Reflection 필드 캐싱 (static - 한 번만 초기화) 
        private static readonly Dictionary<string, FieldInfo> _fieldCache;

        static Stat()
        {
            _fieldCache = typeof(Stat)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType.IsGenericType &&
                            f.FieldType.GetGenericTypeDefinition() == typeof(StatValue<>))
                .ToDictionary(f => f.Name, f => f);
        }

        /// <summary>기본 생성자. 모든 스탯을 0으로 초기화합니다.</summary>
        public Stat() => Init();

        /// <summary>복사 생성자.</summary>
        public Stat(Stat source)
        {
            Init();
            foreach (var kv in _longStats)
                kv.Value.Value = source._longStats[kv.Key].Value;
            foreach (var kv in _doubleStats)
                kv.Value.Value = source._doubleStats[kv.Key].Value;
        }

        private void Init()
        {
            AttackPower        = new StatValue<long>(StatId.AttackPower,        0);
            MagicAttack        = new StatValue<long>(StatId.MagicAttack,        0);
            AttackPowerPercent = new StatValue<double>(StatId.AttackPowerPercent, 0);
            MagicAttackPercent = new StatValue<double>(StatId.MagicAttackPercent, 0);
            Defense            = new StatValue<long>(StatId.Defense,            0);
            DefensePercent     = new StatValue<double>(StatId.DefensePercent,   0);

            Damage         = new StatValue<double>(StatId.Damage,         0);
            FinalDamage    = new StatValue<double>(StatId.FinalDamage,    0);
            CriticalRate   = new StatValue<double>(StatId.CriticalRate,   0);
            CriticalDamage = new StatValue<double>(StatId.CriticalDamage, 0);

            StatusResistance = new StatValue<long>(StatId.StatusResistance, 0);

            BossDamage          = new StatValue<double>(StatId.BossDamage,          0);
            IgnoreDefense       = new StatValue<double>(StatId.IgnoreDefense,       0);
            NormalMonsterDamage = new StatValue<double>(StatId.NormalMonsterDamage, 0);
            IgnoreElemental     = new StatValue<double>(StatId.IgnoreElemental,     0);

            MoveSpeed       = new StatValue<long>(StatId.MoveSpeed,   0);
            JumpPower       = new StatValue<long>(StatId.JumpPower,   0);
            AttackSpeed     = new StatValue<long>(StatId.AttackSpeed, 0);

            BuildDictionary();
        }

        /// <summary>
        /// Reflection 캐시로 StatId -> StatValue Dictionary를 구성합니다.
        /// UID 기반 일괄 처리(버프 합산, 전투력 계산 등)에 사용됩니다.
        /// </summary>
        private void BuildDictionary()
        {
            _longStats.Clear();
            _doubleStats.Clear();

            foreach (var kv in _fieldCache)
            {
                var v = kv.Value.GetValue(this);
                if (v is StatValue<long>   ls) _longStats[ls.Id]   = ls;
                else if (v is StatValue<double> ds) _doubleStats[ds.Id] = ds;
            }
        }

        //StatId 기반 접근 API 

        public double GetValue(StatId id)
        {
            if (_longStats.TryGetValue(id, out var ls))   return ls.Value;
            if (_doubleStats.TryGetValue(id, out var ds)) return ds.Value;
            return 0;
        }

        public bool SetValue(StatId id, double value)
        {
            if (_longStats.TryGetValue(id, out var ls))   { ls.Value = (long)value; return true; }
            if (_doubleStats.TryGetValue(id, out var ds)) { ds.Value = value;        return true; }
            return false;
        }

        public bool AddValue(StatId id, double value)
        {
            if (_longStats.TryGetValue(id, out var ls))   { ls.Value += (long)value; return true; }
            if (_doubleStats.TryGetValue(id, out var ds)) { ds.Value += value;        return true; }
            return false;
        }

        public Dictionary<StatId, StatValue<long>>   GetLongStats()   => new(_longStats);
        public Dictionary<StatId, StatValue<double>> GetDoubleStats() => new(_doubleStats);

        public bool IsEqual(Stat other)
        {
            foreach (var kv in _longStats)
                if (kv.Value.Value != other._longStats[kv.Key].Value) return false;
            foreach (var kv in _doubleStats)
                if (Math.Abs(kv.Value.Value - other._doubleStats[kv.Key].Value) > double.Epsilon) return false;
            return true;
        }
    }
}
