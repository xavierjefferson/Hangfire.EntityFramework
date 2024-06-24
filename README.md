
﻿# Hangfire EntityFramework Storage - A generic implementation for Entity Framework Core Providers
[![Latest version](https://img.shields.io/nuget/v/Hangfire.EntityFrameworkStorage.svg)](https://www.nuget.org/packages/Hangfire.EntityFrameworkStorage/) 

EntityFrameworkCore storage implementation of [Hangfire](http://hangfire.io/) - fire-and-forget, delayed and recurring tasks runner for .NET. Scalable and reliable background job runner. Supports multiple servers, CPU and I/O intensive, long-running and short-running jobs.

Forked from [Hangfire.FluentNHibernateStores](https://github.com/xavierjefferson/Hangfire.FluentNHibernateStorage), this is an EntityFrameworkCore-backed implementation of a Hangfire storage provider that supports the several providers for Entity Framework Core 6.0 and up.  When deployed in a Hangfire instance, this library will automatically generate tables required for storing Hangfire metadata, and pass the correct SQL flavor to the database transparently.  The intention of doing an implementation like this one is to be able to share tentative improvements with a broad audience of developers.

## Installation


Run the following command in the NuGet Package Manager console to install Hangfire.EntityFrameworkStorage:

```
Install-Package Hangfire.EntityFrameworkStorage
```

You will need to install an [additional driver package](DriverPackage.md) for all RDBMS systems.

## Usage - Within an ASP.NET Application
I may simplify the implementation later, but I think this code is pretty painless.  ~~Please note the properties, which include specifying a database schema, passed to the method:~~
```
        public void Configuration(IAppBuilder app)
        {
            //Configure properties (this is optional)
            //2024-06-24 Options are not implemented yet.
            var options = new EntityFrameworkStorageOptions
            {
                TransactionIsolationLevel = IsolationLevel.Serializable,
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                UpdateSchema = true,
                DashboardJobListLimit = 50000,
                InvisibilityTimeout = TimeSpan.FromMinutes(15),
                TransactionTimeout = TimeSpan.FromMinutes(1),
                DefaultSchema = null, // use database provider's default schema
                TablePrefix = "Hangfire_"
            };           

            //THIS SECTION GETS THE STORAGE PROVIDER.  CHANGE THE ENUM VALUE ON THE NEXT LINE FOR
            //YOUR PARTICULAR RDBMS

            var connectionString = //your provider-specific connection string
            
            GlobalConfiguration.Configuration.UseEntityFrameworkJobStorage(i =>
            {
                //THIS SECTION SETS THE STORAGE PROVIDER.  CHANGE THE 
                //NEXT LINE FOR YOUR PARTICULAR RDBMS
                
                i.UseSqlite(connectionString);
            });

            /*** More Hangfire configuration stuff 
            would go in this same method ***/
         }
```
## Usage - A Standalone Server
```
using System;
using System.Configuration;
using System.Transactions;
using Hangfire.EntityFrameworkStorage;

namespace Hangfire.EntityFrameworkStorage.SampleApplication
{
    public class DemoClass
    {
        private static BackgroundJobServer _backgroundJobServer;

        private static void Main(string[] args)
        {
            //Configure properties (this is optional)
            //2024-06-24 Options are not implemented yet.
            var options = new EntityFrameworkStorageOptions
            {
                TransactionIsolationLevel = IsolationLevel.Serializable,
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                UpdateSchema = true,
                DashboardJobListLimit = 50000,
                InvisibilityTimeout = TimeSpan.FromMinutes(15),
                TransactionTimeout = TimeSpan.FromMinutes(1),
                DefaultSchema = null, // use database provider's default schema
                TablePrefix = "Hangfire_"
            };



         
            var connectionString = //your provider-specific connection string

            //THIS LINE CONFIGURES HANGFIRE WITH THE STORAGE PROVIDER
            GlobalConfiguration.Configuration.UseEntityFrameworkJobStorage(i =>
            {
                //THIS SECTION SETS THE STORAGE PROVIDER.  CHANGE THE 
                //NEXT LINE FOR YOUR PARTICULAR RDBMS
                
                i.UseSqlite(connectionString);
            });

            /*THIS LINE STARTS THE BACKGROUND SERVER*/
            _backgroundJobServer = new BackgroundJobServer(new BackgroundJobServerOptions(), storage,
                storage.GetBackgroundProcesses());

            /*AND... DONE.*/

            Console.WriteLine("Background job server is running.  Press [ENTER] to quit.");
            Console.ReadLine();
        }
    }
}
```
Description of optional parameters:
( As of 2024-06-24, these options are not implemented yet, but are planned in future)
- `TransactionIsolationLevel` - transaction isolation level. Default is Serializable.
- `QueuePollInterval` - job queue polling interval. Default is 15 seconds.
- `JobExpirationCheckInterval` - job expiration check interval (manages expired records). Default is 1 hour.
- `CountersAggregateInterval` - interval to aggregate counter. Default is 5 minutes.
- `UpdateSchema` - if set to `true`, it creates database tables. Default is `true`.
- `DashboardJobListLimit` - dashboard job list limit. Default is 50000.
- `TransactionTimeout` - transaction timeout. Default is 1 minute.
- `InvisibilityTimeout` - If your jobs run long, Hangfire will assume they've failed if the duration is longer than this value.  Increase this value so Hangfire try to re-queue them too early. Default is 15 minutes.
- `DefaultSchema` - database schema into which the Hangfire tables will be created.  Default is database provider specific ("dbo" for SQL Server, "public" for PostgreSQL, etc).
- `TablePrefix` - Table name prefix for Hangfire tables that will be created.  Default is 'Hangfire_'.  This cannot be null or blank.

## Build
Please use Visual Studio or any other tool of your choice to build the solution

# Database Stuff

 - **IMPORTANT**:  The Hangfire engine, with its 20 default worker threads, is not database-intensive but it can be VERY chatty.
 - During first-time use, you'll need table-creation rights on your RDBMS.
 - You can't specify table names (yet).  But you can specify the schema.  See the sample code.
 - Since this uses an OR/M, there are no stored procedures or views to be installed.
 - All dates stored in the tables are Unix epoch dates.
 - This implementation uses database transactions to cleanly distribute pending jobs between various workers threads.  You may witness, if you turn on a logger, lots of failed transactions.  For now, this is expected.  Don't panic.
 - Old records are automatically cleaned up.  Most of the cleanup parameters are specified by the Hangfire engine itself, and this implementation does its best to not lock up your RDBMS while old records are being purged.  For more information on specific cleanup parameters, consult the Hangfire forums.
 - Table Hangfire_Dual should contain one and only one row - this is by design, and it's required in order to support the various RDBMSs.

## Tables Created:
```
Hangfire_AggregatedCounter
Hangfire_Counter
Hangfire_DistributedLock
Hangfire_Dual
Hangfire_Hash
Hangfire_Job
Hangfire_JobParameter
Hangfire_JobQueue
Hangfire_JobState
Hangfire_List
Hangfire_Server
Hangfire_Set
```
