//
// Copyright (c) 2012 Krueger Systems, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace SQLite
{
	public partial class SQLiteAsyncConnection
	{
		SQLiteConnectionString _connectionString;

		public SQLiteAsyncConnection (string databasePath, bool storeDateTimeAsTicks = false)
		{
			_connectionString = new SQLiteConnectionString (databasePath, storeDateTimeAsTicks);
		}

		SQLiteConnectionWithLock GetConnection ()
		{
			return SQLiteConnectionPool.Shared.GetConnection (_connectionString);
		}

		public Task<CreateTablesResult> CreateTableAsync<T> ()
			where T : new ()
		{
			return CreateTablesAsync (typeof (T));
		}

		public Task<CreateTablesResult> CreateTablesAsync<T, T2> ()
			where T : new ()
			where T2 : new ()
		{
			return CreateTablesAsync (typeof (T), typeof (T2));
		}

		public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3> ()
			where T : new ()
			where T2 : new ()
			where T3 : new ()
		{
			return CreateTablesAsync (typeof (T), typeof (T2), typeof (T3));
		}

		public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4> ()
			where T : new ()
			where T2 : new ()
			where T3 : new ()
			where T4 : new ()
		{
			return CreateTablesAsync (typeof (T), typeof (T2), typeof (T3), typeof (T4));
		}

		public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4, T5> ()
			where T : new ()
			where T2 : new ()
			where T3 : new ()
			where T4 : new ()
			where T5 : new ()
		{
			return CreateTablesAsync (typeof (T), typeof (T2), typeof (T3), typeof (T4), typeof (T5));
		}

		public Task<CreateTablesResult> CreateTablesAsync (params Type[] types)
		{
			return Task.Factory.StartNew (() => {
				CreateTablesResult result = new CreateTablesResult ();
				var conn = GetConnection ();
				using (conn.Lock ()) {
					foreach (Type type in types) {
						int aResult = conn.CreateTable (type);
						result.Results[type] = aResult;
					}
				}
				return result;
			});
		}

		public Task<int> DropTableAsync<T> ()
			where T : new ()
		{
			return Task.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.DropTable<T> ();
				}
			});
		}

		public Task<int> InsertAsync (object item)
		{
			return Task.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.Insert (item);
				}
			});
		}

		public Task<int> UpdateAsync (object item)
		{
			return Task.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.Update (item);
				}
			});
		}

		public Task<int> DeleteAsync (object item)
		{
			return Task.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.Delete (item);
				}
			});
		}

        public Task<T> GetAsync<T>(object pk)
            where T : new()
        {
            return Task.Factory.StartNew(() =>
            {
                var conn = GetConnection();
                using (conn.Lock())
                {
                    return conn.Get<T>(pk);
                }
            });
        }

		public Task<T> FindAsync<T> (object pk)
			where T : new ()
		{
			return Task.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.Find<T> (pk);
				}
			});
		}
		
		public Task<T> GetAsync<T> (Expression<Func<T, bool>> predicate)
            where T : new()
        {
            return Task.Factory.StartNew(() =>
            {
                var conn = GetConnection();
                using (conn.Lock())
                {
                    return conn.Get<T> (predicate);
                }
            });
        }

		public Task<T> FindAsync<T> (Expression<Func<T, bool>> predicate)
			where T : new ()
		{
			return Task.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.Find<T> (predicate);
				}
			});
		}

		public Task<int> ExecuteAsync (string query, params object[] args)
		{
			return Task<int>.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.Execute (query, args);
				}
			});
		}

		public Task<int> InsertAllAsync (IEnumerable items)
		{
			return Task.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.InsertAll (items);
				}
			});
		}

        [Obsolete("Will cause a deadlock if any call in action ends up in a different thread. Use RunInTransactionAsync(Action<SQLiteConnection>) instead.")]
		public Task RunInTransactionAsync (Action<SQLiteAsyncConnection> action)
		{
			return Task.Factory.StartNew (() => {
				var conn = this.GetConnection ();
				using (conn.Lock ()) {
					conn.BeginTransaction ();
					try {
						action (this);
						conn.Commit ();
					}
					catch (Exception) {
						conn.Rollback ();
						throw;
					}
				}
			});
		}

        public Task RunInTransactionAsync(Action<SQLiteConnection> action)
        {
            return Task.Factory.StartNew(() =>
            {
                var conn = this.GetConnection();
                using (conn.Lock())
                {
                    conn.BeginTransaction();
                    try
                    {
                        action(conn);
                        conn.Commit();
                    }
                    catch (Exception)
                    {
                        conn.Rollback();
                        throw;
                    }
                }
            });
        }

		public AsyncTableQuery<T> Table<T> ()
			where T : new ()
		{
			//
			// This isn't async as the underlying connection doesn't go out to the database
			// until the query is performed. The Async methods are on the query iteself.
			//
			var conn = GetConnection ();
			return new AsyncTableQuery<T> (conn.Table<T> ());
		}

		public Task<T> ExecuteScalarAsync<T> (string sql, params object[] args)
		{
			return Task<T>.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					var command = conn.CreateCommand (sql, args);
					return command.ExecuteScalar<T> ();
				}
			});
		}

		public Task<List<T>> QueryAsync<T> (string sql, params object[] args)
			where T : new ()
		{
			return Task<List<T>>.Factory.StartNew (() => {
				var conn = GetConnection ();
				using (conn.Lock ()) {
					return conn.Query<T> (sql, args);
				}
			});
		}
	}

	//
	// TODO: Bind to AsyncConnection.GetConnection instead so that delayed
	// execution can still work after a Pool.Reset.
	//
	public class AsyncTableQuery<T>
		where T : new ()
	{
		TableQuery<T> _innerQuery;

		public AsyncTableQuery (TableQuery<T> innerQuery)
		{
			_innerQuery = innerQuery;
		}

		public AsyncTableQuery<T> Where (Expression<Func<T, bool>> predExpr)
		{
			return new AsyncTableQuery<T> (_innerQuery.Where (predExpr));
		}

		public AsyncTableQuery<T> Skip (int n)
		{
			return new AsyncTableQuery<T> (_innerQuery.Skip (n));
		}

		public AsyncTableQuery<T> Take (int n)
		{
			return new AsyncTableQuery<T> (_innerQuery.Take (n));
		}

		public AsyncTableQuery<T> OrderBy<U> (Expression<Func<T, U>> orderExpr)
		{
			return new AsyncTableQuery<T> (_innerQuery.OrderBy<U> (orderExpr));
		}

		public AsyncTableQuery<T> OrderByDescending<U> (Expression<Func<T, U>> orderExpr)
		{
			return new AsyncTableQuery<T> (_innerQuery.OrderByDescending<U> (orderExpr));
		}

		public Task<List<T>> ToListAsync ()
		{
			return Task.Factory.StartNew (() => {
				using (((SQLiteConnectionWithLock)_innerQuery.Connection).Lock ()) {
					return _innerQuery.ToList ();
				}
			});
		}

		public Task<int> CountAsync ()
		{
			return Task.Factory.StartNew (() => {
				using (((SQLiteConnectionWithLock)_innerQuery.Connection).Lock ()) {
					return _innerQuery.Count ();
				}
			});
		}

		public Task<T> ElementAtAsync (int index)
		{
			return Task.Factory.StartNew (() => {
				using (((SQLiteConnectionWithLock)_innerQuery.Connection).Lock ()) {
					return _innerQuery.ElementAt (index);
				}
			});
		}

		public Task<T> FirstAsync ()
		{
			return Task<T>.Factory.StartNew(() => {
				using (((SQLiteConnectionWithLock)_innerQuery.Connection).Lock ()) {
					return _innerQuery.First ();
				}
			});
		}

		public Task<T> FirstOrDefaultAsync ()
		{
			return Task<T>.Factory.StartNew(() => {
				using (((SQLiteConnectionWithLock)_innerQuery.Connection).Lock ()) {
					return _innerQuery.FirstOrDefault ();
				}
			});
		}
    }

	public class CreateTablesResult
	{
		public Dictionary<Type, int> Results { get; private set; }

		internal CreateTablesResult ()
		{
			this.Results = new Dictionary<Type, int> ();
		}
	}

	class SQLiteConnectionPool
	{
		class Entry
		{
			public SQLiteConnectionString ConnectionString { get; private set; }
			public SQLiteConnectionWithLock Connection { get; private set; }

			public Entry (SQLiteConnectionString connectionString)
			{
				ConnectionString = connectionString;
				Connection = new SQLiteConnectionWithLock (connectionString);
			}

			public void OnApplicationSuspended ()
			{
				Connection.Dispose ();
				Connection = null;
			}
		}

		readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry> ();
		readonly object _entriesLock = new object ();

		static readonly SQLiteConnectionPool _shared = new SQLiteConnectionPool ();

		/// <summary>
		/// Gets the singleton instance of the connection tool.
		/// </summary>
		public static SQLiteConnectionPool Shared
		{
			get
			{
				return _shared;
			}
		}

		public SQLiteConnectionWithLock GetConnection (SQLiteConnectionString connectionString)
		{
			lock (_entriesLock) {
				Entry entry;
				string key = connectionString.ConnectionString;

				if (!_entries.TryGetValue (key, out entry)) {
					entry = new Entry (connectionString);
					_entries[key] = entry;
				}

				return entry.Connection;
			}
		}

		/// <summary>
		/// Closes all connections managed by this pool.
		/// </summary>
		public void Reset ()
		{
			lock (_entriesLock) {
				foreach (var entry in _entries.Values) {
					entry.OnApplicationSuspended ();
				}
				_entries.Clear ();
			}
		}

		/// <summary>
		/// Call this method when the application is suspended.
		/// </summary>
		/// <remarks>Behaviour here is to close any open connections.</remarks>
		public void ApplicationSuspended ()
		{
			Reset ();
		}
	}

	class SQLiteConnectionWithLock : SQLiteConnection
	{
		readonly object _lockPoint = new object ();

		public SQLiteConnectionWithLock (SQLiteConnectionString connectionString)
			: base (connectionString.DatabasePath, connectionString.StoreDateTimeAsTicks)
		{
		}

		public IDisposable Lock ()
		{
			return new LockWrapper (_lockPoint);
		}

		private class LockWrapper : IDisposable
		{
			object _lockPoint;

			public LockWrapper (object lockPoint)
			{
				_lockPoint = lockPoint;
				Monitor.Enter (_lockPoint);
			}

			public void Dispose ()
			{
				Monitor.Exit (_lockPoint);
			}
		}
	}
}

