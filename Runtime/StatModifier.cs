using System;

namespace StatSystem
{
    /// <summary>
    /// 모디파이어가 적용되는 방식. 숫자는 적용 순서를 뜻하며, 작은 쪽이 먼저 적용된다.
    /// </summary>
    public enum StatModifierType
    {
        /// <summary>기본값에 그대로 더한다. 장비의 "공격력 +50" 같은 것.</summary>
        Flat = 100,

        /// <summary>서로 합산한 뒤 한 번에 곱한다. +30%와 +20%가 있으면 ×1.5가 된다.</summary>
        PercentAdd = 200,

        /// <summary>각각 순차로 곱한다. +30%와 +20%가 있으면 ×1.3×1.2 = ×1.56이 된다.</summary>
        PercentMultiply = 300,
    }

    /// <summary>
    /// 모디파이어 하나를 가리키는 식별자. <see cref="Stat.AddModifier"/>가 돌려주며
    /// 나중에 정확히 그 하나만 제거할 때 쓴다.
    /// </summary>
    public readonly struct ModifierHandle : IEquatable<ModifierHandle>
    {
        internal int Id { get; }

        internal ModifierHandle(int id) => Id = id;

        /// <summary>유효한 핸들인지. default(ModifierHandle)은 무효다.</summary>
        public bool IsValid => Id != 0;

        public bool Equals(ModifierHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ModifierHandle other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => IsValid ? $"Modifier#{Id}" : "Modifier(invalid)";

        public static bool operator ==(ModifierHandle a, ModifierHandle b) => a.Id == b.Id;
        public static bool operator !=(ModifierHandle a, ModifierHandle b) => a.Id != b.Id;
    }

    /// <summary>
    /// 기본값 위에 얹히는 보정 하나. 장비, 버프, 세트 효과처럼 **붙였다 뗄 수 있는** 것들을 표현한다.
    ///
    /// 뗄 수 있다는 점이 핵심이다. 기본값에 직접 더하고 나중에 빼는 방식은
    /// 상한 클램프와 만나면 값이 어긋난다 — 더할 때 상한에 걸려 일부만 반영됐는데
    /// 뺄 때는 전액을 빼기 때문이다. 모디파이어는 목록에서 제거한 뒤 기본값부터 다시
    /// 계산하므로 그런 어긋남이 원리적으로 생기지 않는다.
    /// </summary>
    public readonly struct StatModifier
    {
        public ModifierHandle Handle { get; }
        public StatId Id { get; }
        public StatModifierType Type { get; }
        public StatValue Value { get; }

        /// <summary>
        /// 이 모디파이어를 붙인 주체(장비 인스턴스, 버프 인스턴스 등).
        /// <see cref="Stat.RemoveModifiersFrom"/>가 참조 동일성으로 찾아 제거한다. null일 수 있다.
        /// </summary>
        public object Source { get; }

        internal StatModifier(ModifierHandle handle, StatId id, StatModifierType type, StatValue value, object source)
        {
            Handle = handle;
            Id = id;
            Type = type;
            Value = value;
            Source = source;
        }

        public override string ToString() => $"{Id} {Type} {Value}";
    }
}
