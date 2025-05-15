# SoftballTech-Cosmos (Razor)

This is a learning project for myself, and it is not expected to be useful to the world at large. However, my LI post discusses some speed bumps that I encountered that might be interesting for others just learning about Cosmos. 

After creating a mini-version of my old SoftballTech website using Razor Pages with SQL Server as the database, I wanted to learn about Cosmos DB, and NoSQL databases in general. This repo is a port of the SQL Server version to Cosmos DB. (Please see the ReadMe in the original repo for more info about the website itself.)

Most of the changes in this version are due to the data model now consisting of JSON documents instead of relational tables. I also decided to create a Data Access Layer, moving the database code from the Razor Pages to a centralized service.

This version is also running on Azure; contact me via LinkedIn for more info.

The LinkedIn discussion post is [here](https://www.linkedin.com/feed/update/urn:li:activity:7127341142521577474/).
