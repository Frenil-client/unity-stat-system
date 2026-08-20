using System;
using System.Collections.Generic;

namespace StatSystem
{
    /// <summary>
    /// 캐릭터의 모든 스탯을 보관하고 관리하는 클래스.
    ///
    /// 값은 두 층으로 나뉜다.
    /// - **기본값(base)** — 레벨업이나 강화처럼 되돌리지 않는 영구 수치. StatSlot 배열이 들고 있다
    /// - **모디파이어** — 장비, 버프, 세트 효과처럼 붙였다 뗄 수 있는 보정. 목록으로 들고 있다
    ///
    /// 최종값은 `(기본값 + Flat 합) × (1 + PercentAdd 합) × Π(1 + PercentMultiply)`를
    /// 상한으로 자른 결과이며, 변경이 있을 때마다 다시 계산해 캐시에 넣는다.
    /// 그래서 읽기는 배열 인덱싱 한 번으로 끝나고 할당이 없다.
    ///
    /// 구조:
    /// - StatSlot[] 밀집 배열: 기본값과 상한
    /// - StatValue[] 캐시: 최종값. 쓰기 시점에 갱신되므로 읽기가 O(1)
    /// - StatId -> 인덱스 룩업 테이블: 배열 인덱싱으로 조회
    /// - 명명 접근자: 최종값을 돌려주는 읽기 전용 프로퍼티
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

        private readonly StatSlot[] _slots;   // 기본값 + 상한
        private readonly StatValue[] _final;  // 최종값 캐시
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        private int _nextHandleId;

        /// <summary>
        /// 최종값이 실제로 바뀔 때 (StatId, 새 최종값)으로 발행된다.
        /// 기본값 변경이든 모디파이어 추가/제거든 결과가 같으면 발행되지 않는다.
        /// 복사 생성자는 구독자를 복사하지 않는다 - 구독은 인스턴스마다 독립이다.
        /// </summary>
        public event Action<StatId, StatValue> Changed;

        /// <summary>
        /// 기본값과 상한을 담은 슬롯들. 순서는 StatId 선언 순서와 같다.
        /// **최종값이 아니다** - 모디파이어가 반영된 값은 <see cref="GetValue"/>로 읽는다.
        /// </summary>
        public IReadOnlyList<StatSlot> BaseSlots => _slots;

        /// <summary>현재 걸려 있는 모디파이어 전체. 디버깅과 툴 표시용.</summary>
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        /// <summary>기본 생성자. 모든 스탯을 0으로 초기화합니다.</summary>
        public Stat()
        {
            _slots = new StatSlot[_ids.Length];
            _final = new StatValue[_ids.Length];

            for (int i = 0; i < _ids.Length; i++)
                _slots[i] = new StatSlot(_ids[i], StatValue.Zero);
        }

        /// <summary>복사 생성자. 기본값·상한·모디파이어를 모두 복사하며 Changed 구독자는 복사하지 않습니다.</summary>
        public Stat(Stat source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            _slots = new StatSlot[_ids.Length];
            _final = new StatValue[_ids.Length];

            Array.Copy(source._slots, _slots, _slots.Length);
            Array.Copy(source._final, _final, _final.Length);

            _modifiers.AddRange(source._modifiers);
            _nextHandleId = source._nextHandleId;
        }

        //읽기

        /// <summary>모디파이어까지 반영된 최종값. 미등록 StatId면 StatValue.Zero.</summary>
        public StatValue GetValue(StatId id)
        {
            int index = IndexOf(id);
            return index < 0 ? StatValue.Zero : _final[index];
        }

        /// <summary>모디파이어를 뺀 기본값. 미등록 StatId면 StatValue.Zero.</summary>
        public StatValue GetBaseValue(StatId id)
        {
            int index = IndexOf(id);
            return index < 0 ? StatValue.Zero : _slots[index].Value;
        }

        /// <summary>상한값. 미등록 StatId면 StatValue.Zero.</summary>
        public StatValue GetMaxValue(StatId id)
        {
            int index = IndexOf(id);
            return index < 0 ? StatValue.Zero : _slots[index].MaxValue;
        }

