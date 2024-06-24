﻿using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Hangfire.EntityFrameworkStorage.Monitoring
{
    public class EntityFrameworkMonitoringApi : IMonitoringApi
    {
        private readonly EntityFrameworkJobStorage _storage;

        public EntityFrameworkMonitoringApi([NotNull] EntityFrameworkJobStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IList<QueueWithTopEnqueuedJobsDto> Queues()
        {
            var tuples = _storage.QueueProviders
                .Select(x => x.GetJobQueueMonitoringApi())
                .SelectMany(x => x.GetQueues(), (monitoring, queue) => new {Monitoring = monitoring, Queue = queue})
                .OrderBy(x => x.Queue)
                .ToArray();

            var result = new List<QueueWithTopEnqueuedJobsDto>(tuples.Length);

            foreach (var tuple in tuples)
            {
                var enqueuedJobIds = tuple.Monitoring.GetEnqueuedJobIds(tuple.Queue, 0, 5);
                var counters = tuple.Monitoring.GetEnqueuedAndFetchedCount(tuple.Queue);

                var firstJobs =
                    _storage.UseDbContextInTransaction(dbContext => EnqueuedJobs(dbContext, enqueuedJobIds));

                result.Add(new QueueWithTopEnqueuedJobsDto
                {
                    Name = tuple.Queue,
                    Length = counters.EnqueuedCount ?? 0,
                    Fetched = counters.FetchedCount,
                    FirstJobs = firstJobs
                });
            }

            return result;
        }

        public IList<ServerDto> Servers()
        {
            Func<HangfireContext, IList<ServerDto>> action = dbContext =>
            {
                var result = new List<ServerDto>();

                foreach (var server in dbContext.Servers)
                {
                    var data = SerializationHelper.Deserialize<ServerData>(server.Data);
                    result.Add(new ServerDto
                    {
                        Name = server.Id,
                        Heartbeat = server.LastHeartbeat,
                        Queues = data.Queues,
                        StartedAt = data.StartedAt.HasValue ? data.StartedAt.Value : DateTime.MinValue,
                        WorkersCount = data.WorkerCount
                    });
                }

                return result;
            };
            return _storage.UseDbContextInTransaction(action);
        }

        public JobDetailsDto JobDetails(string jobId)
        {
            var converter = StringToInt32Converter.Convert(jobId);
            if (!converter.Valid) return null;

            //wrap in transaction so we only read serialized data
            using (_storage.CreateTransaction())

                //this will not use a stateless dbContext because it has to use the references between job and parameters.
            using (var dbContext = _storage.SessionFactoryInfo.SessionFactory.OpenSession())
            {
                var job = dbContext.Jobs.SingleOrDefault(i => i.Id == converter.Value);
                if (job == null) return null;

                var parameters = job.Parameters.ToDictionary(x => x.Name, x => x.Value);
                var history =
                    job.History.OrderByDescending(i => i.Id)
                        .Select(jobState => new StateHistoryDto
                        {
                            StateName = jobState.Name,
                            CreatedAt = jobState.CreatedAt,
                            Reason = jobState.Reason,
                            Data = new Dictionary<string, string>(
                                SerializationHelper.Deserialize<Dictionary<string, string>>(jobState.Data),
                                StringComparer.OrdinalIgnoreCase)
                        })
                        .ToList();

                return new JobDetailsDto
                {
                    CreatedAt = job.CreatedAt,
                    ExpireAt = job.ExpireAt,
                    Job = DeserializeJob(job.InvocationData, job.Arguments),
                    History = history,
                    Properties = parameters
                };
            }
        }

        public StatisticsDto GetStatistics()
        {
            var statistics =
                _storage.UseDbContextInTransaction(dbContext =>
                {
                    var statesDictionary = dbContext.Jobs
                        .Where(i => i.StateName != null && i.StateName.Length > 0)
                        .GroupBy(i => i.StateName)
                        .Select(i => new {i.Key, Count = i.Count()})
                        .ToDictionary(i => i.Key, j => j.Count);

                    int GetJobStatusCount(string key)
                    {
                        if (statesDictionary.ContainsKey(key)) return statesDictionary[key];

                        return 0;
                    }

                    long CountStats(string key)
                    {
                        var l1 = dbContext.AggregatedCounters.Where(i => i.Key == key).Select(i => i.Value)
                            .ToList();
                        var l2 = dbContext.Counters.Where(i => i.Key == key).Select(i => i.Value).ToList();
                        return l1.Sum() + l2.Sum();
                    }

                    return new StatisticsDto
                    {
                        Enqueued = GetJobStatusCount("Enqueued"),
                        Failed = GetJobStatusCount("Failed"),
                        Processing = GetJobStatusCount("Processing"),
                        Scheduled = GetJobStatusCount("Scheduled"),
                        Servers = dbContext.Servers.Count(),
                        Succeeded = CountStats("stats:succeeded"),
                        Deleted = CountStats("stats:deleted"),
                        Recurring = dbContext.Sets.Count(i => i.Key == "recurring-jobs")
                    };
                });

            statistics.Queues = _storage.QueueProviders
                .SelectMany(x => x.GetJobQueueMonitoringApi().GetQueues())
                .Count();

            return statistics;
        }

        public JobList<EnqueuedJobDto> EnqueuedJobs(string queue, int from, int perPage)
        {
            var queueApi = GetQueueApi(queue);
            var enqueuedJobIds = queueApi.GetEnqueuedJobIds(queue, from, perPage);

            return _storage.UseDbContextInTransaction(dbContext => EnqueuedJobs(dbContext, enqueuedJobIds));
        }

        public JobList<FetchedJobDto> FetchedJobs(string queue, int from, int perPage)
        {
            var queueApi = GetQueueApi(queue);
            var fetchedJobIds = queueApi.GetFetchedJobIds(queue, from, perPage);

            return _storage.UseDbContextInTransaction(dbContext => FetchedJobs(dbContext, fetchedJobIds));
        }

        public JobList<ProcessingJobDto> ProcessingJobs(int from, int count)
        {
            return _storage.UseDbContextInTransaction(dbContext => GetJobs(
                dbContext,
                from, count,
                ProcessingState.StateName,
                (sqlJob, job, stateData) => new ProcessingJobDto
                {
                    Job = job,
                    ServerId = stateData.ContainsKey("ServerId") ? stateData["ServerId"] : stateData["ServerName"],
                    StartedAt = JobHelper.DeserializeDateTime(stateData["StartedAt"])
                }));
        }

        public JobList<ScheduledJobDto> ScheduledJobs(int from, int count)
        {
            return _storage.UseDbContextInTransaction(dbContext => GetJobs(
                dbContext,
                from, count,
                ScheduledState.StateName,
                (sqlJob, job, stateData) => new ScheduledJobDto
                {
                    Job = job,
                    EnqueueAt = JobHelper.DeserializeDateTime(stateData["EnqueueAt"]),
                    ScheduledAt = JobHelper.DeserializeDateTime(stateData["ScheduledAt"])
                }));
        }

        public JobList<SucceededJobDto> SucceededJobs(int from, int count)
        {
            return _storage.UseDbContextInTransaction(dbContext => GetJobs(
                dbContext,
                from,
                count,
                SucceededState.StateName,
                (sqlJob, job, stateData) => new SucceededJobDto
                {
                    Job = job,
                    Result = stateData.ContainsKey("Result") ? stateData["Result"] : null,
                    TotalDuration = stateData.ContainsKey("PerformanceDuration") && stateData.ContainsKey("Latency")
                        ? (long?) long.Parse(stateData["PerformanceDuration"]) +
                          (long?) long.Parse(stateData["Latency"])
                        : null,
                    SucceededAt = JobHelper.DeserializeNullableDateTime(stateData["SucceededAt"])
                }));
        }

        public JobList<FailedJobDto> FailedJobs(int from, int count)
        {
            return _storage.UseDbContextInTransaction(dbContext => GetJobs(
                dbContext,
                from,
                count,
                FailedState.StateName,
                (sqlJob, job, stateData) => new FailedJobDto
                {
                    Job = job,
                    Reason = sqlJob.StateReason,
                    ExceptionDetails = stateData["ExceptionDetails"],
                    ExceptionMessage = stateData["ExceptionMessage"],
                    ExceptionType = stateData["ExceptionType"],
                    FailedAt = JobHelper.DeserializeNullableDateTime(stateData["FailedAt"])
                }));
        }

        public JobList<DeletedJobDto> DeletedJobs(int from, int count)
        {
            return _storage.UseDbContextInTransaction(dbContext => GetJobs(
                dbContext,
                from,
                count,
                DeletedState.StateName,
                (sqlJob, job, stateData) => new DeletedJobDto
                {
                    Job = job,
                    DeletedAt = JobHelper.DeserializeNullableDateTime(stateData["DeletedAt"])
                }));
        }

        public long ScheduledCount()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetNumberOfJobsByStateName(dbContext, ScheduledState.StateName));
        }

        public long EnqueuedCount(string queue)
        {
            var queueApi = GetQueueApi(queue);
            var counters = queueApi.GetEnqueuedAndFetchedCount(queue);

            return counters.EnqueuedCount ?? 0;
        }

        public long FetchedCount(string queue)
        {
            var queueApi = GetQueueApi(queue);
            var counters = queueApi.GetEnqueuedAndFetchedCount(queue);

            return counters.FetchedCount ?? 0;
        }

        public long FailedCount()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetNumberOfJobsByStateName(dbContext, FailedState.StateName));
        }

        public long ProcessingCount()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetNumberOfJobsByStateName(dbContext, ProcessingState.StateName));
        }

        public long SucceededListCount()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetNumberOfJobsByStateName(dbContext, SucceededState.StateName));
        }

        public long DeletedListCount()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetNumberOfJobsByStateName(dbContext, DeletedState.StateName));
        }

        public IDictionary<DateTime, long> SucceededByDatesCount()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetTimelineStats(dbContext, "succeeded"));
        }

        public IDictionary<DateTime, long> FailedByDatesCount()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetTimelineStats(dbContext, "failed"));
        }

        public IDictionary<DateTime, long> HourlySucceededJobs()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetHourlyTimelineStats(dbContext, "succeeded"));
        }

        public IDictionary<DateTime, long> HourlyFailedJobs()
        {
            return _storage.UseDbContextInTransaction(dbContext =>
                GetHourlyTimelineStats(dbContext, "failed"));
        }

        private long GetNumberOfJobsByStateName(HangfireContext dbContext, string stateName)
        {
            var count = dbContext.Jobs.Count(i => i.StateName == stateName);
            var jobListLimit = _storage.Options.DashboardJobListLimit;
            if (jobListLimit.HasValue) return Math.Min(count, jobListLimit.Value);

            return count;
        }

        private IPersistentJobQueueMonitoringApi GetQueueApi(string queueName)
        {
            var provider = _storage.QueueProviders.GetProvider(queueName);
            var monitoringApi = provider.GetJobQueueMonitoringApi();

            return monitoringApi;
        }

        private JobList<TDto> GetJobs<TDto>(
            HangfireContext dbContext,
            int from,
            int count,
            string stateName,
            Func<_Job, Job, Dictionary<string, string>, TDto> selector)
        {
            var jobs = dbContext.Jobs
                .OrderByDescending(i => i.Id)
                .Where(i => i.StateName == stateName)
                .Skip(from)
                .Take(count)
                .ToList();

            return DeserializeJobs(jobs, selector);
        }

        private static JobList<TDto> DeserializeJobs<TDto>(
            ICollection<_Job> jobs,
            Func<_Job, Job, Dictionary<string, string>, TDto> selector)
        {
            var result = new List<KeyValuePair<string, TDto>>(jobs.Count);

            foreach (var job in jobs)
            {
                var deserializedData = SerializationHelper.Deserialize<Dictionary<string, string>>(job.StateData);
                var stateData = deserializedData != null
                    ? new Dictionary<string, string>(deserializedData, StringComparer.OrdinalIgnoreCase)
                    : null;

                var dto = selector(job, DeserializeJob(job.InvocationData, job.Arguments), stateData);

                result.Add(new KeyValuePair<string, TDto>(
                    job.Id.ToString(), dto));
            }

            return new JobList<TDto>(result);
        }

        private static Job DeserializeJob(string invocationData, string arguments)
        {
            var data = SerializationHelper.Deserialize<InvocationData>(invocationData);
            data.Arguments = arguments;

            try
            {
                return data.DeserializeJob();
            }
            catch (JobLoadException)
            {
                return null;
            }
        }

        private Dictionary<DateTime, long> GetTimelineStats(
            HangfireContext dbContext,
            string type)
        {
            var endDate = dbContext.Storage.UtcNow.Date;
            var dates = new List<DateTime>();
            for (var i = 0; i < 7; i++)
            {
                dates.Add(endDate);
                endDate = endDate.AddDays(-1);
            }

            var keyMaps = dates.ToDictionary(x => string.Format("stats:{0}:{1:yyyy-MM-dd}", type, x),
                x => x);

            return GetTimelineStats(dbContext, keyMaps);
        }

        private Dictionary<DateTime, long> GetTimelineStats(HangfireContext dbContext,
            IDictionary<string, DateTime> keyMaps)
        {
            var valuesMap = dbContext.AggregatedCounters
                .Where(i => keyMaps.Keys.Contains(i.Key))
                .ToDictionary(x => x.Key, x => x.Value);

            foreach (var key in keyMaps.Keys)
                if (!valuesMap.ContainsKey(key))
                    valuesMap.Add(key, 0);

            var result = new Dictionary<DateTime, long>();
            for (var i = 0; i < keyMaps.Count; i++)
            {
                var value = valuesMap[keyMaps.ElementAt(i).Key];
                result.Add(keyMaps.ElementAt(i).Value, value);
            }

            return result;
        }

        private JobList<EnqueuedJobDto> EnqueuedJobs(
            HangfireContext dbContext,
            IEnumerable<long> jobIds)
        {
            var list = jobIds.ToList();
            if (list.Any())
            {
                var jobs = dbContext.Jobs.Where(i => list.Contains(i.Id)).ToList();

                return DeserializeJobs(
                    jobs,
                    (sqlJob, job, stateData) => new EnqueuedJobDto
                    {
                        Job = job,
                        State = sqlJob.StateName,
                        EnqueuedAt = sqlJob.StateName == EnqueuedState.StateName
                            ? JobHelper.DeserializeNullableDateTime(stateData["EnqueuedAt"])
                            : null
                    });
            }

            return new JobList<EnqueuedJobDto>(new List<KeyValuePair<string, EnqueuedJobDto>>());
        }

        private JobList<FetchedJobDto> FetchedJobs(
            HangfireContext dbContext,
            IEnumerable<long> jobIds)
        {
            var list = jobIds.ToList();
            if (list.Any())
            {
                var result = new List<KeyValuePair<string, FetchedJobDto>>();

                foreach (var job in dbContext.Jobs.Where(i => list.Contains(i.Id)))
                    result.Add(new KeyValuePair<string, FetchedJobDto>(
                        job.Id.ToString(),
                        new FetchedJobDto
                        {
                            Job = DeserializeJob(job.InvocationData, job.Arguments),
                            State = job.StateName
                        }));

                return new JobList<FetchedJobDto>(result);
            }

            return new JobList<FetchedJobDto>(new List<KeyValuePair<string, FetchedJobDto>>());
        }

        private Dictionary<DateTime, long> GetHourlyTimelineStats(
            HangfireContext dbContext,
            string type)
        {
            var endDate = dbContext.Storage.UtcNow;
            var dates = new List<DateTime>();
            for (var i = 0; i < 24; i++)
            {
                dates.Add(endDate);
                endDate = endDate.AddHours(-1);
            }

            var keyMaps = dates.ToDictionary(x => string.Format("stats:{0}:{1:yyyy-MM-dd-HH}", type, x),
                x => x);

            return GetTimelineStats(dbContext, keyMaps);
        }
    }
}