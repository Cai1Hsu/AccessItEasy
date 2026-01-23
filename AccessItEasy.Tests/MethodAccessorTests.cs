using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
public partial class MethodAccessorTests
{
    #region Accessors

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "IncrementState")]
    private static extern void CallIncrementState(MethodTarget target);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
    private static extern int CallAdd(MethodTarget target, int a, int b);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "Concat")]
    private static extern string CallConcat(MethodTarget target, string a, string b, string c);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "SetState")]
    private static extern void CallSetState(MethodTarget target, int value);

    #endregion

    [Test]
    public void CallIncrementState_IncrementsState()
    {
        var target = new MethodTarget();
        Assert.That(target.GetState(), Is.EqualTo(0));

        CallIncrementState(target);
        Assert.That(target.GetState(), Is.EqualTo(1));

        CallIncrementState(target);
        Assert.That(target.GetState(), Is.EqualTo(2));
    }

    [Test]
    public void CallAdd_ReturnsCorrectSum()
    {
        var target = new MethodTarget();
        var result = CallAdd(target, 3, 5);
        Assert.That(result, Is.EqualTo(8));
    }

    [Test]
    public void CallAdd_WithNegativeNumbers()
    {
        var target = new MethodTarget();
        var result = CallAdd(target, -10, 3);
        Assert.That(result, Is.EqualTo(-7));
    }

    [Test]
    public void CallConcat_ReturnsCorrectString()
    {
        var target = new MethodTarget();
        var result = CallConcat(target, "Hello", " ", "World");
        Assert.That(result, Is.EqualTo("Hello World"));
    }

    [Test]
    public void CallSetState_SetsState()
    {
        var target = new MethodTarget();
        CallSetState(target, 42);
        Assert.That(target.GetState(), Is.EqualTo(42));
    }

    [Test]
    public void MultipleMethodCalls_WorkCorrectly()
    {
        var target = new MethodTarget();

        CallSetState(target, 10);
        Assert.That(target.GetState(), Is.EqualTo(10));

        CallIncrementState(target);
        Assert.That(target.GetState(), Is.EqualTo(11));

        var sum = CallAdd(target, target.GetState(), 9);
        Assert.That(sum, Is.EqualTo(20));
    }
}
