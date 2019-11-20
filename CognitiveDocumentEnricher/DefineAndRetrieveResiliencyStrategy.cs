using Polly;
using Polly.Wrap;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace CognitiveDocumentEnricher
{
    public class CognitiveServicesRetryPolicy
    {
        public static PolicyWrap<HttpResponseMessage> DefineAndRetrieveResiliencyStrategy()
        {
            // Retry when these status codes are encountered.
            HttpStatusCode []
            httpStatusCodesWorthRetrying = {
                HttpStatusCode.BadRequest, // 400
                HttpStatusCode.InternalServerError, // 500
                HttpStatusCode.BadGateway, // 502
                HttpStatusCode.ServiceUnavailable, // 503
                HttpStatusCode.GatewayTimeout // 504
            };

            // Define our waitAndRetry policy: retry n times with an exponential backoff in case the Computer Vision API throttles us for too many requests.
            var waitAndRetryPolicy = Policy
                .HandleResult<HttpResponseMessage>(e => 
                    (e.StatusCode == HttpStatusCode.ServiceUnavailable) ||
                    (e.StatusCode == (System.Net.HttpStatusCode) 429) ||
                    (e.Content.ReadAsStringAsync().Result == "{\"status\":\"Running\"}")
                    )
                .WaitAndRetryAsync(10, // Retry 12 times with a delay between retries before ultimately giving up
                    attempt => TimeSpan.FromSeconds(0.5 * Math.Pow(2, attempt))
                );

            var circuitBreakerPolicyForRecoverable = Policy
                .HandleResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(3)
                );

            return Policy.WrapAsync(waitAndRetryPolicy, circuitBreakerPolicyForRecoverable);
    }

        public static PolicyWrap<HttpResponseMessage> DefineAndRetrieveResiliencyStrategyForBatchJob()
        {
            // Retry when these status codes are encountered.
            HttpStatusCode[]
            httpStatusCodesWorthRetrying = {
                HttpStatusCode.BadRequest, // 400
                HttpStatusCode.InternalServerError, // 500
                HttpStatusCode.BadGateway, // 502
                HttpStatusCode.ServiceUnavailable, // 503
                HttpStatusCode.GatewayTimeout // 504
            };

            // Define our waitAndRetry policy: retry n times with an exponential backoff in case the Computer Vision API throttles us for too many requests.
            var waitAndRetryPolicy = Policy
                .HandleResult<HttpResponseMessage>(e =>
                    (e.StatusCode == HttpStatusCode.ServiceUnavailable) ||
                    (e.Content.ReadAsStringAsync().Result == "{\"status\":\"Running\"}") ||
                    (e.Content.ReadAsStringAsync().Result == "{\"status\":\"NotStarted\"}") ||
                    (e.Content.ReadAsStringAsync().Result == "{\"status\":\"Failed\"}")
                    )
                .WaitAndRetryAsync(20, // Retry 20 times with a delay between retries before ultimately giving up
                    attempt => TimeSpan.FromSeconds(1 * Math.Pow(2, attempt))
                );

            var circuitBreakerPolicyForRecoverable = Policy
                .HandleResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(3)
                );

            return Policy.WrapAsync(waitAndRetryPolicy, circuitBreakerPolicyForRecoverable);
        }
    }
}