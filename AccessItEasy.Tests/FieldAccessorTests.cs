using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
public class FieldAccessorTests
{
    #region Accessors

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_intField")]
    private static extern int GetIntField(FieldTarget target);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_intField")]
    private static extern void SetIntField(FieldTarget target, int value);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_stringField")]
    private static extern string GetStringField(FieldTarget target);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_stringField")]
    private static extern void SetStringField(FieldTarget target, string value);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_nullableField")]
    private static extern object? GetNullableField(FieldTarget target);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_nullableField")]
    private static extern void SetNullableField(FieldTarget target, object? value);

    #endregion

    [Test]
    public void GetIntField_ReturnsCorrectValue()
    {
        var target = new FieldTarget();
        var result = GetIntField(target);
        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void SetIntField_ModifiesValue()
    {
        var target = new FieldTarget();
        SetIntField(target, 123);
        Assert.That(target.GetIntFieldPublic(), Is.EqualTo(123));
    }

    [Test]
    public void GetStringField_ReturnsCorrectValue()
    {
        var target = new FieldTarget();
        var result = GetStringField(target);
        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void SetStringField_ModifiesValue()
    {
        var target = new FieldTarget();
        SetStringField(target, "world");
        Assert.That(target.GetStringFieldPublic(), Is.EqualTo("world"));
    }

    [Test]
    public void GetNullableField_ReturnsNull()
    {
        var target = new FieldTarget();
        var result = GetNullableField(target);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void SetNullableField_ModifiesValue()
    {
        var target = new FieldTarget();
        var testObject = new object();
        SetNullableField(target, testObject);
        Assert.That(target.GetNullableFieldPublic(), Is.SameAs(testObject));
    }

    [Test]
    public void RoundTrip_IntField()
    {
        var target = new FieldTarget();
        SetIntField(target, 999);
        var result = GetIntField(target);
        Assert.That(result, Is.EqualTo(999));
    }
}
