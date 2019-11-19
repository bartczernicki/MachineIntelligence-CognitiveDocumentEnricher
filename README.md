# Machine Intelligence - Cognitive Document Enricher
Console Application that enriches source documents with Azure Cognitive Services, Microsoft AI &amp; ML

Note: For formal use cases using Search, please use Cognitive Search:
https://docs.microsoft.com/en-us/azure/search/cognitive-search-concept-intro

# Use Cases
1) Want to test out combination of Cognitive Services, Machine Learning, Artificial Intelligence in a single application
2) Need explicit control of the file format libraries, Cognitive Services API versions (i.e. need additional metadata, support for very large/protected documents, additional niche document formats)
3) Need orchestration to run offline from Azure cloud
4) Need to use private Cognitive Services Container endpoints for API processing
5) Need a complex & custom workflow (i.e. mixing existing C# business logic with AI)
6) Can configure additional persistance outputs (Local, Table, CosmosDB storage etc.)

# Requirements
Visual Studio 2017+, .NET 4.5.2+, Windows 10/Windows Server, Azure Subcription (Trial, MSDN or Enterprise)

## Getting Started

Inside the Azure portal provision the following services:
1) Cognitive Services (shared key across most services)
2) Bing Entity Search
3) Storage Account (General Purpose v2)
4) Azure Cosmos DB Account (Core (SQL) API)

![Azure Portal Resources](https://github.com/bartczernicki/MachineIntelligence-CognitiveDocumentEnricher/blob/master/Images/AzurePortal-ResourcesforEnrichment.png)

Set up:
1) Create a blob storage container inside created storage account
2) Storage Account - Add a SAS key with: Create, Read, Write properties (SAS key is used in App.config)
3) Azure Cosmos DB Account - Create a new Container (Container information is used in App.config)
4) Create a new folder and add some example PDF, Word documents to the folder
5) Add the appropriate Cognitive Services keys to the App.Config (example shown below)

![Sample App.Config](https://github.com/bartczernicki/MachineIntelligence-CognitiveDocumentEnricher/blob/master/Images/SampleAppConfig.png)
