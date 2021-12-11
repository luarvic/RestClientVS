using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using RestClient;
using RestClientVS.Parsing;
using RestClientVS.SuggestedActions;

namespace RestClientVS
{
    internal class SuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly ITextView _view;
        private readonly string _file;
        private Request _request;

        public SuggestedActionsSource(ITextView view, string file)
        {
            _view = view;
            _file = file;
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            Document doc = _view.TextBuffer.CurrentSnapshot.ParseRestDocument(_file);
            _request = doc.Requests.LastOrDefault(r => r.IntersectsWith(range.Start.Position));
            return Task.FromResult(_request != null);
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            if (_request != null)
            {
                yield return new SuggestedActionSet(
                    categoryName: PredefinedSuggestedActionCategoryNames.Any,
                    actions: new[] { new SendRequestAction(_request) },
                    title: Vsix.Name,
                    priority: SuggestedActionSetPriority.Medium,
                    applicableToSpan: _request.Url.ToSimpleSpan());
            }
        }

        public void Dispose()
        {
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            // This is a sample provider and doesn't participate in LightBulb telemetry
            telemetryId = Guid.Empty;
            return false;
        }


        public event EventHandler<EventArgs> SuggestedActionsChanged
        {
            add { }
            remove { }
        }
    }
}
