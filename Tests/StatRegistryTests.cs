using System.Collections.Generic;
using NUnit.Framework;

namespace StatSystem.Tests
{
    public class StatRegistryTests
    {
        [Test]
        public void GetName_ById_ReturnsEnumName()
        {
            Assert.AreEqual("AttackPower", StatRegistry.GetName(StatId.AttackPower));
        }

        [Test]
        public void GetUid_ById_ReturnsEnumValue()
        {
            Assert.AreEqual(100u, StatRegistry.GetUid(StatId.AttackPower));
        }

        [Test]
        public void GetId_ByUid_RoundtripsToEnum()
        {
            Assert.AreEqual(StatId.AttackPower, StatRegistry.GetId(100u));
        }

        [Test]
        public void GetId_ByName_RoundtripsToEnum()
        {
            Assert.AreEqual(StatId.AttackPower, StatRegistry.GetId("AttackPower"));
        }

        [Test]
        public void GetName_UnknownUid_ReturnsUnknownLabel()
        {
            Assert.AreEqual("Unknown(99999)", StatRegistry.GetName(99999u));
        }

        [Test]
        public void GetUid_UnknownName_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => StatRegistry.GetUid("DoesNotExist"));
        }

        [Test]
        public void GetId_UnknownUid_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => StatRegistry.GetId(99999u));
        }
    }
}
