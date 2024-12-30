using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Data;
using System.Data.Common;

namespace STDP.Domain
{
    public class ApplicationDbContext : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private IDbConnection _connection;
        private IDbTransaction _transaction;
        private bool _disposed;

        public ApplicationDbContext(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString(_connectionString);
        }
        public ApplicationDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }
        public IDbConnection Connection
        {
            get
            {
                if (_connection == null || _connection.State == ConnectionState.Closed)
                {
                    _connection = new OracleConnection(_connectionString);
                    _connection.Open();
                }
                return _connection;
            }
        }
        public IDbTransaction Transaction => _transaction;
        public void BeginTransaction()
        {
            _transaction = Connection.BeginTransaction();
        }
        public void Commit()
        {
            _transaction?.Commit();
            DisposeTransaction();
        }
        public void Rollback()
        {
            _transaction?.Rollback();
            DisposeTransaction();
        }
        public void DisposeTransaction()
        {
            _transaction?.Dispose();
            _transaction = null;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                DisposeTransaction();
                if (_connection != null && _connection.State == ConnectionState.Open)
                    _connection.Close();

                _connection?.Dispose();
                _disposed = true;
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
