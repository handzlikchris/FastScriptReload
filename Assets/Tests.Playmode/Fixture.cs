using Moq; // got necessary DLLs from NuGet, then referenced Moq.dll in Tests.asmdef
using NUnit.Framework;
using UnityEngine;

public class Fixture {
    private Source source;

    private string value; // this field is important, bug doesn't appear if this is made a variable in Test

    [Test]
    public void Test() {
        var dependency2 = new Dependency2(); 
        var dependency1 = new Mock<IDependency1>(); 
        value = "anything";
        dependency1
            .Setup(it => it.Do(It.IsAny<string>()))
            .Callback<string>(_ => Debug.Log($"Do not do {value}"));
        source = new Source(dependency1.Object, dependency2);
        dependency2.Add();
    }
}