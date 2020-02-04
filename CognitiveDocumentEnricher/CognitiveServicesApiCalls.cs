using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher
{
    public class CognitiveServicesApiCalls
    {
        public int ApiCallCount { get; set; }
        public int ApiCallV2Count { get; set; }
        public int ApiCallV3Count { get; set; }

        public int TotalCount
        {
            get
            {
                var totalCount = ApiCallCount + ApiCallV2Count + ApiCallV3Count;
                return totalCount;
            }
        }
    }
}
