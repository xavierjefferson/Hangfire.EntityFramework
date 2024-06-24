using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire;



namespace Hangfire.EntityFrameworkStorage.Entities
{
    public partial class HangfireContext : IDisposable
    {
        private readonly HangfireContext _session
        {
            get { return this; }
        }
        private bool _flushed;

        public HangfireContext(HangfireContext dbContext, EntityFrameworkJobStorage storage)
        {
            _session = dbContext;
            Storage = storage;
        }

        public EntityFrameworkJobStorage Storage { get; }

        public void Flush()
        {
            _session.SaveChanges();
        }
        public void Dispose()
        {
            if (_session != null)
            {
                if (!_flushed)
                {
                    Flush();
                    _flushed = true;
                }

                _session.Dispose();
            }
        }

        public int DeleteAll<T>() where T : class
        {
            _session.RemoveRange( _session.Set<T>());
            return _session.SaveChanges();
        }

        public IQueryable<T> Query<T>() where T:class
        {
            return _session.Set<T>();
        }

        public IQuery CreateQuery(string queryString)
        {
            return _session.CreateQuery(queryString);
        }

        public void Insert<T>(IEnumerable<T> entities) where T : class
        {
            
            foreach (var item in entities) _session.Insert(item);
            Flush();
        }

        public new void Insert(object entity)
        {
            _session.Insert(entity);
            Flush();
        }

        public new void UpdateAndFlush(object entity)
        {
            _session.UpdateAndFlush(entity);
            Flush();
        }
    }
}