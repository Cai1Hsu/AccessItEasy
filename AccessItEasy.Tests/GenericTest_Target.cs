using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
public class GenericTest
{
    private List<int> numbers = new List<int>();
    private Box<int> intBox = new Box<int>();

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
    private static extern void CallAdd(List<int> list, int number);

    [SetUp]
    public void SetUp()
    {
        numbers.Clear();
        intBox.Value = default;
    }

    [Test]
    public void CallAdd_AddsNumberToList()
    {
        PerformAccessTest(CallAdd);
    }

    [Test]
    public void GenericHelper_AddsItemToList()
    {
        PerformAccessTest(GenericHelper<int>.CallAdd);
    }

    private void PerformAccessTest(Action<List<int>, int> addAction)
    {
        Assert.That(numbers.Count, Is.EqualTo(0));

        addAction(numbers, 42);
        Assert.That(numbers.Count, Is.EqualTo(1));
        Assert.That(numbers[0], Is.EqualTo(42));

        addAction(numbers, 84);
        Assert.That(numbers.Count, Is.EqualTo(2));
    }

    private static class GenericHelper<T>
    {
        [PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
        public static extern void CallAdd(List<T> list, T item);
    }


    // test if field accessors work with generics
    public class Box<T>
    {
        public T Value = default!;

        public Box()
        {
        }

        public Box(T value)
        {
            Value = value;
        }
    }

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
    private static extern T BoxGetValue<T>(Box<T> box);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
    private static extern void BoxSetValue<T>(Box<T> box, T value);

    private class BoxGenericHelper<T>
    {
        [PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
        public static extern T GetValue(Box<T> box);

        [PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
        public static extern void SetValue(Box<T> box, T value);

        [PrivateAccessor(PrivateAccessorKind.Constructor)]
        public static extern Box<T> CreateBox(T value);
    }

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
    private static extern int BoxGetValueInt(Box<int> box);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
    private static extern void BoxSetValueInt(Box<int> box, int value);

    [Test]
    [TestCase(10)]
    [TestCase(42)]
    public void FieldAccessorGetTest(int expectedValue)
    {
        Assert.That(intBox.Value, Is.EqualTo(default(int)));

        intBox.Value = expectedValue;

        int value1 = BoxGetValueInt(intBox);
        int value2 = BoxGenericHelper<int>.GetValue(intBox);
        int value3 = BoxGetValue<int>(intBox);

        Assert.Multiple(() =>
        {
            Assert.That(value1, Is.EqualTo(expectedValue));
            Assert.That(value2, Is.EqualTo(expectedValue));
            Assert.That(value3, Is.EqualTo(expectedValue));
        });
    }

    private static IEnumerable<Action<Box<int>, int>> FieldSetterTestCases()
    {
        yield return BoxSetValueInt;
        yield return BoxGenericHelper<int>.SetValue;
        yield return BoxSetValue<int>;
    }

    [TestCaseSource(nameof(FieldSetterTestCases))]
    public void FieldAccessorSetTest(Action<Box<int>, int> setAction)
    {
        intBox.Value = default;

        setAction(intBox, 10);
        Assert.That(intBox.Value, Is.EqualTo(10));

        setAction(intBox, 42);
        Assert.That(intBox.Value, Is.EqualTo(42));
    }

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    private static extern Box<T> CreateBox<T>(T value);

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    private static extern Box<int> CreateIntBox(int value);

    [Test]
    [TestCase(10)]
    [TestCase(42)]
    public void ConstructorAccessTest(int expectedValue)
    {
        Box<int> box1 = CreateIntBox(expectedValue);
        Box<int> box2 = CreateBox<int>(expectedValue);
        Box<int> box3 = BoxGenericHelper<int>.CreateBox(expectedValue);

        Assert.Multiple(() =>
        {
            Assert.That(box1.Value, Is.EqualTo(expectedValue));
            Assert.That(box2.Value, Is.EqualTo(expectedValue));
            Assert.That(box3.Value, Is.EqualTo(expectedValue));
        });
    }
}
