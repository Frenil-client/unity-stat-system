using NUnit.Framework;

namespace StatSystem.Tests
{
    public class StatTests
    {
        [Test]
        public void SetValue_ThenGetValue_Roundtrips()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 500);
            Assert.AreEqual(500, stat.GetValue(StatId.AttackPower));
        }

        [Test]
        public void AddValue_Accumulates()
        {
            var stat = new Stat();
            stat.SetValue(StatId.Defense, 100);
            stat.AddValue(StatId.Defense, 50);
            Assert.AreEqual(150, stat.GetValue(StatId.Defense));
        }

        // 필드 직접 접근과 StatId Dictionary 접근이 같은 인스턴스를 가리키는지 검증.
        [Test]
        public void DirectFieldAccess_SharesInstanceWithDictionary()
        {
            var stat = new Stat();
            stat.AttackPower.Value = 777;
            Assert.AreEqual(777, stat.GetValue(StatId.AttackPower));
        }

        [Test]
        public void CopyConstructor_ProducesEqualStat()
        {
            var stat = new Stat();
            stat.SetValue(StatId.AttackPower, 300);
            stat.SetValue(StatId.CriticalRate, 25.5);

            var copy = new Stat(stat);

            Assert.IsTrue(stat.IsEqual(copy));
        }

        [Test]
        public void GetValue_UnregisteredId_ReturnsZero()
        {
            var stat = new Stat();
            Assert.AreEqual(0, stat.GetValue((StatId)99999));
        }
    }
}
