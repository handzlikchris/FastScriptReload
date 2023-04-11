using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    public class NewFieldDeclaration
    {
        public string FieldName { get; }
        public string TypeName { get; }
        public FieldDeclarationSyntax FieldDeclarationSyntax { get; } //TODO: PERF: will that block whole tree from being garbage collected

        public NewFieldDeclaration(string fieldName, string typeName, FieldDeclarationSyntax fieldDeclarationSyntax)
        {
            FieldName = fieldName;
            TypeName = typeName;
            FieldDeclarationSyntax = fieldDeclarationSyntax;
        }
    }
}