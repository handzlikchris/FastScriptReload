using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    class FieldsWalker : CSharpSyntaxWalker {
        private Dictionary<string, List<string>> _typeNameToFieldDeclarations = new Dictionary<string, List<string>>(); 
			
        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            base.VisitIdentifierName(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            var className = node.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.ToString();
            var fieldName = node.Declaration.Variables.First().Identifier.ToString();
				
            if(!_typeNameToFieldDeclarations.ContainsKey(className)) {
                _typeNameToFieldDeclarations[className] = new List<string>();
            }
            _typeNameToFieldDeclarations[className].Add(fieldName);
				
            base.VisitFieldDeclaration(node);
        }
			
        public Dictionary<string, List<string>> GetTypeToFieldDeclarations() {
            return _typeNameToFieldDeclarations;
        }
    }
}