using System;

public class Source {
    public Source(IDependency1 dependency1, Dependency2 dependency2) {
        dependency2.Added += () => dependency1.Do("something");
    }
}

public interface IDependency1 {
    void Do(string what); // parameter is important, bug doesn't appear if method doesn't have parameters
}

public class Dependency2 {
    public event Action Added = delegate { };
    public void Add() => Added();
}