using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace StatSystem.Tests
{
    public class StatModifierTests
    {
        // 이 테스트가 이 기능의 존재 이유다.
        // 기본값에 직접 더하고 빼는 방식이었을 때: 180에서 +50을 하면 상한 200에 걸려 20만
        // 반영되는데, 해제할 때는 50을 빼서 150이 됐다. 버프를 받았다 풀었을 뿐인데
        // 스탯이 줄어드는 버그다.
        [Test]
        public void RemovingModifier_RestoresOriginal_EvenWhenItWasClamped()
        {
            var stat = new Stat();
            stat.SetMaxValue(StatId.Defense, 200L);
            stat.SetBaseValue(StatId.Defense, 180L);

            var buff = new object();
            stat.AddModifier(StatId.Defense, StatModifierType.Flat, 50L, buff);
            Assert.AreEqual((StatValue)200L, stat.GetValue(StatId.Defense), "상한으로 잘려야 한다");

            stat.RemoveModifiersFrom(buff);

            Assert.AreEqual((StatValue)180L, stat.GetValue(StatId.Defense), "원래 값으로 돌아와야 한다");
            Assert.AreEqual((StatValue)180L, stat.GetBaseValue(StatId.Defense), "기본값은 처음부터 끝까지 그대로다");
        }

        [Test]
        public void Flat_AddsToBase()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);

            Assert.AreEqual((StatValue)150L, stat.AttackPower);
        }

        [Test]
        public void PercentAdd_SumsThenMultipliesOnce()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.30);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.20);

            // 1.3 × 1.2 = 1.56 이 아니라 1 + 0.5 = 1.5
            Assert.AreEqual((StatValue)150L, stat.AttackPower);
        }

        [Test]
        public void PercentMultiply_AppliesSuccessively()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentMultiply, 0.30);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentMultiply, 0.20);

            Assert.AreEqual((StatValue)156L, stat.AttackPower);
        }

        [Test]
        public void ApplicationOrder_IsFlatThenPercentAddThenPercentMultiply()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.30);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.20);
            stat.AddModifier(StatId.AttackPower, StatModifierType.PercentMultiply, 0.10);

            // (100 + 50) × 1.5 × 1.1 = 247.5
            Assert.AreEqual((StatValue)247.5, stat.AttackPower);
        }

        // 추가한 순서가 결과를 바꾸면 장비 착용 순서에 따라 스탯이 달라진다.
        [Test]
        public void Result_IsIndependentOfInsertionOrder()
        {
            var forward = new Stat();
            forward.SetBaseValue(StatId.AttackPower, 100L);
            forward.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);
            forward.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.30);
            forward.AddModifier(StatId.AttackPower, StatModifierType.PercentMultiply, 0.10);

            var reversed = new Stat();
            reversed.SetBaseValue(StatId.AttackPower, 100L);
            reversed.AddModifier(StatId.AttackPower, StatModifierType.PercentMultiply, 0.10);
            reversed.AddModifier(StatId.AttackPower, StatModifierType.PercentAdd, 0.30);
            reversed.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);

            Assert.AreEqual(forward.AttackPower, reversed.AttackPower);
        }

        [Test]
        public void RemoveModifier_ByHandle_RemovesExactlyOne()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            var first = stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 30L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 20L);
            Assert.AreEqual((StatValue)150L, stat.AttackPower);

            Assert.IsTrue(stat.RemoveModifier(first));

            Assert.AreEqual((StatValue)120L, stat.AttackPower);
            Assert.AreEqual(1, stat.Modifiers.Count);
        }

        [Test]
        public void RemoveModifier_SameHandleTwice_ReturnsFalse()
        {
            var stat = new Stat();
            var handle = stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 10L);

            Assert.IsTrue(stat.RemoveModifier(handle));
            Assert.IsFalse(stat.RemoveModifier(handle));
            Assert.IsFalse(stat.RemoveModifier(default));
        }

        // 장비 하나가 여러 스탯을 올리는 흔한 경우.
        [Test]
        public void RemoveModifiersFrom_RemovesAcrossStats_AndLeavesOtherSources()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            stat.SetBaseValue(StatId.Defense, 100L);

            var armor = new object();
            var potion = new object();
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 40L, armor);
            stat.AddModifier(StatId.Defense, StatModifierType.Flat, 60L, armor);
            stat.AddModifier(StatId.Defense, StatModifierType.Flat, 10L, potion);

            Assert.AreEqual(2, stat.RemoveModifiersFrom(armor));

            Assert.AreEqual((StatValue)100L, stat.AttackPower);
            Assert.AreEqual((StatValue)110L, stat.Defense, "다른 소스의 보정은 남아야 한다");
        }

        [Test]
        public void RemoveModifiersFrom_Null_RemovesNothing()
        {
            var stat = new Stat();
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 10L);

            Assert.AreEqual(0, stat.RemoveModifiersFrom(null));
            Assert.AreEqual(1, stat.Modifiers.Count);
        }

        [Test]
        public void ClearModifiers_LeavesBaseValuesIntact()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);

            stat.ClearModifiers();

            Assert.AreEqual((StatValue)100L, stat.AttackPower);
            Assert.AreEqual(0, stat.Modifiers.Count);
        }

        [Test]
        public void AddModifier_UnregisteredId_ReturnsInvalidHandleAndAddsNothing()
        {
            var stat = new Stat();

            var handle = stat.AddModifier((StatId)99999, StatModifierType.Flat, 10L);

            Assert.IsFalse(handle.IsValid);
            Assert.AreEqual(0, stat.Modifiers.Count);
        }

        //통지

        [Test]
        public void AddModifier_FiresChangedWithFinalValue()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);

            StatValue fired = default;
            int count = 0;
            stat.Changed += (_, value) => { fired = value; count++; };

            stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);

            Assert.AreEqual(1, count);
            Assert.AreEqual((StatValue)150L, fired);
        }

        // 이미 상한에 닿아 있으면 모디파이어를 더 얹어도 최종값이 그대로다.
        // 결과가 같으면 통지하지 않는다는 정책이 여기서도 지켜져야 한다.
        [Test]
        public void AddModifier_WhenFinalValueUnchanged_StaysSilent()
        {
            var stat = new Stat();
            stat.SetMaxValue(StatId.Defense, 200L);
            stat.SetBaseValue(StatId.Defense, 180L);
            stat.AddModifier(StatId.Defense, StatModifierType.Flat, 50L);

            int count = 0;
            stat.Changed += (_, __) => count++;

            stat.AddModifier(StatId.Defense, StatModifierType.Flat, 10L);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void RemoveModifier_FiresChanged()
        {
            var stat = new Stat();
            stat.SetBaseValue(StatId.AttackPower, 100L);
            var handle = stat.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);

            StatValue fired = default;
            stat.Changed += (_, value) => fired = value;

            stat.RemoveModifier(handle);

            Assert.AreEqual((StatValue)100L, fired);
        }

        //복사

        [Test]
        public void CopyConstructor_CopiesModifiers_AndStaysIndependent()
        {
            var source = new Stat();
            source.SetBaseValue(StatId.AttackPower, 100L);
            source.AddModifier(StatId.AttackPower, StatModifierType.Flat, 50L);

            var copy = new Stat(source);
            Assert.AreEqual((StatValue)150L, copy.AttackPower);
            Assert.AreEqual(1, copy.Modifiers.Count);

            copy.ClearModifiers();

            Assert.AreEqual((StatValue)150L, source.AttackPower, "원본은 영향받지 않아야 한다");
        }

        //할당

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