        //기본값 쓰기

        /// <summary>
        /// 기본값을 대입합니다. 상한을 넘으면 슬롯이 클램프하며,
        /// 최종값이 실제로 바뀐 경우에만 Changed가 발행됩니다.
        /// </summary>
        /// <returns>등록된 StatId면 true, 미등록이면 false.</returns>
        public bool SetBaseValue(StatId id, StatValue value)
        {
            int index = IndexOf(id);
            if (index < 0) return false;

            _slots[index].Value = value;
            Refresh(index);
            return true;
        }

        /// <summary>
        /// 기본값에 delta를 더합니다. 음수를 넘기면 차감입니다.
        /// 되돌릴 보정이라면 이 메서드가 아니라 <see cref="AddModifier"/>를 쓸 것.
        /// </summary>
        /// <returns>등록된 StatId면 true, 미등록이면 false.</returns>
        public bool AddBaseValue(StatId id, StatValue delta)
        {
            int index = IndexOf(id);
            if (index < 0) return false;

            _slots[index].Value = _slots[index].Value + delta;
            Refresh(index);
            return true;
        }

        /// <summary>
        /// 상한을 설정합니다. 현재 기본값보다 낮은 상한은 슬롯이 예외로 거부합니다.
        /// </summary>
        /// <returns>등록된 StatId면 true, 미등록이면 false.</returns>
        public bool SetMaxValue(StatId id, StatValue maxValue)
        {
            int index = IndexOf(id);
            if (index < 0) return false;

            _slots[index].MaxValue = maxValue;
            Refresh(index);
            return true;
        }

        //모디파이어

        /// <summary>
        /// 모디파이어를 추가하고 그것을 가리키는 핸들을 돌려줍니다.
        /// 미등록 StatId면 아무것도 추가하지 않고 무효 핸들을 돌려줍니다.
        /// </summary>
        /// <param name="source">
        /// 나중에 <see cref="RemoveModifiersFrom"/>로 한꺼번에 떼어낼 때 쓰는 주체.
        /// 장비나 버프 인스턴스를 그대로 넘기면 된다.
        /// </param>
        public ModifierHandle AddModifier(StatId id, StatModifierType type, StatValue value, object source = null)
        {
            int index = IndexOf(id);
            if (index < 0) return default;

            var handle = new ModifierHandle(++_nextHandleId);
            _modifiers.Add(new StatModifier(handle, id, type, value, source));
            Refresh(index);
            return handle;
        }

        /// <summary>핸들이 가리키는 모디파이어 하나를 제거합니다.</summary>
        /// <returns>실제로 제거했으면 true.</returns>
        public bool RemoveModifier(ModifierHandle handle)
        {
            if (!handle.IsValid) return false;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Handle != handle) continue;

