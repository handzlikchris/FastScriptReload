using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    class FieldsWalker : CSharpSyntaxWalker {
        private readonly Dictionary<string, List<string>> _typeNameToFieldDeclarations = new(); 
	
        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            var fieldName = node.Declaration.Variables.First().Identifier.ToString();
            var fullClassName = RoslynUtils.GetMemberFQDNWithoutMemberName(node);
            if(!_typeNameToFieldDeclarations.ContainsKey(fullClassName)) {
                _typeNameToFieldDeclarations[fullClassName] = new List<string>();
            }
		
            _typeNameToFieldDeclarations[fullClassName].Add(fieldName);
		
            base.VisitFieldDeclaration(node);
        }

        public Dictionary<string, List<string>> GetTypeToFieldDeclarations() {
            return _typeNameToFieldDeclarations;
        }
    }
}