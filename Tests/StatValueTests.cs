using System;
using NUnit.Framework;

namespace StatSystem.Tests
{
    public class StatValueTests
    {
        [Test]
        public void Value_ExceedingMax_ClampsToMax()
        {
            var def = new StatValue<long>(StatId.Defense, 100, 200L);
            def.Value = 999;
            Assert.AreEqual(200L, def.Value);
        }

        [Test]
        public void MaxValue_BelowCurrentValue_Throws()
        {
            var def = new StatValue<long>(StatId.Defense, 100, 200L);
            Assert.Throws<ArgumentException>(() => def.MaxValue = 50);
        }

        [Test]
        public void Constructor_MaxBelowValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StatValue<long>(StatId.Defense, 100, 50L));
        }

        [Test]
        public void ValueDecimal_TracksValue()
        {
            var ap = new StatValue<long>(StatId.AttackPower, 1234);
            Assert.AreEqual(1234m, ap.ValueDecimal);
        }

        [Test]
        public void StatUid_MatchesEnumValue()
        {
            var ap = new StatValue<long>(StatId.AttackPower, 0);
            Assert.AreEqual((uint)StatId.AttackPower, ap.StatUid);
        }

        [Test]
        public void Operator_Add_SumsValues_AndPreservesId()
        {
            var a = new StatValue<long>(StatId.AttackPower, 100, 1000L);
            var b = new StatValue<long>(StatId.AttackPower, 250, 9999L);
            var sum = a + b;
            Assert.AreEqual(350L, sum.Value);
            Assert.AreEqual(StatId.AttackPower, sum.Id);
        }

        [Test]
        public void Operator_Subtract_SubtractsValues()
        {
            var a = new StatValue<long>(StatId.AttackPower, 500, 1000L);
            var b = new StatValue<long>(StatId.AttackPower, 200, 1000L);
            var diff = a - b;
            Assert.AreEqual(300L, diff.Value);
        }

        // 합산 결과가 좌변의 MaxValue 를 넘으면, 결과를 담을 StatValue 생성자가
        // MaxValue < Value 무결성 검사에 걸려 예외를 던진다 (설계상 의도된 동작).
        [Test]
        public void Operator_Add_SumExceedingLeftMax_Throws()
        {
            var a = new StatValue<long>(StatId.AttackPower, 800, 1000L);
            var b = new StatValue<long>(StatId.AttackPower, 500, 1000L);
            Assert.Throws<ArgumentException>(() => { var _ = a + b; });
        }
    }
}
