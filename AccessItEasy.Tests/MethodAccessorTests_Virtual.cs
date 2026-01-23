using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
public partial class MethodAccessorTests
{
    const int BaseValue = 42;
    const int DerivedValue = 84;

    class BaseTarget
    {
        public virtual int Value => BaseValue;
    }

    class DerivedTarget : BaseTarget
    {
        public override int Value => DerivedValue;
    }

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_Value")]
    private static extern int CallGetValueBase(BaseTarget target);

    // Unlike UnsafeAccessor, you don't have to define with the base type to access the virtual method
    // However, this restrict you call method only on the derived type
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_Value")]
    private static extern int CallGetValueDerived(DerivedTarget target);

    [Test]
    public void CallGetValueBase_OnBaseTarget_ReturnsBaseValue()
    {
        var target = new BaseTarget();
        var result = CallGetValueBase(target);
        Assert.That(result, Is.EqualTo(BaseValue));
    }

    [Test]
    public void CallGetValueBase_OnDerivedTarget_ReturnsDerivedValue()
    {
        var target = new DerivedTarget();
        var result = CallGetValueBase(target);
        Assert.That(result, Is.EqualTo(DerivedValue));
    }

    [Test]
    public void CallGetValueDerived_OnDerivedTarget_ReturnsDerivedValue()
    {
        var target = new DerivedTarget();
        var result = CallGetValueDerived(target);
        Assert.That(result, Is.EqualTo(DerivedValue));
    }
}
