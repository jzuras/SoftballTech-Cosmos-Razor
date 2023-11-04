# SoftballTech-Cosmos (Razor)

This is a learning project for myself, and it is not expected to be useful to the world at large. However, my LI post discusses some speed bumps that I encountered that might be interesting for others just learning about Cosmos (LI post coming soon). 

After creating a mini-version of my old SoftballTech website using Razor Pages with SQL Server as the database, I wanted to learn about Cosmos DB, and NoSQL databases in general. This repo is a port of the SQL Server version to Cosmos DB. (PLease see the ReadMe in the original repo for more info about the website itself.)

Most of the changes in this version are due of course to the data model now consisting of JSON documents instead of relational tables. I also decided to create a Data Access Layer, moving the database code from the Razor Pages to a centralized service.

This version is also running on Azure, and can be found [here](https://sbt-cosmos.azurewebsites.net/).

I will update this with a link to the LinkedIn discussion post when it is ready.
