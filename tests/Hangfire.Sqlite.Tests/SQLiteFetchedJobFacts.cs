﻿using System;
using System.Data;
using System.Linq;
using Dapper;
using Hangfire.SQLite.Tests.Utils;
using Moq;
using Xunit;

namespace Hangfire.SQLite.Tests
{
    public class SQLiteFetchedJobFacts
    {
        private const string JobId = "id";
        private const string Queue = "queue";
        private readonly Mock<IDbConnection> _connection;

        public SQLiteFetchedJobFacts()
        {
            _connection = new Mock<IDbConnection>();
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenConnectionIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SQLiteFetchedJob(null, 1, JobId, Queue));

            Assert.Equal("connection", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenJobIdIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SQLiteFetchedJob(_connection.Object, 1, null, Queue));

            Assert.Equal("jobId", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenQueueIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SQLiteFetchedJob(_connection.Object, 1, JobId, null));

            Assert.Equal("queue", exception.ParamName);
        }

        [Fact]
        public void Ctor_CorrectlySets_AllInstanceProperties()
        {
            var fetchedJob = new SQLiteFetchedJob(_connection.Object, 1, JobId, Queue);

            Assert.Equal(1, fetchedJob.Id);
            Assert.Equal<string>(JobId, fetchedJob.JobId);
            Assert.Equal<string>(Queue, fetchedJob.Queue);
        }

        [Fact, CleanDatabase]
        public void RemoveFromQueue_ReallyDeletesTheJobFromTheQueue()
        {
            UseConnection(sql =>
            {
                // Arrange
                var id = CreateJobQueueRecord(sql, "1", "default");
                var processingJob = new SQLiteFetchedJob(sql, id, "1", "default");

                // Act
                processingJob.RemoveFromQueue();

                // Assert
                var count = sql.Query<int>("select count(*) from [HangFire.JobQueue]").Single();
                Assert.Equal(0, count);
            });
        }

        [Fact, CleanDatabase]
        public void RemoveFromQueue_DoesNotDelete_UnrelatedJobs()
        {
            UseConnection(sql =>
            {
                // Arrange
                CreateJobQueueRecord(sql, "1", "default");
                CreateJobQueueRecord(sql, "1", "critical");
                CreateJobQueueRecord(sql, "2", "default");

                var fetchedJob = new SQLiteFetchedJob(sql, 999, "1", "default");

                // Act
                fetchedJob.RemoveFromQueue();

                // Assert
                var count = sql.Query<int>("select count(*) from [HangFire.JobQueue]").Single();
                Assert.Equal(3, count);
            });
        }

        [Fact, CleanDatabase]
        public void Requeue_SetsFetchedAtValueToNull()
        {
            UseConnection(sql =>
            {
                // Arrange
                var id = CreateJobQueueRecord(sql, "1", "default");
                var processingJob = new SQLiteFetchedJob(sql, id, "1", "default");

                // Act
                processingJob.Requeue();

                // Assert
                var record = sql.Query("select * from [HangFire.JobQueue]").Single();
                Assert.Null(record.FetchedAt);
            });
        }

        [Fact, CleanDatabase]
        public void Dispose_SetsFetchedAtValueToNull_IfThereWereNoCallsToComplete()
        {
            UseConnection(sql =>
            {
                // Arrange
                var id = CreateJobQueueRecord(sql, "1", "default");
                var processingJob = new SQLiteFetchedJob(sql, id, "1", "default");

                // Act
                processingJob.Dispose();

                // Assert
                var record = sql.Query("select * from [HangFire.JobQueue]").Single();
                Assert.Null(record.FetchedAt);
            });
        }

        private static int CreateJobQueueRecord(IDbConnection connection, string jobId, string queue)
        {
            const string arrangeSql = @"
insert into [HangFire.JobQueue] (JobId, Queue, FetchedAt)
values (@id, @queue, datetime('now', 'utc'));
select last_insert_rowid() as Id;";

            return (int)connection.Query(arrangeSql, new { id = jobId, queue = queue }).Single().Id;
        }

        private static void UseConnection(Action<IDbConnection> action)
        {
            using (var connection = ConnectionUtils.CreateConnection())
            {
                action(connection);
            }
        }
    }
}
