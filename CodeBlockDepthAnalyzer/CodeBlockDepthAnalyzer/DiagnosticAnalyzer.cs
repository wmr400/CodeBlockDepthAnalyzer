using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeBlockDepthAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CodeBlockDepthAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CodeBlockDepthAnalyzer";

        private const int _maxCodeBlockDepth = 2;

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterCodeBlockAction(AnalyzeCodeBlock);
        }

        private void AnalyzeCodeBlock(CodeBlockAnalysisContext context)
        {
            // We only want to check code blocks inside method declarations.
            if (!(context.CodeBlock is MethodDeclarationSyntax))
            {
                return;
            }

            var methodDeclaration = (MethodDeclarationSyntax)context.CodeBlock;
            RunDiagnostics(context, methodDeclaration.Body, 0);
        }

        private void RunDiagnostics(CodeBlockAnalysisContext context, BlockSyntax codeBlock, int depth)
        {
            if (depth > _maxCodeBlockDepth)
            {
                // Nesting too deep: report diagnostic.
                var diagnostic = Diagnostic.Create(Rule, codeBlock.Parent.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }

            foreach (var statement in codeBlock.Statements)
            {
                BlockSyntax childCodeBlock = null;

                if (statement is IfStatementSyntax)
                {
                    childCodeBlock = (BlockSyntax)((IfStatementSyntax)statement).Statement;
                }
                else if (statement is DoStatementSyntax)
                {
                    childCodeBlock = (BlockSyntax)((DoStatementSyntax)statement).Statement;
                }
                else if (statement is WhileStatementSyntax)
                {
                    childCodeBlock = (BlockSyntax)((WhileStatementSyntax)statement).Statement;
                }
                else if (statement is ForEachStatementSyntax)
                {
                    childCodeBlock = (BlockSyntax)((ForEachStatementSyntax)statement).Statement;
                }
                else if (statement is ForStatementSyntax)
                {
                    childCodeBlock = (BlockSyntax)((ForStatementSyntax)statement).Statement;
                }

                if (childCodeBlock != null)
                {
                    RunDiagnostics(context, childCodeBlock, depth + 1);
                }
            }
        }
    }
}
