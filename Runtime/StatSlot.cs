using System;

namespace StatSystem
{
    /// <summary>
    /// StatId + 값 + 최대값을 함께 관리하는 스탯 슬롯.
    /// 값 표현은 StatValue(고정소수점) 하나라 수치 타입별 분기가 없고,
    /// MaxValue 초과 시 자동 클램프, MaxValue 역전 시 예외로 무결성을 지킨다.
    ///
    /// 읽기 멤버에는 전부 readonly가 붙어 있다. Stat이 슬롯을 ref readonly로 노출하는데,
    /// readonly가 없으면 컴파일러가 멤버 호출마다 방어적 복사본을 만들어
    /// ref로 넘긴 이득이 사라지기 때문이다.
    /// </summary>
    public struct StatSlot
    {
        public StatId Id { get; }

        private StatValue _value;
        private StatValue _maxValue;

        /// <summary>StatId를 uint UID로 변환.</summary>
        public readonly uint StatUid => (uint)Id;

        /// <summary>스탯 값. MaxValue를 초과하면 자동으로 MaxValue로 클램프된다.</summary>
        public StatValue Value
        {
            readonly get => _value;
            set => _value = _maxValue.CompareTo(value) < 0 ? _maxValue : value;
        }

        /// <summary>스탯 최대값. 현재 Value보다 낮게 설정하면 예외를 던진다.</summary>
        public StatValue MaxValue
        {
            readonly get => _maxValue;
            set
            {
                if (value.CompareTo(_value) < 0)
                    throw new ArgumentException($"MaxValue({value})는 현재 Value({_value})보다 작을 수 없습니다.");
                _maxValue = value;
            }
        }

        /// <summary>StatId, 초기값, 최대값으로 생성한다.</summary>
        public StatSlot(StatId id, StatValue value, StatValue maxValue)
        {
            if (maxValue.CompareTo(value) < 0)
                throw new ArgumentException($"MaxValue({maxValue})는 Value({value})보다 작을 수 없습니다.");

            Id = id;
            _maxValue = maxValue;
            _value = value;
        }

        /// <summary>StatId와 초기값으로 생성한다. 최대값은 StatValue.MaxValue(사실상 무제한)로 설정된다.</summary>
        public StatSlot(StatId id, StatValue value) : this(id, value, StatValue.MaxValue)
        {
        }

        public static StatSlot operator +(StatSlot a, StatSlot b) =>
            new StatSlot(a.Id, a._value + b._value, a._maxValue);

        public static StatSlot operator -(StatSlot a, StatSlot b) =>
            new StatSlot(a.Id, a._value - b._value, a._maxValue);

        public override readonly string ToString() => $"{Id}={Value}";
    }
}
