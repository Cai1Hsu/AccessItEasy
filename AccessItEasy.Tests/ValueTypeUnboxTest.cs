using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
public class ValueTypeUnboxTest
{
    private List<Vector2> vectors = new List<Vector2>();

    // assume that we can't access to Vector2 directly
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
    private static extern void CallAdd([PrivateAccessorType("System.Collections.Generic.List`1[System.Numerics.Vector2]")] object list, [PrivateAccessorType("System.Numerics.Vector2")] object item);

    [SetUp]
    public void SetUp()
    {
        vectors.Clear();
    }

    [Test]
    public void TestAddVector2ToList()
    {
        Assert.That(vectors.Count, Is.EqualTo(0));

        CallAdd(vectors, new Vector2(1, 2));

        Assert.That(vectors.Count, Is.EqualTo(1));
        Assert.That(vectors[0], Is.EqualTo(new Vector2(1, 2)));
    }

    private class HasVector2Value
    {
        public Vector2 Value;
        public Vector2 Value2 => Value;

        public HasVector2Value(Vector2 value)
        {
            Value = value;
        }
    }
    
    [PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
    [return: PrivateAccessorType("System.Numerics.Vector2")]
    private static extern object GetValue(HasVector2Value _);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_Value2")]
    [return: PrivateAccessorType("System.Numerics.Vector2")]
    private static extern object GetValue2(HasVector2Value _);

    [Test]
    public void TestFieldAndPropertyAccess()
    {
        var obj = new HasVector2Value(new Vector2(3, 4));

        var fieldValue = (Vector2)GetValue(obj);
        Assert.That(fieldValue, Is.EqualTo(new Vector2(3, 4)));

        var propertyValue = (Vector2)GetValue2(obj);
        Assert.That(propertyValue, Is.EqualTo(new Vector2(3, 4)));
    }
}
