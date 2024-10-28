﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using RestClient;
using RestClient.Parser;

namespace RestClientVS
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IClassificationTag))]
    [ContentType(LanguageFactory.LanguageName)]
    public class SyntaxHighlighting : TokenClassificationTaggerBase
    {
        public override Dictionary<object, string> ClassificationMap { get; } = new()
        {
            { ItemType.VariableName, PredefinedClassificationTypeNames.MarkupNode },
            { ItemType.VariableValue, PredefinedClassificationTypeNames.String },
            { ItemType.Method, PredefinedClassificationTypeNames.Keyword },
            { ItemType.Url, PredefinedClassificationTypeNames.Literal },
            { ItemType.Version, PredefinedClassificationTypeNames.Type },
            { ItemType.HeaderName, PredefinedClassificationTypeNames.Literal },
            { ItemType.HeaderValue, PredefinedClassificationTypeNames.Literal },
            { ItemType.Comment, PredefinedClassificationTypeNames.Comment },
            { ItemType.Body, PredefinedClassificationTypeNames.Literal },
            { ItemType.Reference, PredefinedClassificationTypeNames.MarkupAttribute },
            { ItemType.OutputOperator, PredefinedClassificationTypeNames.Operator },
            { ItemType.RequestVariableName, PredefinedClassificationTypeNames.MarkupNode },
        };
    }

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IStructureTag))]
    [ContentType(LanguageFactory.LanguageName)]
    public class Outlining : TokenOutliningTaggerBase
    { }

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType(LanguageFactory.LanguageName)]
    public class ErrorSquigglies : TokenErrorTaggerBase
    { }

    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(LanguageFactory.LanguageName)]
    internal sealed class Tooltips : TokenQuickInfoBase
    { }

    [Export(typeof(IBraceCompletionContextProvider))]
    [BracePair('(', ')')]
    [BracePair('[', ']')]
    [BracePair('{', '}')]
    [BracePair('"', '"')]
    [BracePair('$', '$')]
    [ContentType(LanguageFactory.LanguageName)]
    [ProvideBraceCompletion(LanguageFactory.LanguageName)]
    internal sealed class BraceCompletion : BraceCompletionBase
    { }

    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [ContentType(LanguageFactory.LanguageName)]
    internal sealed class CompletionCommitManager : CompletionCommitManagerBase
    {
        public override IEnumerable<char> CommitChars => new char[] { ' ', '\'', '"', ',', '.', ';', ':', '\\', '$' };
    }

    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(TextMarkerTag))]
    [ContentType(LanguageFactory.LanguageName)]
    internal sealed class BraceMatchingTaggerProvider : BraceMatchingBase
    {
        // This will match parenthesis, curly brackets, and square brackets by default.
        // Override the BraceList property to modify the list of braces to match.
    }

    [Export(typeof(IViewTaggerProvider))]
    [ContentType(LanguageFactory.LanguageName)]
    [TagType(typeof(TextMarkerTag))]
    public class SameWordHighlighter : SameWordHighlighterBase
    { }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(LanguageFactory.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class UserRating : WpfTextViewCreationListener
    {
        private readonly RatingPrompt _rating = new(Constants.MarketplaceId, Vsix.Name, General.Instance);
        private readonly DateTime _openedDate = DateTime.Now;

        protected override void Closed(IWpfTextView textView)
        {
            if (_openedDate.AddMinutes(2) < DateTime.Now)
            {
                // Only register use after the document was open for more than 2 minutes.
                _rating.RegisterSuccessfulUsage();

            }
        }
    }
}
