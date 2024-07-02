using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using FastScriptReload.Editor.Compilation.CodeRewriting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FastScriptReload.Tests.Editor.Integration.CodePatterns
{
    public class MergePartialClassesTest
    {
        private string tempPath;

        [SetUp]
        public void SetUp()
        {
            tempPath = Path.Combine(Path.GetTempPath(), "PartialSyntaxTreeTests");
            Directory.CreateDirectory(tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
        }

        [UnityTest]
        public IEnumerator Partials_MergePartialClasses()
        {
            // Arrange
            var partialClass1 = @"
            using System;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    public void Method1() { }
                }
            }";

            var partialClass2 = @"
            using System.Collections.Generic;
            namespace TestNamespace
            {
                public partial class TestClass
                {
                    public void Method2() { }
                }
            }";

            var path1 = Path.Combine(tempPath, "PartialClass1.cs");
            var path2 = Path.Combine(tempPath, "PartialClass2.cs");
            File.WriteAllText(path1, partialClass1);
            File.WriteAllText(path2, partialClass2);

            var tree1 = CSharpSyntaxTree.ParseText(partialClass1, path: path1);
            var trees = new List<SyntaxTree>
            {
                    tree1
            };
            var definedPreprocessorSymbols = new List<string>
            {
                    "DEBUG", "UNITY_EDITOR"
            };

            var result = trees.CombinePartials(definedPreprocessorSymbols, out var typesDefined);

            Assert.AreEqual(1, result.Count(), "Should result in a single combined tree");

            var combinedTree = result.First();
            var root = combinedTree.GetCompilationUnitRoot();

            var classDeclaration = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
            Assert.AreEqual("TestClass", classDeclaration.Identifier.Text, "Class name should be TestClass");

            var methods = classDeclaration.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().ToList();
            Assert.AreEqual(2, methods.Count, "Combined class should have two methods");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method1"), "Combined class should contain Method1");
            Assert.IsTrue(methods.Any(m => m.Identifier.Text == "Method2"), "Combined class should contain Method2");

            var usingDirectives = root.Usings;
            Assert.AreEqual(2, usingDirectives.Count, "Should have two using directives");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System"), "Should have using System");
            Assert.IsTrue(usingDirectives.Any(u => u.Name.ToString() == "System.Collections.Generic"), "Should have using System.Collections.Generic");

            Assert.IsTrue(typesDefined.Contains("TestClass"), "TestClass should be in typesDefined");

            yield return null;
        }
    }
}
