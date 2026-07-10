using System;
using NUnit.Framework;

namespace StatSystem.Tests
{
    public class StatSlotTests
    {
        [Test]
        public void Value_ExceedingMax_ClampsToMax()
        {
            var def = new StatSlot(StatId.Defense, 100L, 200L);
            def.Value = 999L;
            Assert.AreEqual((StatValue)200L, def.Value);
        }

        [Test]
        public void MaxValue_BelowCurrentValue_Throws()
        {
            var def = new StatSlot(StatId.Defense, 100L, 200L);
            Assert.Throws<ArgumentException>(() => def.MaxValue = 50L);
        }

        [Test]
        public void Constructor_MaxBelowValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StatSlot(StatId.Defense, 100L, 50L));
        }

        [Test]
        public void StatUid_MatchesEnumValue()
        {
            var ap = new StatSlot(StatId.AttackPower, 0L);
            Assert.AreEqual((uint)StatId.AttackPower, ap.StatUid);
        }

        [Test]
        public void Operator_Add_SumsValues_AndPreservesId()
        {
            var a = new StatSlot(StatId.AttackPower, 100L, 1000L);
            var b = new StatSlot(StatId.AttackPower, 250L, 9999L);
            var sum = a + b;
            Assert.AreEqual((StatValue)350L, sum.Value);
            Assert.AreEqual(StatId.AttackPower, sum.Id);
        }

        [Test]
        public void Operator_Subtract_SubtractsValues()
        {
            var a = new StatSlot(StatId.AttackPower, 500L, 1000L);
            var b = new StatSlot(StatId.AttackPower, 200L, 1000L);
            var diff = a - b;
            Assert.AreEqual((StatValue)300L, diff.Value);
        }

        // 합산 결과가 좌변의 MaxValue 를 넘으면, 결과를 담을 StatSlot 생성자가
        // MaxValue < Value 무결성 검사에 걸려 예외를 던진다 (설계상 의도된 동작).
        [Test]
        public void Operator_Add_SumExceedingLeftMax_Throws()
        {
            var a = new StatSlot(StatId.AttackPower, 800L, 1000L);
            var b = new StatSlot(StatId.AttackPower, 500L, 1000L);
            Assert.Throws<ArgumentException>(() => { var _ = a + b; });
        }
    }
}
