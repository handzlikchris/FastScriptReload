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

            yield return MergeAndTest_CSharpPartialStruct(partialStruct1, partialStruct2);
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

        private IEnumerator MergeAndTest_CSharpPartialClass(string partialClass1, string partialClass2)
        {
            var path1 = Path.Combine(_tempPath, "PartialClass1.cs");
            var path2 = Path.Combine(_tempPath, "PartialClass2.cs");
            File.WriteAllText(path1, partialClass1);
            File.WriteAllText(path2, partialClass2);

            var tree1 = CSharpSyntaxTree.ParseText(partialClass1, path: path1);
            var tree2 = CSharpSyntaxTree.ParseText(partialClass2, path: path1);
            var trees = new List<SyntaxTree>
            {
                    tree1, tree2
            };

            var result = trees.MergePartials(new List<string>
            {
                    "DEBUG", "UNITY_EDITOR"
            });

            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            var classDeclaration = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
            Assert.AreEqual("TestClass", classDeclaration.Identifier.Text, "Class name should be TestClass");

            //Test Fields
            var fields = classDeclaration.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>().ToList();
            Assert.AreEqual(2, fields.Count, "Combined class should have two fields");

            //Test Methods
            var methods = classDeclaration.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().ToList();
            Assert.AreEqual(2, methods.Count, "Combined class should have two methods");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method1"), "Combined class should contain Method1");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method2"), "Combined class should contain Method2");

            //Test Usings
            var usingDirectives = root.Usings;
            Assert.AreEqual(2, usingDirectives.Count, "Should have two using directives");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System.Collections.Generic"), "Should have using System.Collections.Generic");

            yield return null;
        }

        private IEnumerator MergeAndTest_CSharpPartialStruct(string partialClass1, string partialClass2)
        {
            var path1 = Path.Combine(_tempPath, "PartialClass1.cs");
            var path2 = Path.Combine(_tempPath, "PartialClass2.cs");
            File.WriteAllText(path1, partialClass1);
            File.WriteAllText(path2, partialClass2);

            var tree1 = CSharpSyntaxTree.ParseText(partialClass1, path: path1);
            var tree2 = CSharpSyntaxTree.ParseText(partialClass2, path: path1);
            var trees = new List<SyntaxTree>
            {
                    tree1, tree2
            };

            var result = trees.MergePartials(new List<string>
            {
                    "DEBUG", "UNITY_EDITOR"
            });

            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            var structDeclaration = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax>().First();
            Assert.AreEqual("TestStruct", structDeclaration.Identifier.Text, "Struct name should be TestStruct");

            //Test Fields
            var fields = structDeclaration.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>().ToList();
            Assert.AreEqual(2, fields.Count, "Combined class should have two fields");

            //Test Methods
            var methods = structDeclaration.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().ToList();
            Assert.AreEqual(2, methods.Count, "Combined class should have two methods");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method1"), "Combined class should contain Method1");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method2"), "Combined class should contain Method2");

            //Test Usings
            var usingDirectives = root.Usings;
            Assert.AreEqual(2, usingDirectives.Count, "Should have two using directives");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System.Collections.Generic"), "Should have using System.Collections.Generic");

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

            var path1 = Path.Combine(_tempPath, "PartialClassWithFields1.cs");
            var path2 = Path.Combine(_tempPath, "PartialClassWithFields2.cs");
            File.WriteAllText(path1, partialClass1);
            File.WriteAllText(path2, partialClass2);

            var tree1 = CSharpSyntaxTree.ParseText(partialClass1, path: path1);
            var tree2 = CSharpSyntaxTree.ParseText(partialClass2, path: path2);
            var trees = new List<SyntaxTree>
            {
                    tree1, tree2
            };

            // Act
            var result = trees.MergePartials(new List<string>
            {
                    "DEBUG", "UNITY_EDITOR"
            });

            // Assert
            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            var classDeclaration = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
            Assert.AreEqual("TestClassWithFields", classDeclaration.Identifier.Text, "Class name should be TestClassWithFields");

            // Test Fields
            var fields = classDeclaration.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>().ToList();
            Assert.AreEqual(6, fields.Count, "Combined class should have six fields");

            // Test specific fields
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "Field1" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))), "Should have public Field1");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_privateField1" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))), "Should have private _privateField1");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "ProtectedField1" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))), "Should have protected ProtectedField1");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "Field2" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))), "Should have public Field2");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_privateField2" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))), "Should have private _privateField2");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "ProtectedField2" && f.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))), "Should have protected ProtectedField2");

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.AreEqual(2, usingDirectives.Count, "Should have two using directives");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System.Collections.Generic"), "Should have using System.Collections.Generic");

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

            var path = Path.Combine(_tempPath, "SameFilePartials.cs");
            File.WriteAllText(path, sameFilePartials);

            var tree = CSharpSyntaxTree.ParseText(sameFilePartials, path: path);
            var trees = new List<SyntaxTree> { tree };

            // Act
            var result = trees.MergePartials(new List<string> { "DEBUG", "UNITY_EDITOR" });

            // Assert
            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            Assert.AreEqual("TestClass", classDeclaration.Identifier.Text, "Class name should be TestClass");

            // Test Fields
            var fields = classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            Assert.AreEqual(2, fields.Count, "Combined class should have two fields");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_field1")), "Should have _field1");
            Assert.IsTrue(fields.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_field2")), "Should have _field2");

            // Test Methods
            var methods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            Assert.AreEqual(2, methods.Count, "Combined class should have two methods");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method1"), "Combined class should contain Method1");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method2"), "Combined class should contain Method2");

            // Test Usings
            var usingDirectives = root.Usings;
            Assert.AreEqual(2, usingDirectives.Count, "Should have two using directives");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System.Collections.Generic"), "Should have using System.Collections.Generic");

            // Test that there's only one class declaration
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            Assert.AreEqual(1, classDeclarations.Count, "Should only have one class declaration after merging");

            yield return null;
        }
    }
}
