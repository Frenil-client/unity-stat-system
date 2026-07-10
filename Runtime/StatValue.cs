using System;
using System.Globalization;

namespace StatSystem
{
    /// <summary>
    /// 소수 4자리 고정소수점 값 타입.
    /// long raw 값 하나로 표현되는 8바이트 값 타입이라 boxing 없이 연산되고,
    /// 정수 산술만 사용하므로 float처럼 플랫폼/실행마다 결과가 흔들리지 않는다(결정적).
    ///
    /// decimal도 값 타입이라 boxing은 없지만 16바이트 + 소프트웨어 연산이라 핫패스에는 과함.
    /// double은 간단하지만 누적 오차 문제가 그대로 남는다.
    /// 스탯 도메인(기본값 + 가산 + 퍼센트 증감)에는 소수 4자리 정밀도로 충분하다.
    /// </summary>
    public readonly partial struct StatValue : IEquatable<StatValue>, IComparable<StatValue>
    {
        public const int Scale = 10000;

        public static readonly StatValue Zero = default;
        public static readonly StatValue One = FromInt(1);
        public static readonly StatValue MaxValue = FromRaw(long.MaxValue);
        public static readonly StatValue MinValue = FromRaw(long.MinValue);

        private readonly long raw;

        private StatValue(long raw) => this.raw = raw;

        /// <summary>내부 raw 값(1/Scale 단위)을 그대로 사용해 생성한다. 클램프 등 상한값 표현에 사용.</summary>
        public static StatValue FromRaw(long raw) => new StatValue(raw);

        public static StatValue FromInt(long value) => new StatValue(checked(value * Scale));

        /// <summary>
        /// 소수 4자리로 반올림해 저장한다 (0.5는 항상 먼 쪽으로: MidpointRounding.AwayFromZero).
        /// </summary>
        public static StatValue FromFloat(double value) =>
            new StatValue(checked((long)Math.Round(value * Scale, MidpointRounding.AwayFromZero)));

        public static StatValue FromFloat(float value) => FromFloat((double)value);

        /// <summary>1/Scale 단위의 내부 원값. 디버깅/직렬화 용도.</summary>
        public long Raw => raw;

        public float ToFloat() => (float)raw / Scale;

        public double ToDouble() => (double)raw / Scale;

        /// <summary>정수 변환은 버림(내림) 규칙을 따른다. 반올림이 필요하면 Round()를 사용할 것.</summary>
        public long ToInt() => raw / Scale;

        /// <summary>가장 가까운 정수로 반올림(0.5는 먼 쪽으로).</summary>
        public long Round() => (long)Math.Round((double)raw / Scale, MidpointRounding.AwayFromZero);

        public static implicit operator StatValue(long value) => FromInt(value);
        public static implicit operator StatValue(double value) => FromFloat(value);

        public bool Equals(StatValue other) => raw == other.raw;
        public override bool Equals(object obj) => obj is StatValue other && Equals(other);
        public override int GetHashCode() => raw.GetHashCode();
        public int CompareTo(StatValue other) => raw.CompareTo(other.raw);

        public override string ToString() =>
            (raw / (decimal)Scale).ToString("0.####", CultureInfo.InvariantCulture);
    }
}
