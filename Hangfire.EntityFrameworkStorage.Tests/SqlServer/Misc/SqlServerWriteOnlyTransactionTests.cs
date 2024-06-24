﻿using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Misc;

[Collection(Constants.SqlServerFixtureCollectionName)]
public class
    SqlServerWriteOnlyTransactionTests : WriteOnlyTransactionTestsBase
{
    public SqlServerWriteOnlyTransactionTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
    {
    }
}