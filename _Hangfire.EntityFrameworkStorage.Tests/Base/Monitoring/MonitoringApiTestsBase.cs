﻿using System;
using System.Collections.Generic;
using Hangfire.Common;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring
{
    public abstract class MonitoringApiTestsBase : TestBase
    {
        protected MonitoringApiTestsBase(DatabaseFixtureBase fixture) : base(fixture)
        {
            _storage = GetStorage();
            _api = new EntityFrameworkMonitoringApi(_storage);
            _createdAt = _storage.UtcNow;
            _expireAt = _storage.UtcNow.AddMinutes(1);
        }

        private EntityFrameworkJobStorage _storageMock;

        public override EntityFrameworkJobStorage GetStorage(EntityFrameworkStorageOptions options = null)
        {
            if (_storageMock == null)
            {
                var persistentJobQueueMonitoringApiMock = new Mock<IPersistentJobQueueMonitoringApi>();
                persistentJobQueueMonitoringApiMock.Setup(m => m.GetQueues()).Returns(new[] {"default"});

                var defaultProviderMock = new Mock<IPersistentJobQueueProvider>();
                defaultProviderMock.Setup(m => m.GetJobQueueMonitoringApi())
                    .Returns(persistentJobQueueMonitoringApiMock.Object);
                _storageMock = GetStorageMock(mock =>
                {
                    mock
                        .Setup(m => m.QueueProviders)
                        .Returns(new PersistentJobQueueProviderCollection(defaultProviderMock.Object));
                    return mock.Object;
                }, options);
            }

            return _storageMock;
        }

        private readonly string _arguments = "[\"test\"]";

        private readonly DateTime _createdAt;
        private readonly DateTime _expireAt;

        private static readonly string InvocationData = SerializationHelper.Serialize(
            new InvocationData(typeof(Console).AssemblyQualifiedName, nameof(Console.WriteLine),
                SerializationHelper.Serialize(new[] {typeof(string).AssemblyQualifiedName}),
                SerializationHelper.Serialize(new[] {"test"})));


        private readonly EntityFrameworkJobStorage _storage;
        private readonly EntityFrameworkMonitoringApi _api;

        [Fact]
        public void GetStatistics_ShouldReturnDeletedCount()
        {
            const int expectedStatsDeletedCount = 7;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                session.Insert(new _AggregatedCounter {Key = "stats:deleted", Value = 5});
                session.Insert(new _Counter {Key = "stats:deleted", Value = 1});
                session.Insert(new _Counter {Key = "stats:deleted", Value = 1});

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedStatsDeletedCount, result.Deleted);
        }

        [Fact]
        public void GetStatistics_ShouldReturnEnqueuedCount()
        {
            const int expectedEnqueuedCount = 1;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                session.Insert(new _Job
                {
                    InvocationData = string.Empty,
                    Arguments = string.Empty,
                    StateName = "Enqueued",
                    CreatedAt = session.Storage.UtcNow
                });

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedEnqueuedCount, result.Enqueued);
        }

        [Fact]
        public void GetStatistics_ShouldReturnFailedCount()
        {
            const int expectedFailedCount = 2;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                for (var i = 0; i < 2; i++)
                    session.Insert(new _Job
                    {
                        InvocationData = string.Empty,
                        Arguments = string.Empty,
                        CreatedAt = session.Storage.UtcNow,
                        StateName = "Failed"
                    });

                //does nothing

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedFailedCount, result.Failed);
        }

        [Fact]
        public void GetStatistics_ShouldReturnProcessingCount()
        {
            const int expectedProcessingCount = 1;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                session.Insert(new _Job
                {
                    InvocationData = string.Empty,
                    Arguments = string.Empty,
                    CreatedAt = _storage.UtcNow,
                    StateName = "Processing"
                });
                //does nothing

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedProcessingCount, result.Processing);
        }

        [Fact]
        public void GetStatistics_ShouldReturnQueuesCount()
        {
            const int expectedQueuesCount = 1;
            var _sut = new EntityFrameworkMonitoringApi(_storage);
            var result = _sut.GetStatistics();

            Assert.Equal(expectedQueuesCount, result.Queues);
        }

        [Fact]
        public void GetStatistics_ShouldReturnRecurringCount()
        {
            const int expectedRecurringCount = 1;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                session.Insert(new _Set {Key = "recurring-jobs", Value = "test", Score = 0});

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedRecurringCount, result.Recurring);
        }

        [Fact]
        public void GetStatistics_ShouldReturnScheduledCount()
        {
            const int expectedScheduledCount = 3;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                for (var i = 0; i < 3; i++)
                    session.Insert(new _Job
                    {
                        InvocationData = string.Empty,
                        CreatedAt = session.Storage.UtcNow,
                        Arguments = string.Empty,
                        StateName = "Scheduled"
                    });

                //does nothing
                session.Flush();

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedScheduledCount, result.Scheduled);
        }

        [Fact]
        public void GetStatistics_ShouldReturnServersCount()
        {
            const int expectedServersCount = 2;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                for (var i = 1; i < 3; i++) session.Insert(new _Server {Id = i.ToString(), Data = i.ToString()});

                //does nothing

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedServersCount, result.Servers);
        }

        [Fact]
        public void GetStatistics_ShouldReturnSucceededCount()
        {
            const int expectedStatsSucceededCount = 11;

            StatisticsDto result = null;
            UseJobStorageConnectionWithSession((session, connection) =>
            {
                session.Insert(new _Counter {Key = "stats:succeeded", Value = 1});
                session.Insert(new _AggregatedCounter {Key = "stats:succeeded", Value = 10});
                //does nothing

                result = _api.GetStatistics();
            });

            Assert.Equal(expectedStatsSucceededCount, result.Succeeded);
        }

        [Fact]
        public void JobDetails_ShouldReturnCreatedAtAndExpireAt()
        {
            JobDetailsDto result = null;

            UseJobStorageConnectionWithSession((session, connection) =>
            {
                var newJob = new _Job
                {
                    CreatedAt = _createdAt,
                    InvocationData = InvocationData,
                    Arguments = _arguments,
                    ExpireAt = _expireAt
                };
                session.Insert(newJob);
                //does nothing
                var jobId = newJob.Id;

                result = _api.JobDetails(jobId.ToString());
            });

            Assert.InRange(result.CreatedAt.Value.Subtract(_createdAt).TotalSeconds, -3, 60);
            Assert.InRange(result.ExpireAt.Value.Subtract(_expireAt).TotalSeconds, -3, 60);
        }

        [Fact]
        public void JobDetails_ShouldReturnHistory()
        {
            const string jobStateName = "Scheduled";
            const string stateData =
                "{\"EnqueueAt\":\"2016-02-21T11:56:05.0561988Z\", \"ScheduledAt\":\"2016-02-21T11:55:50.0561988Z\"}";

            JobDetailsDto result = null;

            UseJobStorageConnectionWithSession((session, connection) =>
            {
                var newJob = new _Job
                {
                    CreatedAt = _createdAt,
                    InvocationData = InvocationData,
                    Arguments = _arguments,
                    ExpireAt = _expireAt
                };
                session.Insert(newJob);
                session.Insert(new _JobState
                {
                    Job = newJob,
                    CreatedAt = _createdAt,
                    Name = jobStateName,
                    Data = stateData
                });
                //does nothing
                var jobId = newJob.Id;

                result = _api.JobDetails(jobId.ToString());
            });

            Assert.Equal(1, result.History.Count);
        }

        [Fact]
        public void JobDetails_ShouldReturnJob()
        {
            JobDetailsDto result = null;

            UseJobStorageConnectionWithSession((session, connection) =>
            {
                var newJob = new _Job
                {
                    CreatedAt = _createdAt,
                    InvocationData = InvocationData,
                    Arguments = _arguments,
                    ExpireAt = _expireAt
                };
                session.Insert(newJob);
                //does nothing
                var jobId = newJob.Id;


                result = _api.JobDetails(jobId.ToString());
            });

            Assert.NotNull(result.Job);
        }

        [Fact]
        public void JobDetails_ShouldReturnProperties()
        {
            var properties = new Dictionary<string, string>
            {
                ["CurrentUICulture"] = "en-US",
                ["CurrentCulture"] = "lt-LT"
            };

            JobDetailsDto result = null;

            UseJobStorageConnectionWithSession((session, connection) =>
            {
                var newJob = new _Job
                {
                    CreatedAt = _createdAt,
                    InvocationData = InvocationData,
                    Arguments = _arguments,
                    ExpireAt = _expireAt
                };
                session.Insert(newJob);

                foreach (var x in properties)
                    session.Insert(new _JobParameter {Job = newJob, Name = x.Key, Value = x.Value});

                //does nothing
                var jobId = newJob.Id;

                result = _api.JobDetails(jobId.ToString());
            });

            Assert.Equal(properties, result.Properties);
        }
    }
}