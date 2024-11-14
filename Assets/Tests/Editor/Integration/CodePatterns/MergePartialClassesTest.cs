using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using FastScriptReload.Editor.Compilation.CodeRewriting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Tests.Editor.Integration.CodePatterns
{
    public class SyntaxTreeMergePartialsTest
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "PartialSyntaxTreeTests");
            Directory.CreateDirectory(_tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempPath))
                Directory.Delete(_tempPath, true);
        }

        [UnityTest]
        public IEnumerator Partials_ClassMergeTest()
        {
            // Arrange
            const string partialClass1 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    private int _field1;
                    public void Method1() { }
                }
            }";

            const string partialClass2 = @"
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    private int _field2;
                    public void Method2() { }
                }
            }";

            yield return MergeAndTest_CSharpPartialClass(partialClass1, partialClass2);
        }

        [UnityTest]
        public IEnumerator Partials_ClassMergeWithoutNamespaceTest()
        {
            // Arrange
            const string partialClass1 = @"
            using System;
            public partial class TestClass
            {
                private int _field1;
            }";

            const string partialClass2 = @"
            using System.Collections.Generic;
            public partial class TestClass
            {
                private int _field2;
            }";

            var trees = CombineTrees(partialClass1, partialClass2);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            // Assert
            var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            Assert.That("TestClass", Is.EqualTo(classDeclaration.Identifier.Text));

            // Test Fields
            var fields = classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            Assert.That(fields.Count, Is.EqualTo(2));

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.That(usingDirectives.Count, Is.EqualTo(2));
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System.Collections.Generic"),
                "Should have using System.Collections.Generic");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partials_StructMergeTest()
        {
            // Arrange
            const string partialStruct1 = @"
            using System;
            namespace TestNamespace
            {
                public partial struct TestStruct
                {
                    private int _field1;
                    public void Method1() { }
                }
            }";

            const string partialStruct2 = @"
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public partial struct TestStruct
                {
                    private int _field2;
                    public void Method2() { }
                }
            }";

            var trees = CombineTrees(partialStruct1, partialStruct2);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            // Assert
            // Test Namespace
            var namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            Assert.That(namespaceDeclaration, Is.Not.Null);
            Assert.That(namespaceDeclaration.Name.ToString(), Is.EqualTo("TestNamespace"));

            var structDeclaration = root.DescendantNodes().OfType<StructDeclarationSyntax>().First();
            Assert.That(structDeclaration.Identifier.Text, Is.EqualTo("TestStruct"));

            // Test Fields
            var fields = structDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            Assert.That(fields.Count, Is.EqualTo(2));

            // Test Methods
            var methods = structDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            Assert.That(methods.Count, Is.EqualTo(2));
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method1"), "Combined class should contain Method1");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method2"), "Combined class should contain Method2");

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.That(usingDirectives.Count, Is.EqualTo(2));
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System.Collections.Generic"),
                "Should have using System.Collections.Generic");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partials_MethodMergeTest()
        {
            // Arrange
            const string partialClass1 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    int _field1;
                    partial void Method1() { }
                }
            }";

            const string partialClass2 = @"
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    int _field2;
                    partial void Method2() { }
                }
            }";

            yield return MergeAndTest_CSharpPartialClass(partialClass1, partialClass2);
        }

        private IEnumerator MergeAndTest_CSharpPartialClass(params string[] files)
        {
            var trees = CombineTrees(files);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            // Assert
            // Test Namespace
            var namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            Assert.That(namespaceDeclaration, Is.Not.Null);
            Assert.That(namespaceDeclaration.Name.ToString(), Is.EqualTo("TestNamespace"));

            var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            Assert.That(classDeclaration.Identifier.Text, Is.EqualTo("TestClass"));

            // Test Fields
            var fields = classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            Assert.That(fields.Count, Is.EqualTo(2));

            // Test Methods
            var methods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            Assert.That(methods.Count, Is.EqualTo(2));
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method1"), "Combined class should contain Method1");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method2"), "Combined class should contain Method2");

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.That(usingDirectives.Count, Is.EqualTo(2));
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System.Collections.Generic"),
                "Should have using System.Collections.Generic");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partials_NoDuplicateUsingsMergeTest()
        {
            // Arrange
            const string partialClass1 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClass { }
            }";

            const string partialClass2 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClass { }
            }";

            var trees = CombineTrees(partialClass1, partialClass2);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            // Assert
            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.That(usingDirectives.Count, Is.EqualTo(1));
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System"), "Should have using System");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partials_ClassWithFieldsMergeTest()
        {
            // Arrange
            const string partialClass1 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClassWithFields
                {
                    public int Field1;
                    private string _privateField1;
                    protected float ProtectedField1;
                }
            }";

            const string partialClass2 = @"
            using System;
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public partial class TestClassWithFields
                {
                    public List<int> Field2;
                    private DateTime _privateField2;
                    protected bool ProtectedField2;
                }
            }";

            var trees = CombineTrees(partialClass1, partialClass2);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            // Assert
            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            // Test Namespace
            var namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            Assert.That(namespaceDeclaration, Is.Not.Null);
            Assert.That(namespaceDeclaration.Name.ToString(), Is.EqualTo("TestNamespace"));

            var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            Assert.That(classDeclaration.Identifier.Text, Is.EqualTo("TestClassWithFields"));

            // Test Fields
            var fields = classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            Assert.That(fields.Count, Is.EqualTo(6));

            // Test specific fields
            Assert.IsTrue(
                fields.Any(f => f.Declaration.Variables.Any(v =>
                    v.Identifier.Text == "Field1" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))),
                "Should have public Field1");
            Assert.IsTrue(
                fields.Any(f => f.Declaration.Variables.Any(v =>
                    v.Identifier.Text == "_privateField1" &&
                    f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))), "Should have private _privateField1");
            Assert.IsTrue(
                fields.Any(f => f.Declaration.Variables.Any(v =>
                    v.Identifier.Text == "ProtectedField1" &&
                    f.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))),
                "Should have protected ProtectedField1");
            Assert.IsTrue(
                fields.Any(f => f.Declaration.Variables.Any(v =>
                    v.Identifier.Text == "Field2" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))),
                "Should have public Field2");
            Assert.IsTrue(
                fields.Any(f => f.Declaration.Variables.Any(v =>
                    v.Identifier.Text == "_privateField2" &&
                    f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))), "Should have private _privateField2");
            Assert.IsTrue(
                fields.Any(f => f.Declaration.Variables.Any(v =>
                    v.Identifier.Text == "ProtectedField2" &&
                    f.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))),
                "Should have protected ProtectedField2");

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.That(usingDirectives.Count, Is.EqualTo(2));
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System.Collections.Generic"),
                "Should have using System.Collections.Generic");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partials_SameFileMergeTest()
        {
            // Arrange
            const string sameFilePartials = @"
            using System;
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    private int _field1;
                    public void Method1() { }
                }

                public partial class TestClass
                {
                    private string _field2;
                    public void Method2() { }
                }
            }";

            var trees = CombineTrees(sameFilePartials);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            // Assert
            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            // Test Namespace
            var namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            Assert.That(namespaceDeclaration, Is.Not.Null);
            Assert.That(namespaceDeclaration.Name.ToString(), Is.EqualTo("TestNamespace"));

            var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            Assert.That(classDeclaration.Identifier.Text, Is.EqualTo("TestClass"));

            // Test Fields
            var fields = classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            Assert.That(fields.Count, Is.EqualTo(2));
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_field1")),
                "Should have _field1");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_field2")),
                "Should have _field2");

            // Test Methods
            var methods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            Assert.That(methods.Count, Is.EqualTo(2));
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method1"), "Combined class should contain Method1");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method2"), "Combined class should contain Method2");

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.That(usingDirectives.Count, Is.EqualTo(2));
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System.Collections.Generic"),
                "Should have using System.Collections.Generic");

            // Test that there's only one class declaration
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            Assert.That(classDeclarations.Count, Is.EqualTo(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partials_IgnoreInnerTypesTest()
        {
            // Arrange
            const string partialClass1 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    private int _field1;

                    private class InnerClass
                    {
                        public int innerField;
                    }
                }
            }";

            const string partialClass2 = @"
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    private string _field2;

                    private struct InnerStruct
                    {
                        public int innerField;
                    }
                }
            }";

            var trees = CombineTrees(partialClass1, partialClass2);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            // Assert
            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            Assert.That(classDeclarations.Count, Is.EqualTo(2));

            var structDeclarations = root.DescendantNodes().OfType<StructDeclarationSyntax>().ToList();
            Assert.That(structDeclarations.Count, Is.EqualTo(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Partials_PreserveUsingAliasTest()
        {
            // Arrange
            const string partialClass1 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClass { }
            }";

            const string partialClass2 = @"
            using StringList = System.Collections.Generic.List<string>;
            namespace TestNamespace
            {
                public partial class TestClass { }
            }";

            var trees = CombineTrees(partialClass1, partialClass2);

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            // Assert
            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.That(usingDirectives.Count, Is.EqualTo(2));
            Assert.IsTrue(usingDirectives.Any(u => u.Name!.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u =>
                    u.Alias != null && u.Alias.Name.ToString() == "StringList" &&
                    u.Name!.ToString() == "System.Collections.Generic.List<string>"),
                "Should have using StringList = System.Collections.Generic.List<string>");

            yield return null;
        }

        private List<SyntaxTree> CombineTrees(params string[] files) {
            return files.Select((it, i) => {
                var path = Path.Combine(_tempPath, $"PartialClass{i}.cs");
                File.WriteAllText(path, it);
                return CSharpSyntaxTree.ParseText(it, path: path);
            }).ToList();
        }
    }
}
