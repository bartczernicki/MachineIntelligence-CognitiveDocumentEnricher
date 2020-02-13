# Machine Intelligence - Cognitive Document Enricher
Console Application that enriches source documents with Azure Cognitive Services, Microsoft AI &amp; ML

Note: For formal use cases using Search, please use Cognitive Search:
https://docs.microsoft.com/en-us/azure/search/cognitive-search-concept-intro

# Use Cases
1) Desire to test out combination of Cognitive Services, Machine Learning, Artificial Intelligence in a single application
2) Need explicit control of the file format libraries, Cognitive Services API versions (i.e. need additional metadata, support for very large/protected documents, additional niche document formats)
3) Need orchestration to run offline from Azure cloud
4) Need to use private Cognitive Services Container endpoints for API processing
5) Need a complex & custom workflow (i.e. mixing existing C# business logic with AI)
6) Can configure additional persistance outputs (Local Storage, Azure Blob Storage, Azure Table, CosmosDB storage etc.)

# Requirements
Visual Studio 2019 (built on 16.4), .NET 4.5.2+, Windows 10 or Windows Server 2012+, Azure Subcription (Free Trial, MSDN or Enterprise)

## Getting Started

Inside the Azure portal provision the following services:
1) [Required] Cognitive Services (shared key across most services)
2) [Optional] Bing Entity Search
3) [Optional] Storage Account (General Purpose v2)
4) [Optional] Azure Cosmos DB Account (Core (SQL) API)

Note: Only the Cognitive Services are required to run the MachineIntelligence-CognitiveDocumentEnricher.
- Bing Entity Search provides additional information from the Bing Knowledge Graph
- Storage Account optionally writes the output of the Artificial Intelligence Insights to Azure Blob Storage
- CosmosDB provides a NoSQL database to write the Artificial Intelligence extracted metadata for later easy querying


Screenshot of Azure Portal of required resources
![Azure Portal Resources](https://github.com/bartczernicki/MachineIntelligence-CognitiveDocumentEnricher/blob/master/Images/AzurePortal-ResourcesforEnrichment.png)

## Set up:
1) Create a blob storage container inside created storage account
2) Storage Account - Add a SAS key with: Create, Read, Write properties (SAS key is used in App.config)
3) Azure Cosmos DB Account - Create a new Container (Container information is used in App.config)
4) Create a new folder and add some example PDF, Word documents to the folder
5) Add the appropriate Cognitive Services keys to the App.Config (example shown below)

## Configuration

In the App.Config file there are several values that can be set to either True or False.
USE_AZURE_BLOB_STORAGE - Setting this to True will write out the split images, JSON OCR as well as the Cognitive Services AI content to an Azure Blob Storage account.
USE_AZURE_TABLE_STORAGE - Setting this to True will write out the high level information of the processed files to Azure Table Storage.  This is a great way to see a quick snapshot of the metadata (i.e. number of API calls, size of pages, number of PII information found) for each processed document
USE_COGNITIVE_SERVICES_V2 - Use the v2.x version of the Cognitive Services APIs.  You must set either V2 or V3 to True.  Both can be set to True.
USE_COGNITIVE_SERVICES_V3 - Use the v3.x version of the Cognitive Services APIs.  You must set either V2 or V3 to True.  Both can be set to True.
USE_COGNITIVE_SERVICES_BING_ENTITY_SEARCH - Set this to True to run the extracted entitites through the Bing Knowledge Graph gaining additional taxonomy information (note: this is largerly provided in the V3 APIs)
USE_COSMOSDB_STORAGE - Setting this value to true will persist the extracted JSON file for each document in a NoSQL CosmosDB database that is easy to query.

Screenshot of App.Config file
![Sample App.Config](https://github.com/bartczernicki/MachineIntelligence-CognitiveDocumentEnricher/blob/master/Images/SampleAppConfig.png)
