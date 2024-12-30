using Dapper;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Data;

namespace STDP.Domain
{
    public class OracleDynamicParameters : SqlMapper.IDynamicParameters
    {
        private readonly DynamicParameters _dynamicParameters;
        private readonly List<OracleParameter> _oracleParameters;
        public OracleDynamicParameters(params string[] refCursorNames)
        {
            _dynamicParameters = new DynamicParameters();
        }
        public OracleDynamicParameters(object template, params string[] refCursorNames)
        {
            _dynamicParameters = new DynamicParameters(template);
            AddRefCursorParameters(refCursorNames);
        }
        public void AddRefCursorParameters(params string[] refCursorNames)
        {
            foreach (var item in refCursorNames)
                _oracleParameters.Add(new OracleParameter(item, OracleDbType.RefCursor, ParameterDirection.Output));
        }
        public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            ((SqlMapper.IDynamicParameters)_dynamicParameters).AddParameters(command, identity);
            if (command is OracleCommand oracleCommand)
                oracleCommand?.Parameters.AddRange(_oracleParameters.ToArray());
        }
    }
}
