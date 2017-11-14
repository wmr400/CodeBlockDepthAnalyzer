using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.Formatting;

namespace CodeBlockDepthAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CodeBlockDepthAnalyzerCodeFixProvider)), Shared]
    public class CodeBlockDepthAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string title = "Reduce nesting";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(CodeBlockDepthAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }
        
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            SyntaxNode n = root.FindNode(diagnosticSpan);

            BlockSyntax node = root.FindNode(diagnosticSpan).ChildNodes().OfType<BlockSyntax>().First();
            var s = node.Statements;

            var currentnode = root.FindNode(diagnosticSpan).ChildNodes().First();

            ClassDeclarationSyntax cds = root.FindNode(diagnosticSpan).Ancestors().OfType<ClassDeclarationSyntax>().Single();


            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => ExtractMethodAsync(context.Document, cds, n, c, node),
                    equivalenceKey: title),
                diagnostic);

            var t = root.FindToken(diagnosticSpan.Start);

            //// Find the type declaration identified by the diagnostic.
            //var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

            //// Register a code action that will invoke the fix.
            //context.RegisterCodeFix(
            //    CodeAction.Create(
            //        title: title,
            //        createChangedSolution: c => MakeUppercaseAsync(context.Document, declaration, c),
            //        equivalenceKey: title),
            //    diagnostic);
        }

        private async Task<Document> ExtractMethodAsync(Document document, ClassDeclarationSyntax cds, SyntaxNode n, CancellationToken c, BlockSyntax node)
        {
            var oldDocument = document;

            var oldSyntaxRoot = await oldDocument.GetSyntaxRootAsync(c);

            //var statement = ((IfStatementSyntax)n).Statement;

            var newNode = MethodDeclaration(ParseTypeName("void"), "NewMethod")
                .AddModifiers(Token(SyntaxKind.PrivateKeyword))
                .WithBody(Block(ParseStatement(n.ToString())));
            //.WithBody(Block((IfStatementSyntax)n));

            var invocation = InvocationExpression(IdentifierName("NewMethod"));
            //SyntaxFactory.ArgumentList(
            //    SyntaxFactory.SingletonSeparatedList(
            //        SyntaxFactory.Argument(
            //            SyntaxFactory.IdentifierName("blaat")
            //            )
            //        )
            //    )
            //);

            var p = n.Parent;

            var changed = p.ReplaceNode(n, ParseStatement(invocation.ToString()));
            var newCds = cds.AddMembers(newNode);
            var newSyntaxRoot = oldSyntaxRoot.ReplaceNode(cds, newCds);

            var newDocument = oldDocument.WithSyntaxRoot(newSyntaxRoot);

            return await Formatter.FormatAsync(newDocument);

            //return newDocument;
        }

        private async Task<Solution> MakeUppercaseAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Compute new uppercase name.
            var identifierToken = typeDecl.Identifier;
            var newName = identifierToken.Text.ToUpperInvariant();

            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

            // Return the new solution with the now-uppercase type name.
            return newSolution;
        }
    }
}