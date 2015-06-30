﻿// This file is part of Hangfire.
// Copyright © 2013-2014 Sergey Odinokov.
// 
// Hangfire is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as 
// published by the Free Software Foundation, either version 3 
// of the License, or any later version.
// 
// Hangfire is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public 
// License along with Hangfire. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;

namespace Hangfire.SqlServer
{
    internal class SQLiteJobQueueMonitoringApi : IPersistentJobQueueMonitoringApi
    {
        private readonly IDbConnection _connection;

        public SQLiteJobQueueMonitoringApi(IDbConnection connection)
        {
            if (connection == null) throw new ArgumentNullException("connection");

            _connection = connection;
        }

        public IEnumerable<string> GetQueues()
        {
            const string sqlQuery = @"select distinct(Queue) from HangFire.JobQueue";
            return _connection.Query(sqlQuery).Select(x => (string)x.Queue).ToList();
        }

        public IEnumerable<int> GetEnqueuedJobIds(string queue, int @from, int perPage)
        {
            const string sqlQuery = @"
select r.Id from (
  select j.Id, row_number() over (order by j.Id) as row_num 
  from HangFire.JobQueue jq
  left join HangFire.Job j on jq.JobId = j.Id
  left join HangFire.State s on s.Id = j.StateId
  where jq.Queue = @queue and jq.FetchedAt is null
) as r
where r.row_num between @start and @end";

            return _connection.Query<JobIdDto>(
                sqlQuery,
                new { queue = queue, start = from + 1, end = @from + perPage })
                .ToList()
                .Select(x => x.Id)
                .ToList();
        }

        public IEnumerable<int> GetFetchedJobIds(string queue, int @from, int perPage)
        {
            const string fetchedJobsSql = @"
select r.Id from (
  select j.Id, jq.FetchedAt, row_number() over (order by j.Id) as row_num 
  from HangFire.JobQueue jq
  left join HangFire.Job j on jq.JobId = j.Id
  where jq.Queue = @queue and jq.FetchedAt is not null
) as r
where r.row_num between @start and @end";

            return _connection.Query<JobIdDto>(
                fetchedJobsSql,
                new { queue = queue, start = from + 1, end = @from + perPage })
                .ToList()
                .Select(x => x.Id)
                .ToList();
        }

        public EnqueuedAndFetchedCountDto GetEnqueuedAndFetchedCount(string queue)
        {
            const string sqlQuery = @"
select sum(Enqueued) as EnqueuedCount, sum(Fetched) as FetchedCount 
from (
    select 
	    case when FetchedAt is null then 1 else 0 end as Enqueued,
	    case when FetchedAt is not null then 1 else 0 end as Fetched
    from HangFire.JobQueue
    where Queue = @queue
) q";

            var result = _connection.Query(sqlQuery, new { queue = queue }).Single();

            return new EnqueuedAndFetchedCountDto
            {
                EnqueuedCount = result.EnqueuedCount,
                FetchedCount = result.FetchedCount
            };
        }

// ReSharper disable once ClassNeverInstantiated.Local
        private class JobIdDto
        {
            public int Id { get; set; }
        }
    }
}