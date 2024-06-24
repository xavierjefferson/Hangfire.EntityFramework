using System;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue
{
    public abstract class FetchedJobTestsBase : TestBase
    {
        protected FetchedJobTestsBase(DatabaseFixtureBase fixture) : base(fixture)
        {
            _fetchedJob = new FetchedJob {Id = _id, JobId = JobId, Queue = Queue};
        }

        private const int JobId = 1;
        private const string Queue = "queue";
        private readonly FetchedJob _fetchedJob;
        private readonly int _id = 0;


        [Fact]
        public void Ctor_CorrectlySets_AllInstanceProperties()
        {
            var fetchedJob = new EntityFrameworkFetchedJob(GetStorage(), _fetchedJob);

            Assert.Equal(JobId.ToString(), fetchedJob.JobId);
            Assert.Equal(Queue, fetchedJob.Queue);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenConnectionIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new EntityFrameworkFetchedJob(null, _fetchedJob));

            Assert.Equal("storage", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenStorageIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new EntityFrameworkFetchedJob(null, _fetchedJob));

            Assert.Equal("storage", exception.ParamName);
        }
    }
}