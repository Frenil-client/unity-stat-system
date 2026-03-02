using System;

namespace StatSystem
{
    /// <summary>
    /// 제네릭 스탯 값 컨테이너.
    /// int / long / float / double 등 다양한 수치 타입을 단일 클래스로 처리합니다.
    ///
    /// 핵심 설계:
    /// - MaxValue 무결성 검사: Value가 MaxValue를 초과하면 자동 클램프
    /// - decimal 정밀도 유지: 부동소수점 오차 방지를 위해 ValueDecimal 병행 보관
    /// </summary>
    public partial class StatValue<T> where T : struct, IComparable<T>
    {
        /// <summary>스탯 고유 ID (enum)</summary>
        public StatId Id { get; private set; }

        /// <summary>스탯 UID (uint). StatRegistry를 통해 이름으로 조회 가능</summary>
        public uint StatUid => (uint)Id;

        private T _value;
        private T _maxValue;

        /// <summary>부동소수점 오차 방지용 decimal 보관값</summary>
        public decimal ValueDecimal { get; private set; }

        /// <summary>
        /// 스탯 값. MaxValue를 초과하면 자동으로 MaxValue로 클램프됩니다.
        /// </summary>
        public T Value
        {
            get => _value;
            set
            {
                _value = _maxValue.CompareTo(value) < 0 ? _maxValue : value;
                ValueDecimal = Convert.ToDecimal(_value);
            }
        }

        /// <summary>
        /// 스탯 최대값. 현재 Value보다 낮게 설정하면 예외를 던집니다.
        /// </summary>
        public T MaxValue
        {
            get => _maxValue;
            set
            {
                if (value.CompareTo(_value) < 0)
                    throw new ArgumentException($"MaxValue({value})는 현재 Value({_value})보다 작을 수 없습니다.");
                _maxValue = value;
            }
        }

        /// <summary>StatId와 초기값으로 생성. 최대값은 타입 기본 최대값으로 설정됩니다.</summary>
        public StatValue(StatId id, T value)
        {
            Id        = id;
            _maxValue = GetTypeMaxValue();
            Value     = value;
        }

        /// <summary>StatId, 초기값, 최대값으로 생성합니다.</summary>
        public StatValue(StatId id, T value, T maxValue)
        {
            if (maxValue.CompareTo(value) < 0)
                throw new ArgumentException($"MaxValue({maxValue})는 Value({value})보다 작을 수 없습니다.");

            Id        = id;
            _maxValue = maxValue;
            Value     = value;
        }

        private T GetTypeMaxValue()
        {
            var type = typeof(T);
            if (type == typeof(int))    return (T)(object)int.MaxValue;
            if (type == typeof(long))   return (T)(object)long.MaxValue;
            if (type == typeof(uint))   return (T)(object)uint.MaxValue;
            if (type == typeof(ulong))  return (T)(object)ulong.MaxValue;
            if (type == typeof(float))  return (T)(object)float.MaxValue;
            if (type == typeof(double)) return (T)(object)double.MaxValue;
            throw new ArgumentException($"지원하지 않는 타입: {type}");
        }
    }
}