                StatId id = _modifiers[i].Id;
                _modifiers.RemoveAt(i);
                Refresh(IndexOf(id));
                return true;
            }

            return false;
        }

        /// <summary>
        /// 특정 주체가 붙인 모디파이어를 모두 제거합니다. 장비 해제나 버프 종료에 쓴다.
        /// 주체는 참조 동일성으로 비교합니다.
        /// </summary>
        /// <returns>제거된 개수.</returns>
        public int RemoveModifiersFrom(object source)
        {
            if (source == null) return 0;

            int removed = 0;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_modifiers[i].Source, source)) continue;

                _modifiers.RemoveAt(i);
                removed++;
            }

            // 여러 스탯이 한꺼번에 영향받을 수 있어 전체를 다시 계산한다.
            // 스탯 수가 고정(현재 18)이라 비용이 예측 가능하다.
            if (removed > 0) RefreshAll();
            return removed;
        }

        /// <summary>모든 모디파이어를 제거합니다. 기본값은 그대로 둡니다.</summary>
        public void ClearModifiers()
        {
            if (_modifiers.Count == 0) return;

            _modifiers.Clear();
            RefreshAll();
        }

        /// <summary>모든 스탯의 최종값이 같은지 비교합니다.</summary>
        public bool IsEqual(Stat other)
        {
            if (other == null) return false;

            for (int i = 0; i < _final.Length; i++)
            {
                if (_final[i] != other._final[i]) return false;
            }
            return true;
        }

        //이름으로 최종값 읽기 (쓰기는 SetBaseValue / AddModifier 사용)

        public StatValue AttackPower        => GetValue(StatId.AttackPower);
        public StatValue MagicAttack        => GetValue(StatId.MagicAttack);
        public StatValue AttackPowerPercent => GetValue(StatId.AttackPowerPercent);
        public StatValue MagicAttackPercent => GetValue(StatId.MagicAttackPercent);
        public StatValue Defense            => GetValue(StatId.Defense);
        public StatValue DefensePercent     => GetValue(StatId.DefensePercent);

        public StatValue Damage         => GetValue(StatId.Damage);
        public StatValue FinalDamage    => GetValue(StatId.FinalDamage);
        public StatValue CriticalRate   => GetValue(StatId.CriticalRate);
        public StatValue CriticalDamage => GetValue(StatId.CriticalDamage);

        public StatValue StatusResistance => GetValue(StatId.StatusResistance);

        public StatValue BossDamage          => GetValue(StatId.BossDamage);
        public StatValue IgnoreDefense       => GetValue(StatId.IgnoreDefense);
        public StatValue NormalMonsterDamage => GetValue(StatId.NormalMonsterDamage);
        public StatValue IgnoreElemental     => GetValue(StatId.IgnoreElemental);

        public StatValue MoveSpeed   => GetValue(StatId.MoveSpeed);
        public StatValue JumpPower   => GetValue(StatId.JumpPower);
        public StatValue AttackSpeed => GetValue(StatId.AttackSpeed);

        //내부 구현

        private static int IndexOf(StatId id)
        {
            uint raw = (uint)id;
            return raw <= _maxRawId ? _idToIndex[raw] : -1;
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _final.Length; i++)
                Refresh(i);
        }

        // 최종값을 다시 계산하고, 실제로 바뀐 경우에만 통지한다.
        private void Refresh(int index)
        {
            StatValue before = _final[index];
            StatValue after = Compute(index);
            if (after == before) return;

            _final[index] = after;
            Changed?.Invoke(_ids[index], after);
        }

        /// <summary>
        /// 기본값에 모디파이어를 접어 최종값을 만든다.
        ///
        /// 목록을 종류별로 세 번 훑는다. 한 번에 처리하려면 목록이 Type 순으로 정렬되어
        /// 있어야 하는데, 그 불변식을 유지하는 비용보다 세 번 훑는 편이 읽기 쉽고
        /// 순서 실수도 생기지 않는다. List의 struct 열거자를 쓰므로 할당은 없다.
        ///
        /// 비용은 모디파이어 전체 개수에 비례한다. 수십 개 규모를 전제한 선택이며,
        /// 수천 개가 되면 StatId별 버킷으로 나눠야 한다.
        /// </summary>
        private StatValue Compute(int index)
        {
            StatId id = _ids[index];
            StatValue result = _slots[index].Value;

            foreach (var modifier in _modifiers)
            {
                if (modifier.Id == id && modifier.Type == StatModifierType.Flat)
                    result += modifier.Value;
            }

            StatValue percentAdd = StatValue.Zero;
            foreach (var modifier in _modifiers)
            {
                if (modifier.Id == id && modifier.Type == StatModifierType.PercentAdd)
                    percentAdd += modifier.Value;
            }

            if (percentAdd != StatValue.Zero)
                result += result * percentAdd;

            foreach (var modifier in _modifiers)
            {
                if (modifier.Id == id && modifier.Type == StatModifierType.PercentMultiply)
                    result += result * modifier.Value;
            }

            // 상한은 최종값에만 적용한다. 기본값 자체는 슬롯이 이미 상한으로 잘라 둔다.
            StatValue max = _slots[index].MaxValue;
            return max.CompareTo(result) < 0 ? max : result;
        }
    }
}
