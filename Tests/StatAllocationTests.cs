using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace StatSystem.Tests
{
    /// <summary>
    /// 힙 할당 회귀를 막는 테스트. 이 파일만 Unity에 의존한다
    /// (`Is.Not.AllocatingGCMemory()`가 UnityEngine.TestTools의 제약이라서).
    ///
    /// 나머지 테스트는 순수 C#이라 `Tests~/`의 헤드리스 프로젝트에서 Unity 없이 돌고,
    /// CI가 매 푸시마다 실행한다. 같은 성질을 라이선스 없이 검증하는 쪽은
    /// `Benchmarks~/`가 맡는다 — 그쪽은 수치까지 비교하고 CI에서 0바이트를 강제한다.
    /// </summary>
    public class StatAllocationTests
    {
        // 이 두 테스트는 이전 Reflection(FieldInfo) 구현에서 실패했다.
        // FieldInfo.GetValue/SetValue가 struct인 StatSlot을 object로 boxing하기 때문이다.

        [Test]
        public void BulkAccess_DoesNotAllocate()
        {
            var stat = new Stat();
            StatValue sink = default;

            Assert.That(() =>
            {
                stat.SetBaseValue(StatId.AttackPower, 500L);
                stat.AddBaseValue(StatId.AttackPower, 100L);
                sink = stat.GetValue(StatId.AttackPower);
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual((StatValue)600L, sink);
        }

        [Test]
        public void NamedAccessorRead_DoesNotAllocate()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 500L);
            StatValue sink = default;

            Assert.That(() =>
            {
                sink = stat.AttackPower;
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual((StatValue)500L, sink);
        }

        [Test]
        public void ChangedNotification_DoesNotAllocate()
        {
            var stat = new Stat();
            StatValue observed = default;
            stat.Changed += (_, value) => observed = value;

            long next = 1;
            Assert.That(() =>
            {
                stat.SetBaseValue(StatId.AttackPower, next++);
            }, Is.Not.AllocatingGCMemory());

            Assert.AreNotEqual(StatValue.Zero, observed);
        }

        [Test]
        public void Arithmetic_ChainedOperators_DoNotAllocate()
        {
            StatValue a = 100L;
            StatValue b = 200L;
            StatValue c = 3L;

            Assert.That(() =>
            {
                var r = a + b * c;
            }, Is.Not.AllocatingGCMemory());
        }

        // 최종값은 쓰기 시점에 계산해 캐시에 넣으므로, 읽기는 배열 인덱싱 한 번이다.
        [Test]
        public void ReadingFinalValue_DoesNotAllocate()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.30);
            StatValue sink = default;

            Assert.That(() =>
            {
                sink = stat.GetValue(StatId.AttackPower);
                sink = stat.AttackPower;
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual((StatValue)195L, sink);
        }

        // 재계산은 모디파이어 목록을 훑지만 struct 열거자를 쓰므로 할당이 없다.
        [Test]
        public void RecomputingWithModifiers_DoesNotAllocate()
        {
            var stat = new Stat();
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.30);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentMultiply, 0.10);

            long next = 1;
            Assert.That(() =>
            {
                stat.SetBaseValue(StatId.AttackPower, next++);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
