using System;
using System.Collections.Generic;

namespace StatSystem
{
    /// <summary>
    /// 캐릭터의 모든 스탯을 보관하고 관리하는 클래스.
    ///
    /// 구조:
    /// - StatSlot[] 밀집 배열: 슬롯 실체는 여기 하나뿐이고, 모든 접근이 이 배열을 가리킨다
    /// - StatId -> 인덱스 룩업 테이블: 일괄 처리(버프 합산, 전투력 계산)를 배열 인덱싱으로 처리
    /// - 명명 접근자: ref readonly 프로퍼티라 복사 없이 읽히고, 쓰기는 컴파일 단계에서 막힌다
    ///
    /// 쓰기 경로는 SetValue / AddValue 하나로 좁혀져 있다. 슬롯을 직접 대입할 수 있으면
    /// MaxValue 클램프와 Changed 통지를 조용히 건너뛸 수 있어서, 값 타입 접근자를
    /// 읽기 전용으로 노출해 타입 수준에서 차단했다.
    /// </summary>
    public partial class Stat
    {
        //StatId -> 인덱스 룩업. Dictionary<StatId, T>를 쓰지 않는 이유는,
        //Unity의 Mono/IL2CPP에서 enum 키의 EqualityComparer<T>.Default가
        //boxing 경로로 떨어지는 경우가 있어 조회마다 힙 할당이 생기기 때문이다.
        //StatId는 값이 희소하지만 상한이 작아(현재 502) 평면 배열로 충분하다.
        private static readonly StatId[] _ids;
        private static readonly int[] _idToIndex;
        private static readonly uint _maxRawId;

        static Stat()
        {
            _ids = (StatId[])Enum.GetValues(typeof(StatId));

            uint max = 0;
            foreach (var id in _ids)
            {
                if ((uint)id > max) max = (uint)id;
            }
            _maxRawId = max;

            _idToIndex = new int[max + 1];
            for (int i = 0; i < _idToIndex.Length; i++)
                _idToIndex[i] = -1;

            for (int i = 0; i < _ids.Length; i++)
                _idToIndex[(uint)_ids[i]] = i;
        }

        private readonly StatSlot[] _slots;

        /// <summary>
        /// 스탯 값이 실제로 바뀔 때 (StatId, 클램프까지 반영된 최종 값)으로 발행된다.
        /// 같은 값을 다시 대입하면 발행되지 않는다.
        /// 복사 생성자는 구독자를 복사하지 않는다 - 구독은 인스턴스마다 독립이다.
        /// </summary>
        public event Action<StatId, StatValue> Changed;

        /// <summary>등록된 모든 슬롯. 순서는 StatId 선언 순서와 같다.</summary>
        public IReadOnlyList<StatSlot> Slots => _slots;

        /// <summary>기본 생성자. 모든 스탯을 0으로 초기화합니다.</summary>
        public Stat()
        {
            _slots = new StatSlot[_ids.Length];
            for (int i = 0; i < _ids.Length; i++)
                _slots[i] = new StatSlot(_ids[i], StatValue.Zero);
        }

        /// <summary>복사 생성자. 값만 복사하며 Changed 구독자는 복사하지 않습니다.</summary>
        public Stat(Stat source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            _slots = new StatSlot[_ids.Length];
            Array.Copy(source._slots, _slots, _slots.Length);
        }

        //StatId 기반 접근 API

        /// <summary>미등록 StatId면 StatValue.Zero를 반환합니다.</summary>
        public StatValue GetValue(StatId id)
        {
            int index = IndexOf(id);
            return index < 0 ? StatValue.Zero : _slots[index].Value;
        }

        /// <summary>
        /// 값을 대입합니다. MaxValue를 넘으면 슬롯이 클램프하며,
        /// 클램프 후 값이 실제로 바뀐 경우에만 Changed가 발행됩니다.
        /// </summary>
        /// <returns>등록된 StatId면 true, 미등록이면 false.</returns>
        public bool SetValue(StatId id, StatValue value)
        {
            int index = IndexOf(id);
            if (index < 0) return false;

            StatValue before = _slots[index].Value;
            _slots[index].Value = value;
            NotifyIfChanged(id, index, before);
            return true;
        }

