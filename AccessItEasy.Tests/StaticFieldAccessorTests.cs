using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
[NonParallelizable] // static accessing
public class StaticFieldAccessorTests
{
    #region Accessors

    // Static getter: first param is type marker only
    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticIntField")]
    private static extern int GetStaticIntField([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _);

    // Static setter: first param is type marker, second is the value
    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticIntField")]
    private static extern void SetStaticIntField([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _, int value);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticStringField")]
    private static extern string GetStaticStringField([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticStringField")]
    private static extern void SetStaticStringField([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _, string value);

    #endregion

    [SetUp]
    public void SetUp()
    {
        StaticFieldTarget.ResetFields();
    }

    [Test]
    public void GetStaticIntField_ReturnsCorrectValue()
    {
        var result = GetStaticIntField(null);
        Assert.That(result, Is.EqualTo(100));
    }

    [Test]
    public void SetStaticIntField_ModifiesValue()
    {
        SetStaticIntField(null, 200);
        Assert.That(StaticFieldTarget.GetStaticIntFieldPublic(), Is.EqualTo(200));
    }

    [Test]
    public void GetStaticStringField_ReturnsCorrectValue()
    {
        var result = GetStaticStringField(null);
        Assert.That(result, Is.EqualTo("static hello"));
    }

    [Test]
    public void SetStaticStringField_ModifiesValue()
    {
        SetStaticStringField(null, "modified static");
        Assert.That(StaticFieldTarget.GetStaticStringFieldPublic(), Is.EqualTo("modified static"));
    }

    [Test]
    public void RoundTrip_StaticIntField()
    {
        SetStaticIntField(null, 777);
        var result = GetStaticIntField(null);
        Assert.That(result, Is.EqualTo(777));
    }
}
