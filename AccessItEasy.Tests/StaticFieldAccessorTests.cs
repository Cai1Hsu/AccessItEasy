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

    // Static reference: returns ref to the field
    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticIntField")]
    private static extern ref int GetStaticIntFieldRef([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticStringField")]
    private static extern string GetStaticStringField([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticStringField")]
    private static extern void SetStaticStringField([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _, string value);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticStringField")]
    private static extern ref string GetStaticStringFieldRef([PrivateAccessorType("AccessItEasy.Tests.StaticFieldTarget")] object? _);

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

    #region Static Reference Accessor Tests

    [Test]
    public void GetStaticIntFieldRef_ReturnsReference()
    {
        ref int fieldRef = ref GetStaticIntFieldRef(null);
        Assert.That(fieldRef, Is.EqualTo(100));
    }

    [Test]
    public void GetStaticIntFieldRef_ModifyThroughReference()
    {
        ref int fieldRef = ref GetStaticIntFieldRef(null);
        fieldRef = 888;
        Assert.That(StaticFieldTarget.GetStaticIntFieldPublic(), Is.EqualTo(888));
    }

    [Test]
    public void GetStaticStringFieldRef_ReturnsReference()
    {
        ref string fieldRef = ref GetStaticStringFieldRef(null);
        Assert.That(fieldRef, Is.EqualTo("static hello"));
    }

    [Test]
    public void GetStaticStringFieldRef_ModifyThroughReference()
    {
        ref string fieldRef = ref GetStaticStringFieldRef(null);
        fieldRef = "ref modified";
        Assert.That(StaticFieldTarget.GetStaticStringFieldPublic(), Is.EqualTo("ref modified"));
    }

    [Test]
    public void RoundTrip_StaticIntFieldRef()
    {
        ref int fieldRef = ref GetStaticIntFieldRef(null);
        fieldRef = 54321;
        Assert.That(GetStaticIntField(null), Is.EqualTo(54321));
    }

    #endregion
}
