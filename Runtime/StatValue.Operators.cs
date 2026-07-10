using System;

namespace StatSystem
{
    /// <summary>
    /// StatValue 연산자 오버로딩. 전부 long 산술이며 object 경유가 없다.
    /// </summary>
    public readonly partial struct StatValue
    {
        public static StatValue operator +(StatValue a, StatValue b) => new StatValue(checked(a.raw + b.raw));

        public static StatValue operator -(StatValue a, StatValue b) => new StatValue(checked(a.raw - b.raw));

        public static StatValue operator -(StatValue a) => new StatValue(checked(-a.raw));

        /// <summary>
        /// raw * raw는 long 범위를 넘을 수 있어 decimal(28~29자리)을 중간값으로 사용한다.
        /// Unity(netstandard 2.1)엔 Int128이 없고, 부호 분리 후 ulong 상하위 분할 곱은
        /// 코드 복잡도 대비 이득이 낮아 decimal 중간값 방식을 선택했다.
        /// 실제 게임 스탯 범위(raw가 10^10~10^12 수준)에서 곱은 decimal 상한(약 7.9x10^28)에
        /// 여유 있게 들어온다. 범위를 벗어나면 조용히 자르는 대신 OverflowException을 던진다 -
        /// 값이 잘리면 전투 수치 버그가 은폐될 위험이 크기 때문.
        /// </summary>
        public static StatValue operator *(StatValue a, StatValue b)
        {
            decimal result = (decimal)a.raw * b.raw / Scale;
            return new StatValue(checked((long)result));
        }

        public static StatValue operator /(StatValue a, StatValue b)
        {
            if (b.raw == 0)
                throw new DivideByZeroException("StatValue division by zero.");

            decimal result = (decimal)a.raw * Scale / b.raw;
            return new StatValue(checked((long)Math.Round(result, MidpointRounding.AwayFromZero)));
        }

        public static bool operator ==(StatValue a, StatValue b) => a.raw == b.raw;
        public static bool operator !=(StatValue a, StatValue b) => a.raw != b.raw;
        public static bool operator <(StatValue a, StatValue b) => a.raw < b.raw;
        public static bool operator <=(StatValue a, StatValue b) => a.raw <= b.raw;
        public static bool operator >(StatValue a, StatValue b) => a.raw > b.raw;
        public static bool operator >=(StatValue a, StatValue b) => a.raw >= b.raw;

        /// <summary>value * (1 + percent). percent는 0.30 = 30%처럼 비율로 전달한다.</summary>
        public static StatValue ApplyPercent(StatValue value, StatValue percent) => value + value * percent;
    }
}
