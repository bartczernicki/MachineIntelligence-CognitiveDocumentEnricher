using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher
{
    public class ApiKeyServiceClientCredentialsEntitySearch : Microsoft.Rest.ServiceClientCredentials
    {
        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", Config.COGNITIVE_SERVICES_BING_ENTITY_SEARCH_KEY);
            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
