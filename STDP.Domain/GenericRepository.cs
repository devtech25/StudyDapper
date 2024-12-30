using Dapper;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace STDP.Domain
{
    public interface IGenericRepository<T>
    {
        T FirstOrDefault(string whereCause = null, object paramter = null);
        bool Any(string whereCause = null, object paramter = null);
        long Count(string whereCause = null, object paramter = null);
        T GetById(object id);
        IEnumerable<T> Get(string whereCause = null, object paramter = null);
        IEnumerable<T> GetAll();
        int Add(T entity);
        int Update(T entity);
        int Delete(T entity);
        int Delete(string whereCause = null, object paramter = null);
        (IEnumerable<T>, long totalCount) GetPaged(
           string columns,
           string tableNames,
           int pageIndex,
           int pageSize,
           string whereCause = "",
           object parameters = null,
           string orderby = ""
           );
    }
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;

        public GenericRepository(ApplicationDbContext context)
        {
            _connection = context.Connection;
            _transaction = context.Transaction;
        }
        public T FirstOrDefault(string whereCause = null, object paramter = null)
        {
            var query = $@"SELECT {GetColumnsAsProperties()} FROM {TableName}
            {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")}";

            return _connection.QueryFirstOrDefault<T>(query, paramter, _transaction);

        }
        public bool Any(string whereCause = null, object paramter = null)
        {
            var obj = FirstOrDefault(whereCause, paramter);

            return obj != null;
        }
        public long Count(string whereCause = null, object paramter = null)
        {
            var query = $@"SELECT COUNT(*) FROM {TableName}
            {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")}";

            return _connection.ExecuteScalar<long>(query, paramter, _transaction);
        }
        public T GetById(object id)
        {
            string query = $"SELECT {GetColumnsAsProperties()} FROM {TableName} WHERE {KeyColumnName} = :Id";

            return _connection.QueryFirstOrDefault<T>(query, new { Id = id }, _transaction);
        }
        public IEnumerable<T> Get(string whereCause = null, object paramter = null)
        {
            var query = $@"SELECT {GetColumnsAsProperties()} FROM {TableName}
            {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")}";

            return _connection.Query<T>(query, paramter, _transaction);
        }
        public IEnumerable<T> GetAll()
        {
            return _connection.Query<T>($"SELECT {GetColumnsAsProperties()} FROM {TableName}", _transaction);
        }
        public int Add(T entity)
        {
            string columns = GetColumns();
            string properties = GetPropertyNames();
            string query = $"INSERT INTO {TableName} ({columns}) VALUES ({properties})";

            return _connection.Execute(query, entity, _transaction);
        }
        public int Update(T entity)
        {
            StringBuilder query = new StringBuilder();
            query.Append($"UPDATE {TableName} SET ");

            foreach (var property in GetProperties(true))
            {
                var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();

                string propertyName = property.Name;
                string columnName = columnAttribute?.Name ?? "";

                query.Append($"{columnName} = :{propertyName},");
            }

            query.Remove(query.Length - 1, 1);

            query.Append($" WHERE {KeyColumnName} = :{KeyPropertyName}");

            return _connection.Execute(query.ToString(), entity, _transaction);
        }
        public int Delete(T entity)
        {
            string query = $"DELETE FROM {TableName} WHERE {KeyColumnName} = :{KeyPropertyName}";

            return _connection.Execute(query, entity, _transaction);
        }
        public int Delete(string whereCause = null, object paramter = null)
        {
            string query = $@"DELETE FROM {TableName}
            {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")}";
            return _connection.Execute(query, paramter, _transaction);
        }
        public (IEnumerable<T>, long totalCount) GetPaged(
            string columns,
            string tableNames,
            int pageIndex,
            int pageSize,
            string whereCause = "",
            object parameters = null,
            string orderby = ""
            )
        {
            var offset = (pageIndex - 1) * pageSize;
            var dynParams = new DynamicParameters(parameters);
            // Solution 1
            //dynParams.Add("p_offset", offset);
            //dynParams.Add("p_pageSize", pageSize);

            //var query = $@"
            //BEGIN
            //    OPEN :C1 FOR SELECT a.*, ROWNUM rnum FROM (
            //                    SELECT {columns}
            //                    FROM {tableNames}
            //                    {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")}
            //                    {(!string.IsNullOrWhiteSpace(orderby) ? "ORDER BY " + orderby : "")}
            //                    ) a
            //                    OFFSET :p_offset ROWS FETCH NEXT :p_pageSize ROWS ONLY;

            //    OPEN :C2 FOR SELECT COUNT(*) FROM {tableNames}
            //                    {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")};
            //END;
            //";

            // Solution 2
            int startRow = (pageIndex - 1) * pageSize; 
            int endRow = startRow + pageSize;
            dynParams.Add("p_startRow", startRow);
            dynParams.Add("p_endRow", endRow);

            var query = $@"
            BEGIN
                OPEN :C1 FOR SELECT * FROM (
                                SELECT a.*, ROWNUM rnum FROM (
                                    SELECT {columns}
                                    FROM {tableNames}
                                    {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")}
                                    {(!string.IsNullOrWhiteSpace(orderby) ? "ORDER BY " + orderby : "")}
                                ) a
                                WHERE ROWNUM <:p_endRow
                             )
                             WHERE rnum > :p_startRow;
                
                OPEN :C2 FOR SELECT COUNT(*) FROM {tableNames}
                                {(!string.IsNullOrWhiteSpace(whereCause) ? "WHERE " + whereCause : "")};
            END;
            ";

            string[] refCursorNames = { "C1", "C2" };
            var orclParams = new OracleDynamicParameters(dynParams, refCursorNames);

            using (var multi = _connection.QueryMultiple(query, orclParams, _transaction))
            {
                var data = multi.Read<T>();
                var totalCount = multi.Read<long>().Single();
                return (data, totalCount);
            }
        }

        private string TableName
        {
            get
            {
                var type = typeof(T);
                var tableAttribute = type.GetCustomAttribute<TableAttribute>();
                if (tableAttribute != null)
                    return tableAttribute.Name;

                return type.Name;
            }
        }
        private static string KeyColumnName
        {
            get
            {
                PropertyInfo[] properties = typeof(T).GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    object[] keyAttributes = property.GetCustomAttributes(typeof(KeyAttribute), true);

                    if (keyAttributes != null && keyAttributes.Length > 0)
                    {
                        object[] columnAttributes = property.GetCustomAttributes(typeof(ColumnAttribute), true);

                        if (columnAttributes != null && columnAttributes.Length > 0)
                        {
                            ColumnAttribute columnAttribute = (ColumnAttribute)columnAttributes[0];
                            return columnAttribute?.Name ?? "";
                        }
                        else
                        {
                            return property.Name;
                        }
                    }
                }

                return null;
            }
        }
        private string GetColumns(bool excludeKey = false)
        {
            var type = typeof(T);
            var columns = string.Join(", ", type.GetProperties()
                .Where(p => !excludeKey || !p.IsDefined(typeof(KeyAttribute)))
                .Select(p =>
                {
                    var columnAttribute = p.GetCustomAttribute<ColumnAttribute>();
                    return columnAttribute != null ? columnAttribute.Name : p.Name;
                }));

            return columns;
        }
        private string GetColumnsAsProperties(bool excludeKey = false)
        {
            var type = typeof(T);
            var columnsAsProperties = string.Join(", ", type.GetProperties()
                .Where(p => !excludeKey || !p.IsDefined(typeof(KeyAttribute)))
                .Select(p =>
                {
                    var columnAttribute = p.GetCustomAttribute<ColumnAttribute>();
                    return columnAttribute != null ? $"{columnAttribute.Name} AS {p.Name}" : p.Name;
                }));

            return columnsAsProperties;
        }
        private string GetPropertyNames(bool excludeKey = false)
        {
            var properties = typeof(T).GetProperties()
                .Where(p => !excludeKey || p.GetCustomAttribute<KeyAttribute>() == null);

            var values = string.Join(", ", properties.Select(p => $":{p.Name}"));

            return values;
        }
        private IEnumerable<PropertyInfo> GetProperties(bool excludeKey = false)
        {
            var properties = typeof(T).GetProperties()
                .Where(p => !excludeKey || p.GetCustomAttribute<KeyAttribute>() == null);

            return properties;
        }
        private string KeyPropertyName
        {
            get
            {
                var properties = typeof(T).GetProperties()
                    .Where(p => p.GetCustomAttribute<KeyAttribute>() != null).ToList();

                if (properties.Any())
                    return properties?.FirstOrDefault()?.Name ?? null;

                return null;
            }
        }
    }
}
