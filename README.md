# Machine Intelligence - Cognitive Document Enricher
Console Application that enriches source documents with Azure Cognitive Services, Microsoft AI &amp; ML

# Requirements
Visual Studio 2017+, .NET 4.5.2+, Windows 10/Windows Server, Azure Subcription (Trial, MSDN or Enterprise)

## Getting Started

Inside the Azure portal provision the following services:
1) Cognitive Services (shared key across most services)
2) Bing Entity Search
3) Storage Account (General Purpose v2)
4) Azure Cosmos DB Account (Core (SQL) API)

Set up:
1) Create a blob storage container inside created storage account
2) Storage Account - Add a SAS key with: Create, Read, Write properties (SAS key is used in App.config)
3) Azure Cosmos DB Account - Create a new Container (Container information is used in App.config)
4) Create a new folder and add some example PDF, Word documents to the folder
5) Add the appropriate Cognitive Services keys to the App.Config (example shown below)
