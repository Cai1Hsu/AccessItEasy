using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
public class AccessorAttributeTest
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Foo(int x1, int x2, int x3) => x1 + x2 + x3;

    [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "Foo")]
    [return: PrivateAccessorType("System.Int32")]
    private static extern object CallFoo(
        [PrivateAccessorType("AccessItEasy.Tests.AccessorAttributeTest")] object _,
        [PrivateAccessorType("System.Int32")] object a,
        [PrivateAccessorType("System.Int32")] object b,
        [PrivateAccessorType("System.Int32")] object c);

    [Test]
    public void TestCallFoo_AccessorWorks()
    {
        int result = (int)CallFoo(null!, 1, 2, 3);

        Assert.That(result, Is.EqualTo(6));
    }

    private static readonly MethodInfo callFooMethodInfo = typeof(AccessorAttributeTest).GetMethod("CallFoo", BindingFlags.Static | BindingFlags.NonPublic)!;

    [Test]
    public void TestCallFoo_MethodAttributesRemoved()
    {
        var attributes = callFooMethodInfo.
            GetCustomAttributes(typeof(PrivateAccessorAttribute), false);

        Assert.That(attributes, Is.Empty);
    }

    [Test]
    public void TestCallFoo_ParameterAttributesRemoved()
    {
        var parameters = callFooMethodInfo.GetParameters();

        foreach (var parameter in parameters)
        {
            var attributes = parameter.GetCustomAttributes(typeof(PrivateAccessorTypeAttribute), false);
            Assert.That(attributes, Is.Empty);
        }
    }

    [Test]
    public void TestCallFoo_ReturnAttributesRemoved()
    {
        var attributes = callFooMethodInfo.ReturnTypeCustomAttributes.GetCustomAttributes(typeof(PrivateAccessorTypeAttribute), false);
        Assert.That(attributes, Is.Empty);
    }

    [Test]
    public void TestCallFoo_CompilerGeneratedAttributeAdded()
    {
        var attributes = callFooMethodInfo.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false);
        Assert.That(attributes, Is.Not.Empty);
    }
}
