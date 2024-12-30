using System;
using System.Collections;
using System.Data;

namespace STDP.Domain
{
    public interface IUnitOfWork : IDisposable
    {
        IDbConnection Connection { get; }
        IGenericRepository<T> GenericRepository<T>() where T : class;
        void BeginTransaction();
        void Commit();
        void Rollback();
    }
    public class UnitOfWork : IUnitOfWork
    {
        readonly ApplicationDbContext _context;
        private Hashtable _repo;
        private bool _disposed;
        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IDbConnection Connection => _context.Connection;

        public IGenericRepository<T> GenericRepository<T>() where T : class
        {
            if (_repo == null) 
                _repo = new Hashtable();

            var type = typeof(T).Name;
            if (!_repo.ContainsKey(type))
                _repo.Add(type, new GenericRepository<T>(_context));

            return (IGenericRepository<T>)_repo[type];
        }

        public void BeginTransaction()
        {
            _context.BeginTransaction();
        }

        public void Commit()
        {
            _context.Commit();
        }

        public void Rollback()
        {
            _context.Rollback();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _context.Dispose();
            }
            _disposed = true;
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
