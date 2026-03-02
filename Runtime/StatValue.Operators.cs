using System;

namespace StatSystem
{
    /// <summary>
    /// StatValue&lt;T&gt; 연산자 오버로딩.
    /// Stat 레이어는 합산(+/-)만 담당합니다.
    /// 배율 스탯의 곱연산은 버프/데미지 계산 레이어에서 처리합니다.
    ///
  /// 사용 예:
    /// var total = baseStat + equipStat + buffStat;
    /// var diff  = currentStat - removedBuff;
    /// </summary>
    public partial class StatValue<T> where T : struct, IComparable<T>
    {
        public static StatValue<T> operator +(StatValue<T> a, StatValue<T> b) =>
            new StatValue<T>(a.Id, Add(a._value, b._value), a._maxValue);

        public static StatValue<T> operator -(StatValue<T> a, StatValue<T> b) =>
            new StatValue<T>(a.Id, Subtract(a._value, b._value), a._maxValue);

        private static T Add(T a, T b)
        {
            if (typeof(T) == typeof(int))    return (T)(object)((int)(object)a    + (int)(object)b);
            if (typeof(T) == typeof(long))   return (T)(object)((long)(object)a   + (long)(object)b);
            if (typeof(T) == typeof(uint))   return (T)(object)((uint)(object)a   + (uint)(object)b);
            if (typeof(T) == typeof(ulong))  return (T)(object)((ulong)(object)a  + (ulong)(object)b);
            if (typeof(T) == typeof(float))  return (T)(object)((float)(object)a  + (float)(object)b);
            if (typeof(T) == typeof(double)) return (T)(object)((double)(object)a + (double)(object)b);
            throw new InvalidOperationException($"지원하지 않는 타입: {typeof(T)}");
        }

        private static T Subtract(T a, T b)
        {
            if (typeof(T) == typeof(int))    return (T)(object)((int)(object)a    - (int)(object)b);
            if (typeof(T) == typeof(long))   return (T)(object)((long)(object)a   - (long)(object)b);
            if (typeof(T) == typeof(uint))   return (T)(object)((uint)(object)a   - (uint)(object)b);
            if (typeof(T) == typeof(ulong))  return (T)(object)((ulong)(object)a  - (ulong)(object)b);
            if (typeof(T) == typeof(float))  return (T)(object)((float)(object)a  - (float)(object)b);
            if (typeof(T) == typeof(double)) return (T)(object)((double)(object)a - (double)(object)b);
            throw new InvalidOperationException($"지원하지 않는 타입: {typeof(T)}");
        }
    }
}
