using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveDocumentEnricher
{
    public class BingEntityData
    {
        public string EntityName { get; set; }

        public string Taxony { get; set; }

        public string Description { get; set; }

        public override string ToString()
        {
            return EntityName + " | Taxonomy: " + Taxony + " | Description: " + Description;
        }
    }
}
