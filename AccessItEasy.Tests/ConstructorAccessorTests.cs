using NUnit.Framework;

namespace AccessItEasy.Tests;

[TestFixture]
public class ConstructorAccessorTests
{
    #region Accessors

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    private static extern ConstructorTarget CreateDefault();

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    private static extern ConstructorTarget CreateWithInt(int value);

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    private static extern ConstructorTarget CreateWithIntAndString(int value, string name);

    // Test with PrivateAccessorType for internal type
    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    [return: PrivateAccessorType("AccessItEasy.Tests.InternalTarget")]
    private static extern object CreateInternalTarget();

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    [return: PrivateAccessorType("AccessItEasy.Tests.InternalTarget")]
    private static extern object CreateInternalTargetWithSecret(int secret);

    #endregion

    [Test]
    public void CreateDefault_CreatesInstanceWithDefaultValues()
    {
        var instance = CreateDefault();

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.Value, Is.EqualTo(0));
        Assert.That(instance.Name, Is.EqualTo("default"));
    }

    [Test]
    public void CreateWithInt_CreatesInstanceWithValue()
    {
        var instance = CreateWithInt(42);

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.Value, Is.EqualTo(42));
        Assert.That(instance.Name, Is.EqualTo("int-constructor"));
    }

    [Test]
    public void CreateWithInt_DifferentValues()
    {
        var instance1 = CreateWithInt(100);
        var instance2 = CreateWithInt(-50);

        Assert.That(instance1.Value, Is.EqualTo(100));
        Assert.That(instance2.Value, Is.EqualTo(-50));
    }

    [Test]
    public void CreateWithIntAndString_CreatesInstanceWithBothValues()
    {
        var instance = CreateWithIntAndString(123, "custom name");

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.Value, Is.EqualTo(123));
        Assert.That(instance.Name, Is.EqualTo("custom name"));
    }

    [Test]
    public void CreateInternalTarget_CreatesInternalInstance()
    {
        var instance = CreateInternalTarget();

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.GetType().Name, Is.EqualTo("InternalTarget"));

        // Use reflection to verify the internal state
        var getSecretMethod = instance.GetType().GetMethod("GetSecret");
        var secret = getSecretMethod?.Invoke(instance, null);
        Assert.That(secret, Is.EqualTo(999));
    }

    [Test]
    public void CreateInternalTargetWithSecret_CreatesInternalInstanceWithSecret()
    {
        var instance = CreateInternalTargetWithSecret(12345);

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.GetType().Name, Is.EqualTo("InternalTarget"));

        // Use reflection to verify the internal state
        var getSecretMethod = instance.GetType().GetMethod("GetSecret");
        var secret = getSecretMethod?.Invoke(instance, null);
        Assert.That(secret, Is.EqualTo(12345));
    }

    [Test]
    public void MultipleConstructorCalls_CreateIndependentInstances()
    {
        var instance1 = CreateWithIntAndString(1, "first");
        var instance2 = CreateWithIntAndString(2, "second");
        var instance3 = CreateWithIntAndString(3, "third");

        Assert.That(instance1.Value, Is.EqualTo(1));
        Assert.That(instance2.Value, Is.EqualTo(2));
        Assert.That(instance3.Value, Is.EqualTo(3));

        Assert.That(instance1, Is.Not.SameAs(instance2));
        Assert.That(instance2, Is.Not.SameAs(instance3));
    }
}