        /// <summary>
        /// 현재 값에 delta를 더합니다. 음수를 넘기면 차감입니다.
        /// </summary>
        /// <returns>등록된 StatId면 true, 미등록이면 false.</returns>
        public bool AddValue(StatId id, StatValue delta)
        {
            int index = IndexOf(id);
            if (index < 0) return false;

            StatValue before = _slots[index].Value;
            _slots[index].Value = before + delta;
            NotifyIfChanged(id, index, before);
            return true;
        }

        /// <summary>
        /// 스탯의 상한을 설정합니다. 현재 값보다 낮은 상한은 슬롯이 예외로 거부합니다.
        /// </summary>
        /// <returns>등록된 StatId면 true, 미등록이면 false.</returns>
        public bool SetMaxValue(StatId id, StatValue maxValue)
        {
            int index = IndexOf(id);
            if (index < 0) return false;

            _slots[index].MaxValue = maxValue;
            return true;
        }

        /// <summary>모든 슬롯의 값이 같은지 비교합니다. MaxValue는 비교 대상이 아닙니다.</summary>
        public bool IsEqual(Stat other)
        {
            if (other == null) return false;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Value != other._slots[i].Value) return false;
            }
            return true;
        }

        //이름으로 직접 접근 (읽기 전용 - 쓰기는 SetValue / AddValue 사용)

        public ref readonly StatSlot AttackPower        => ref Slot(StatId.AttackPower);
        public ref readonly StatSlot MagicAttack        => ref Slot(StatId.MagicAttack);
        public ref readonly StatSlot AttackPowerPercent => ref Slot(StatId.AttackPowerPercent);
        public ref readonly StatSlot MagicAttackPercent => ref Slot(StatId.MagicAttackPercent);
        public ref readonly StatSlot Defense            => ref Slot(StatId.Defense);
        public ref readonly StatSlot DefensePercent     => ref Slot(StatId.DefensePercent);

        public ref readonly StatSlot Damage         => ref Slot(StatId.Damage);
        public ref readonly StatSlot FinalDamage    => ref Slot(StatId.FinalDamage);
        public ref readonly StatSlot CriticalRate   => ref Slot(StatId.CriticalRate);
        public ref readonly StatSlot CriticalDamage => ref Slot(StatId.CriticalDamage);

        public ref readonly StatSlot StatusResistance => ref Slot(StatId.StatusResistance);

        public ref readonly StatSlot BossDamage          => ref Slot(StatId.BossDamage);
        public ref readonly StatSlot IgnoreDefense       => ref Slot(StatId.IgnoreDefense);
        public ref readonly StatSlot NormalMonsterDamage => ref Slot(StatId.NormalMonsterDamage);
        public ref readonly StatSlot IgnoreElemental     => ref Slot(StatId.IgnoreElemental);

        public ref readonly StatSlot MoveSpeed   => ref Slot(StatId.MoveSpeed);
        public ref readonly StatSlot JumpPower   => ref Slot(StatId.JumpPower);
        public ref readonly StatSlot AttackSpeed => ref Slot(StatId.AttackSpeed);

        //내부 구현

        //명명 접근자 전용 경로. StatId가 enum에 정의된 값임이 보장되므로 -1 검사가 없다.
        private ref StatSlot Slot(StatId id) => ref _slots[_idToIndex[(uint)id]];

        private static int IndexOf(StatId id)
        {
            uint raw = (uint)id;
            return raw <= _maxRawId ? _idToIndex[raw] : -1;
        }

        private void NotifyIfChanged(StatId id, int index, StatValue before)
        {
            //클램프 때문에 대입한 값과 저장된 값이 다를 수 있어 슬롯에서 다시 읽는다.
            StatValue after = _slots[index].Value;
            if (after != before)
                Changed?.Invoke(id, after);
        }
    }
}
