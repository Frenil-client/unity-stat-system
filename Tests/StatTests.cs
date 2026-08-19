using System;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace StatSystem.Tests
{
    public class StatTests
    {
        [Test]
        public void SetValue_ThenGetValue_Roundtrips()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 500L);
            Assert.AreEqual((StatValue)500L, stat.GetValue(StatId.AttackPower));
        }

        [Test]
        public void AddValue_Accumulates()
        {
            var stat = new Stat();
            stat.SetValue(StatId.Defense, 100L);
            stat.AddValue(StatId.Defense, 50L);
            Assert.AreEqual((StatValue)150L, stat.GetValue(StatId.Defense));
        }

        [Test]
        public void AddValue_NegativeDelta_Subtracts()
        {
            var stat = new Stat();
            stat.SetValue(StatId.Defense, 100L);
            stat.AddValue(StatId.Defense, -30L);
            Assert.AreEqual((StatValue)70L, stat.GetValue(StatId.Defense));
        }

        // 명명 접근자와 StatId 접근이 같은 슬롯을 가리키는지 검증.
        [Test]
        public void NamedAccessor_ReflectsSetValue()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 777L);
            Assert.AreEqual((StatValue)777L, stat.AttackPower.Value);
            Assert.AreEqual(StatId.AttackPower, stat.AttackPower.Id);
        }

        [Test]
        public void CopyConstructor_ProducesEqualStat()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 300L);
            stat.SetValue(StatId.CriticalRate, 25.5);

            var copy = new Stat(stat);

            Assert.IsTrue(stat.IsEqual(copy));
        }

        [Test]
        public void CopyConstructor_ProducesIndependentInstance()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 300L);

            var copy = new Stat(stat);
            copy.SetValue(StatId.AttackPower, 1L);

            Assert.AreEqual((StatValue)300L, stat.GetValue(StatId.AttackPower));
            Assert.IsFalse(stat.IsEqual(copy));
        }

        [Test]
        public void CopyConstructor_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Stat(null));
        }

        [Test]
        public void GetValue_UnregisteredId_ReturnsZero()
        {
            var stat = new Stat();
            Assert.AreEqual(StatValue.Zero, stat.GetValue((StatId)99999));
        }

        [Test]
        public void SetValue_UnregisteredId_ReturnsFalse()
        {
            var stat = new Stat();
            Assert.IsFalse(stat.SetValue((StatId)99999, 10L));
            Assert.IsTrue(stat.SetValue(StatId.AttackPower, 10L));
        }

        [Test]
        public void Slots_CoverEveryStatId()
        {
            var stat = new Stat();
            Assert.AreEqual(StatRegistry.AllIds.Count, stat.Slots.Count);

            foreach (var id in StatRegistry.AllIds)
                Assert.AreEqual(id, stat.Slots[IndexOfForTest(id)].Id);
        }

        [Test]
        public void SetMaxValue_ThenExceedingSet_Clamps()
        {
            var stat = new Stat();
            stat.SetMaxValue(StatId.Defense, 200L);
            stat.SetValue(StatId.Defense, 999L);
            Assert.AreEqual((StatValue)200L, stat.GetValue(StatId.Defense));
        }

        //Changed 통지

        [Test]
        public void Changed_FiresWithStoredValue()
        {
            var stat = new Stat();
            StatId firedId = default;
            StatValue firedValue = default;
            int fireCount = 0;

            stat.Changed += (id, value) => { firedId = id; firedValue = value; fireCount++; };
            stat.SetValue(StatId.AttackPower, 500L);

            Assert.AreEqual(1, fireCount);
            Assert.AreEqual(StatId.AttackPower, firedId);
            Assert.AreEqual((StatValue)500L, firedValue);
        }

        // 클램프가 걸리면 요청값이 아니라 실제로 저장된 값이 통지되어야 한다.
        // 요청값을 그대로 흘리면 UI가 실제 스탯과 다른 숫자를 표시하게 된다.
        [Test]
        public void Changed_ReportsClampedValue_NotRequestedValue()
        {
            var stat = new Stat();
            stat.SetMaxValue(StatId.Defense, 200L);

            StatValue firedValue = default;
            stat.Changed += (_, value) => firedValue = value;
            stat.SetValue(StatId.Defense, 999L);

            Assert.AreEqual((StatValue)200L, firedValue);
        }

        [Test]
        public void Changed_NotFired_WhenValueUnchanged()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 500L);

            int fireCount = 0;
            stat.Changed += (_, __) => fireCount++;

            stat.SetValue(StatId.AttackPower, 500L);
            stat.AddValue(StatId.AttackPower, 0L);

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void Changed_NotCopied_ByCopyConstructor()
        {
            var stat = new Stat();
            int fireCount = 0;
            stat.Changed += (_, __) => fireCount++;

            var copy = new Stat(stat);
            copy.SetValue(StatId.AttackPower, 123L);

            Assert.AreEqual(0, fireCount);
        }

        //할당 검증 - 이 두 테스트가 Reflection(FieldInfo) 기반 구현에서는 실패했다.
        //FieldInfo.GetValue/SetValue가 struct인 StatSlot을 object로 boxing하기 때문이다.

        [Test]
        public void BulkAccess_DoesNotAllocate()
        {
            var stat = new Stat();
            StatValue sink = default;

            Assert.That(() =>
            {
                stat.SetValue(StatId.AttackPower, 500L);
                stat.AddValue(StatId.AttackPower, 100L);
                sink = stat.GetValue(StatId.AttackPower);
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual((StatValue)600L, sink);
        }

        [Test]
        public void NamedAccessorRead_DoesNotAllocate()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 500L);
            StatValue sink = default;

            Assert.That(() =>
            {
                sink = stat.AttackPower.Value;
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
                stat.SetValue(StatId.AttackPower, next++);
            }, Is.Not.AllocatingGCMemory());

            Assert.AreNotEqual(StatValue.Zero, observed);
        }

        //StatRegistry.AllIds 순서가 곧 Slots 인덱스라는 전제를 테스트에서 재현한다.
        private static int IndexOfForTest(StatId id)
        {
            var ids = StatRegistry.AllIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return i;
            }
            return -1;
        }
    }
}
