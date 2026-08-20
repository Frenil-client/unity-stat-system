using System;
using NUnit.Framework;

namespace StatSystem.Tests
{
    public class StatValueTests
    {
        [Test]
        public void Precision_ZeroPointOnePlusZeroPointTwo_EqualsZeroPointThree()
        {
            StatValue a = 0.1;
            StatValue b = 0.2;
            StatValue expected = 0.3;
            Assert.AreEqual(expected, a + b);
        }

        [Test]
        public void FromInt_ToFloat_Roundtrips()
        {
            StatValue v = StatValue.FromInt(1234);
            Assert.AreEqual(1234f, v.ToFloat());
        }

        [Test]
        public void FromFloat_ToDouble_Roundtrips()
        {
            StatValue v = StatValue.FromFloat(12.3456);
            Assert.AreEqual(12.3456, v.ToDouble(), 0.00001);
        }

        [Test]
        public void ToInt_Truncates()
        {
            StatValue v = StatValue.FromFloat(4.9999);
            Assert.AreEqual(4L, v.ToInt());
        }

        [Test]
        public void Round_RoundsToNearest()
        {
            StatValue v = StatValue.FromFloat(4.9999);
            Assert.AreEqual(5L, v.Round());
        }

        [Test]
        public void ApplyPercent_AddsPercentageOfBase()
        {
            StatValue baseValue = 1000L;
            StatValue percent = 0.30; // 30%
            StatValue expected = 1300L;
            Assert.AreEqual(expected, StatValue.ApplyPercent(baseValue, percent));
        }

        [Test]
        public void ApplyPercent_RoundTripAccuracy()
        {
            StatValue baseValue = 12345L;
            StatValue percent = 0.1; // 10%
            StatValue expected = 13579.5; // 12345 * 1.1
            Assert.AreEqual(expected, StatValue.ApplyPercent(baseValue, percent));
        }

        [Test]
        public void Comparison_Operators_OrderCorrectly()
        {
            StatValue a = 1.5;
            StatValue b = 2.5;
            Assert.IsTrue(a < b);
            Assert.IsTrue(b > a);
            Assert.IsTrue(a <= a);
            Assert.IsTrue(a >= a);
            Assert.IsTrue(a != b);
        }

        [Test]
        public void UnaryMinus_NegatesValue()
        {
            StatValue a = 5L;
            Assert.AreEqual((StatValue)(-5L), -a);
        }

        [Test]
        public void Divide_ByZero_Throws()
        {
            StatValue a = 10L;
            StatValue zero = 0L;
            Assert.Throws<DivideByZeroException>(() => { var _ = a / zero; });
        }

        [Test]
        public void Multiply_Overflow_Throws()
        {
            StatValue huge = StatValue.FromRaw(long.MaxValue / 2);
            Assert.Throws<OverflowException>(() => { var _ = huge * huge; });
        }

        [Test]
        public void Add_Overflow_Throws()
        {
            StatValue huge = StatValue.FromRaw(long.MaxValue);
            StatValue one = StatValue.FromRaw(1);
            Assert.Throws<OverflowException>(() => { var _ = huge + one; });
        }
    }
}
