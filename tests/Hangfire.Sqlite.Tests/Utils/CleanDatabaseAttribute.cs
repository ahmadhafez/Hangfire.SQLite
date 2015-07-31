﻿using System.IO;
using System.Reflection;
using System.Threading;
using System.Transactions;
using Hangfire.SQLite.Tests.Utils;
using Xunit.Sdk;

namespace Hangfire.SQLite.Tests
{
    public class CleanDatabaseAttribute : BeforeAfterTestAttribute
    {
        private static readonly object GlobalLock = new object();
        private static bool _sqlObjectInstalled;

        private readonly IsolationLevel _isolationLevel;
        private TransactionScope _transaction;

        public CleanDatabaseAttribute() : this(IsolationLevel.ReadCommitted)
        {}

        public CleanDatabaseAttribute(IsolationLevel isolationLevel)
        {
            _isolationLevel = isolationLevel;
        }

        public override void Before(MethodInfo methodUnderTest)
        {
            Monitor.Enter(GlobalLock);

            if (!_sqlObjectInstalled)
            {
                RecreateDatabaseAndInstallObjects();
                _sqlObjectInstalled = true;
            }

            _transaction = new TransactionScope(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = _isolationLevel });
        }

        public override void After(MethodInfo methodUnderTest)
        {
            try
            {
                _transaction.Dispose();
            }
            finally
            {
                Monitor.Exit(GlobalLock);
            }
        }

        private static void RecreateDatabaseAndInstallObjects()
        {
            //var recreateDatabaseSql = String.Format(
            //    @"if db_id('{0}') is null create database [{0}] COLLATE SQL_Latin1_General_CP1_CS_AS",
            //    ConnectionUtils.GetDatabaseName());

            //using (var connection = new SqlConnection(
            //    ConnectionUtils.GetMasterConnectionString()))
            //{
            //    connection.Execute(recreateDatabaseSql);
            //}

            if (File.Exists(ConnectionUtils.GetDatabaseName()))
                File.Delete(ConnectionUtils.GetDatabaseName());

            using (var connection = new System.Data.SQLite.SQLiteConnection(ConnectionUtils.GetConnectionString()))
            {
                SQLiteObjectsInstaller.Install(connection);
            }
        }
    }
}
