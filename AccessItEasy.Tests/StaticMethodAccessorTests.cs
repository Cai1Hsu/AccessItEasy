using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
[NonParallelizable] // static accessing
public class StaticMethodAccessorTests
{
    #region Accessors

    // First parameter is always the type marker for static methods (not forwarded)
    [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "IncrementStaticState")]
    private static extern void CallIncrementStaticState([PrivateAccessorType("AccessItEasy.Tests.StaticMethodTarget")] object? _);

    // First parameter is type marker, remaining parameters are forwarded
    [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "Multiply")]
    private static extern int CallMultiply([PrivateAccessorType("AccessItEasy.Tests.StaticMethodTarget")] object? _, int a, int b);

    [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "FormatMessage")]
    private static extern string CallFormatMessage([PrivateAccessorType("AccessItEasy.Tests.StaticMethodTarget")] object? _, string template, int value);

    #endregion

    [SetUp]
    public void SetUp()
    {
        StaticMethodTarget.ResetState();
    }

    [Test]
    public void CallIncrementStaticState_IncrementsState()
    {
        Assert.That(StaticMethodTarget.GetStaticState(), Is.EqualTo(0));

        CallIncrementStaticState(null);
        Assert.That(StaticMethodTarget.GetStaticState(), Is.EqualTo(1));

        CallIncrementStaticState(null);
        Assert.That(StaticMethodTarget.GetStaticState(), Is.EqualTo(2));
    }

    [Test]
    public void CallMultiply_ReturnsCorrectProduct()
    {
        var result = CallMultiply(null, 6, 7);
        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void CallMultiply_WithZero()
    {
        var result = CallMultiply(null, 100, 0);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void CallMultiply_WithNegativeNumbers()
    {
        var result = CallMultiply(null, -5, 3);
        Assert.That(result, Is.EqualTo(-15));
    }

    [Test]
    public void CallFormatMessage_ReturnsFormattedString()
    {
        var result = CallFormatMessage(null, "Value is {0}", 42);
        Assert.That(result, Is.EqualTo("Value is 42"));
    }

    [Test]
    public void CallFormatMessage_WithDifferentValues()
    {
        var result = CallFormatMessage(null, "Count: {0}", 100);
        Assert.That(result, Is.EqualTo("Count: 100"));
    }
}
